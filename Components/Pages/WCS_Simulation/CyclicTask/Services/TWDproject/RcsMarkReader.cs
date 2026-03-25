using System.Net.Http;
using System.Text;
using System.Text.Json;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;
using MySqlConnector;
using Microsoft.Extensions.Logging;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject
{
    // Cargo 数据/快照模型
    public record CargoRecord(string ContainerCode, string Mark, DateTime RetrievedAt);
    // Cargo 快照包含多条记录和快照时间
    public record CargoSnapshot(IReadOnlyList<CargoRecord> Items, DateTime SnapshotTime);

    public interface ICyclicTasksIssuing
    {

        // 新增：cargo 相关方法
        Task<CargoSnapshot> ReadCargoAsync(CancellationToken ct = default);
        CargoSnapshot? GetLatestCargoSnapshot();

        // 把 cargo 解析为 RCS 下发格式并通过 HTTP 批量下发
        Task<bool> DispatchCargoAsTasksAsync(string endpoint, CancellationToken ct = default);
        // 先刷新 cargo 缓存再下发
        Task<bool> RefreshAndDispatchCargoAsync(string endpoint, CancellationToken ct = default);
    }

    public sealed class CyclicTasksIssuingTWD : ICyclicTasksIssuing
    {
        private readonly IRcsDbService _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<CyclicTasksIssuingTWD>? _logger;

        private readonly object _lock = new();
        private CargoSnapshot? _cargoLatest;

        public CyclicTasksIssuingTWD(IRcsDbService db, IHttpClientFactory httpFactory, ILogger<CyclicTasksIssuingTWD>? logger = null)
        {
            _db = db;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        // ----------------- cargo 读取与缓存 -----------------
        // 假定 cargo 表有列 container_code, mark —— 若不同请修改 SQL
        private const string SqlReadCargo = "SELECT code, home_station FROM cargo";

        public async Task<CargoSnapshot> ReadCargoAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            //读每一行数据，转换成 CargoRecord 列表
            var list = await _db.QueryAsync(SqlReadCargo, rdr =>
            {
                var container = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                var mark = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                return new CargoRecord(container, mark, now);
            }, ct);

            //生成快照并更新缓存
            var snapshot = new CargoSnapshot(list, now);
            lock (_lock) { _cargoLatest = snapshot; }
            return snapshot;
        }

        //获取最新 cargo 快照（可能为 null）
        public CargoSnapshot? GetLatestCargoSnapshot()
        {
            lock (_lock) { return _cargoLatest; }
        }

        // ----------------- 将 cargo 转换为 RCS 下发任务并批量下发 -----------------
        // 1) 组装下发格式（示例：GroupId/MsgTime/Tasks[]）；2) POST JSON 到 endpoint；3) 返回是否成功（2xx）
        public async Task<bool> DispatchCargoAsTasksAsync(string endpoint, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                throw new ArgumentException("endpoint 不能为空", nameof(endpoint));

            // 确保有缓存数据
            var snapshot = GetLatestCargoSnapshot() ?? await ReadCargoAsync(ct);

            // 解析成 RCS/下发任务结构（示例结构，可按 RCS 接口调整）
            var requestPayload = new
            {
                GroupId = Guid.NewGuid().ToString(),
                MsgTime = DateTime.UtcNow,
                Tasks = snapshot.Items.Select(item => new
                {
                    TaskId = $"TK{DateTime.UtcNow:yyyyMMddHHmmssfff}{item.ContainerCode}",
                    TaskType = "TWD_TASK", // 根据业务替换
                    ContainerCode = item.ContainerCode,
                    Mark = item.Mark
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpFactory.CreateClient(); // 可改成命名客户端以设置 BaseAddress/Token
            try
            {
                using var resp = await client.PostAsync(endpoint, content, ct);
                if (resp.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("DispatchCargoAsTasksAsync 成功，下发 {Count} 条任务 到 {Endpoint}", requestPayload.Tasks.Length, endpoint);
                    return true;
                }
                else
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger?.LogWarning("DispatchCargoAsTasksAsync 失败 HTTP {Status}: {Error}", resp.StatusCode, err);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "DispatchCargoAsTasksAsync 异常");
                return false;
            }
        }

        public async Task<bool> RefreshAndDispatchCargoAsync(string endpoint, CancellationToken ct = default)
        {
            await ReadCargoAsync(ct);
            return await DispatchCargoAsTasksAsync(endpoint, ct);
        }
    }
}