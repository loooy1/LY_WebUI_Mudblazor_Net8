using System.Data;
using MySqlConnector;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services
{
    public interface IRcsDbService
    {
        Task<bool> TestConnectionAsync(CancellationToken ct = default);
        Task<int> QuerySomeCountAsync(CancellationToken ct = default);
        Task DisconnectAsync(CancellationToken ct = default);

        // 新增：通用查询方法，map 将把每行映射为 T
        Task<List<T>> QueryAsync<T>(string sql, Func<MySqlDataReader, T> map, CancellationToken ct = default);
    }

    public sealed class RcsDbService : IRcsDbService
    {
        private readonly ICyclicConfigReader _cfgReader;
        private readonly ICyclicConfigWriter _cfgWriter;

        // 持有打开的连接，直到 DisconnectAsync 被调用
        private MySqlConnection? _connection;
        private string? _currentConnectionString;
        private readonly object _sync = new();

        public RcsDbService(ICyclicConfigReader cfgReader, ICyclicConfigWriter cfgWriter)
        {
            _cfgReader = cfgReader;
            _cfgWriter = cfgWriter;
        }

        private static string BuildConnectionString(RcsConnectionConfig cfg)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = cfg.Host,
                Port = (uint)cfg.Port,
                Database = cfg.Database,
                UserID = cfg.User,
                Password = cfg.Password,
                SslMode = MySqlSslMode.None,
                ConnectionTimeout = (uint)Math.Max(1, cfg.ConnectTimeoutSeconds)
            };
            return builder.ConnectionString;
        }

        // 打开并保持连接（不在此处关闭），并把连接状态写回配置
        public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
        {
            var cfg = _cfgReader.Get();
            if (cfg is null) return false;

            var cs = BuildConnectionString(cfg);

            // 如果已有匹配的打开连接，直接返回成功并更新状态
            lock (_sync)
            {
                if (_connection != null
                    && _currentConnectionString == cs
                    && _connection.State == ConnectionState.Open)
                {
                    var sameCfg = cfg.Clone();
                    sameCfg.ConnectionState = ConnState.Connected;
                    sameCfg.LastStatusMessage = "Already connected";
                    sameCfg.LastCheckedUtc = DateTime.UtcNow;
                    _cfgWriter.Save(sameCfg);
                    return true;
                }
            }

            // 标记为检测中
            var testing = cfg.Clone();
            testing.ConnectionState = ConnState.Testing;
            testing.LastStatusMessage = "Testing...";
            testing.LastCheckedUtc = DateTime.UtcNow;
            _cfgWriter.Save(testing);

            MySqlConnection? newConn = null;
            try
            {
                newConn = new MySqlConnection(cs);
                await newConn.OpenAsync(ct);

                // 轻量验证
                await using (var cmd = newConn.CreateCommand())
                {
                    cmd.CommandText = "SELECT 1";
                    var result = await cmd.ExecuteScalarAsync(ct);
                    var ok = result != null;

                    var okCfg = cfg.Clone();
                    okCfg.ConnectionState = ok ? ConnState.Connected : ConnState.Disconnected;
                    okCfg.LastStatusMessage = ok ? "OK" : "Query returned null";
                    okCfg.LastCheckedUtc = DateTime.UtcNow;
                    _cfgWriter.Save(okCfg);

                    if (!ok)
                    {
                        // 关闭并抛出以进入 catch 分支统一处理
                        await newConn.CloseAsync();
                        await newConn.DisposeAsync();
                        newConn = null;
                        return false;
                    }
                }

                // 成功：保存并持有该连接实例（替换旧的连接）
                lock (_sync)
                {
                    // 释放旧连接（如果存在）
                    if (_connection != null)
                    {
                        try
                        {
                            _connection.Close();
                            _connection.Dispose();
                        }
                        catch { }
                    }

                    _connection = newConn;
                    _currentConnectionString = cs;
                    newConn = null; // ownership moved
                }

                return true;
            }
            catch (Exception ex)
            {
                // 写回失败信息
                var failCfg = cfg.Clone();
                failCfg.ConnectionState = ConnState.Disconnected;
                failCfg.LastStatusMessage = ex.Message;
                failCfg.LastCheckedUtc = DateTime.UtcNow;
                _cfgWriter.Save(failCfg);

                // 清理临时连接
                if (newConn != null)
                {
                    try
                    {
                        await newConn.CloseAsync();
                        await newConn.DisposeAsync();
                    }
                    catch { }
                }

                return false;
            }
        }

        // 查询示例：优先使用已打开的连接，否则临时打开
        public async Task<int> QuerySomeCountAsync(CancellationToken ct = default)
        {
            var cfg = _cfgReader.Get();
            if (cfg is null) return -1;
            var cs = BuildConnectionString(cfg);

            // 优先使用持有连接
            MySqlConnection? useConn = null;
            lock (_sync)
            {
                if (_connection != null && _connection.State == ConnectionState.Open && _currentConnectionString == cs)
                    useConn = _connection;
            }

            if (useConn != null)
            {
                try
                {
                    await using var cmd = useConn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(1) FROM your_table LIMIT 1;"; // 替换为真实查询
                    var result = await cmd.ExecuteScalarAsync(ct);
                    return Convert.ToInt32(result);
                }
                catch
                {
                    return -1;
                }
            }
            else
            {
                // 临时连接（不保留）
                try
                {
                    await using var temp = new MySqlConnection(cs);
                    await temp.OpenAsync(ct);
                    await using var cmd = temp.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(1) FROM your_table LIMIT 1;"; // 替换为真实查询
                    var result = await cmd.ExecuteScalarAsync(ct);
                    await temp.CloseAsync();
                    return Convert.ToInt32(result);
                }
                catch
                {
                    return -1;
                }
            }
        }

        // 统一断开/清理：关闭持有连接并清空内存配置与连接池
        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            // 关闭并释放持有连接
            lock (_sync)
            {
                if (_connection != null)
                {
                    try
                    {
                        if (_connection.State == ConnectionState.Open)
                        {
                            // CloseAsync 不支持 CancellationToken in some versions; call sync Close
                            _connection.Close();
                        }
                    }
                    catch { }

                    try { _connection.Dispose(); } catch { }
                    _connection = null;
                    _currentConnectionString = null;
                }
            }

            // 更新配置为断开状态
            var disconnected = new RcsConnectionConfig
            {
                ConnectionState = ConnState.Disconnected,
                LastStatusMessage = "Disconnected",
                LastCheckedUtc = DateTime.UtcNow
            };
            _cfgWriter.Save(disconnected);

            // 清理连接池
            try
            {
                MySqlConnection.ClearAllPools();
            }
            catch { }

            await Task.CompletedTask;
        }

        // 新增：通用查询实现
        public async Task<List<T>> QueryAsync<T>(string sql, Func<MySqlDataReader, T> map, CancellationToken ct = default)
        {
            var cfg = _cfgReader.Get();
            if (cfg is null) return new List<T>();
            var cs = BuildConnectionString(cfg);

            // 优先使用持有连接
            MySqlConnection? useConn = null;
            lock (_sync)
            {
                if (_connection != null && _connection.State == ConnectionState.Open && _currentConnectionString == cs)
                    useConn = _connection;
            }

            if (useConn != null)
            {
                // 使用现有连接（不关闭）
                var list = new List<T>();
                try
                {
                    await using var cmd = useConn.CreateCommand();
                    cmd.CommandText = sql;
                    await using var rdr = await cmd.ExecuteReaderAsync(ct);
                    while (await rdr.ReadAsync(ct))
                    {
                        list.Add(map(rdr));
                    }
                }
                catch
                {
                    // 出错返回空列表（调用方可检测）
                    return new List<T>();
                }
                return list;
            }
            else
            {
                // 临时连接（打开后关闭）
                try
                {
                    await using var temp = new MySqlConnection(cs);
                    await temp.OpenAsync(ct);
                    var list = new List<T>();
                    await using (var cmd = temp.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        await using var rdr = await cmd.ExecuteReaderAsync(ct);
                        while (await rdr.ReadAsync(ct))
                        {
                            list.Add(map(rdr));
                        }
                    }
                    await temp.CloseAsync();
                    return list;
                }
                catch
                {
                    return new List<T>();
                }
            }
        }
    }
}