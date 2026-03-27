using System.Net.Http;
using System.Text;
using System.Text.Json;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using static LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject.CyclicTasksIssuingTWD;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject
{
    //// Cargo 数据/快照模型
    //public record BaseCargoRecord(string ContainerCode, string Mark, DateTime RetrievedAt);
    //// Cargo 快照包含多条记录和快照时间
    //public record CargoSnapshot(IReadOnlyList<BaseCargoRecord> Items, DateTime SnapshotTime);

    //// Conveyor 仅保存 transfer_points 的数据/快照模型
    //public record ConveyorRecord(string TransferPoints, DateTime RetrievedAt);
    //public record ConveyorSnapshot(IReadOnlyList<ConveyorRecord> Items, DateTime SnapshotTime);

    // 新增：storage 专用记录与快照类型（保留 AreaRecord/AreaSnapshot 用于 conveyor/sorting）
    public record StorageAreaRecord(string WmsCode, string Cargo, DateTime RetrievedAt);
    public record StorageAreaSnapshot(IReadOnlyList<StorageAreaRecord> Items, DateTime SnapshotTime);
    public record AreaRecord(string WmsCode, DateTime RetrievedAt);
    public record AreaSnapshot(IReadOnlyList<AreaRecord> Items, DateTime SnapshotTime);

    public interface ICyclicTasksIssuing
    {
        //// cargo 相关方法（保留原方法签名）
        //Task<CargoSnapshot> ReadCargoAsync(CancellationToken ct = default);
        //CargoSnapshot? GetLatestCargoSnapshot();

        //// conveyors 相关
        //Task<ConveyorSnapshot> ReadConveyorsAsync(CancellationToken ct = default);
        //ConveyorSnapshot? GetLatestConveyorSnapshot();

        // 新增：读取 cargo_area_instances 并分成三个区域快照
        Task<(StorageAreaSnapshot StorageArea, AreaSnapshot ConveyorArea, AreaSnapshot SortingArea)> ReadCargoAreaInstancesAsync(CancellationToken ct = default);

        Task Test(int times);
    }

    public sealed class CyclicTasksIssuingTWD : ICyclicTasksIssuing
    {
        private readonly IRcsDbService _db;
        private readonly ILogger<CyclicTasksIssuingTWD>? _logger;
        private readonly IWcsTaskHttpService _wcsTaskHttpService;

        //private readonly object _lock = new();
        //private CargoSnapshot? _cargoLatest;

        // conveyors 缓存与锁（仅保存 transfer_points）
        //private readonly object _conveyorLock = new();
        //private ConveyorSnapshot? _conveyorLatest;


        public CyclicTasksIssuingTWD(IRcsDbService db, IWcsTaskHttpService wcsTaskHttpService, ILogger<CyclicTasksIssuingTWD>? logger = null)
        {
            _db = db;
            _logger = logger;
            _wcsTaskHttpService = wcsTaskHttpService;
        }

        // ----------------- cargo 读取与缓存 -----------------
        // 假定 cargo 表有列 code, home_station —— 若不同请修改 SQL
        //private const string SqlReadCargo = "SELECT code, home_station FROM cargo";

        //public async Task<CargoSnapshot> ReadCargoAsync(CancellationToken ct = default)
        //{
        //    var now = DateTime.UtcNow;

        //    var list = await _db.QueryAsync(SqlReadCargo, rdr =>
        //    {
        //        var container = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
        //        var mark = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
        //        return new BaseCargoRecord(container, mark, now);
        //    }, ct);

        //    var snapshot = new CargoSnapshot(list, now);
        //    lock (_lock) { _cargoLatest = snapshot; }

        //    return _cargoLatest;
        ////}

        //public CargoSnapshot? GetLatestCargoSnapshot()
        //{
        //    lock (_lock) { return _cargoLatest; }
        //}

        //// ----------------- conveyors 读取与缓存（仅 transfer_points） -----------------
        //// 假定 conveyors 表有列 transfer_points —— 若不同请修改 SQL
        //private const string SqlReadConveyors = "SELECT transfer_points FROM conveyors";

        //public async Task<ConveyorSnapshot> ReadConveyorsAsync(CancellationToken ct = default)
        //{
        //    var now = DateTime.UtcNow;

        //    var list = await _db.QueryAsync(SqlReadConveyors, rdr =>
        //    {
        //        var transferPointsRaw = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);

        //        // 提取并归一化为 ASCII 数字
        //        var sb = new StringBuilder(Math.Min(16, transferPointsRaw.Length));
        //        foreach (var ch in transferPointsRaw)
        //        {
        //            if (ch >= '0' && ch <= '9')
        //            {
        //                sb.Append(ch);
        //                continue;
        //            }

        //            if (char.IsDigit(ch))
        //            {
        //                var val = (int)char.GetNumericValue(ch);
        //                if (val >= 0 && val <= 9)
        //                    sb.Append((char)('0' + val));
        //            }
        //        }

        //        return new ConveyorRecord(sb.ToString(), now);
        //    }, ct);

        //    var snapshot = new ConveyorSnapshot(list, now);
        //    lock (_conveyorLock) { _conveyorLatest = snapshot; }
        //    return snapshot;
        //}

        //public ConveyorSnapshot? GetLatestConveyorSnapshot()
        //{
        //    lock (_conveyorLock) { return _conveyorLatest; }
        //}

        // ----------------- 新方法：读取 cargo_area_instances 并按 cargo_area 分类 -----------------
        // 假定表结构：cargo_area_instances(cargo_area, cargo, wms_code)
        private const string SqlReadCargoAreaInstances = "SELECT cargo_area, wms_code, cargo FROM cargo_area_instances";

        public async Task<(StorageAreaSnapshot StorageArea, AreaSnapshot ConveyorArea, AreaSnapshot SortingArea)> ReadCargoAreaInstancesAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // 从 DB 读取三列：cargo_area, wms_code, cargo
            var rows = await _db.QueryAsync(SqlReadCargoAreaInstances, rdr =>
            {
                var area = rdr.IsDBNull(0) ? string.Empty : rdr.GetString(0);
                var wms = rdr.IsDBNull(1) ? string.Empty : rdr.GetString(1);
                var cargo = rdr.FieldCount > 2 && !rdr.IsDBNull(2) ? rdr.GetString(2) : string.Empty;
                return (Area: area ?? string.Empty, Wms: wms ?? string.Empty, Cargo: cargo ?? string.Empty);
            }, ct);

            var storage = new List<StorageAreaRecord>();
            var conveyor = new List<AreaRecord>();
            var sorting = new List<AreaRecord>();


            foreach (var r in rows)
            {
                var areaNormalized = (r.Area ?? string.Empty).Trim().ToLowerInvariant();

                if (areaNormalized.Contains("storage"))
                {
                    // storage 类型：使用 wms_code 与 cargo 字段都保存
                    if (!string.IsNullOrWhiteSpace(r.Wms) || !string.IsNullOrWhiteSpace(r.Cargo))
                        storage.Add(new StorageAreaRecord(r.Wms, r.Cargo, now));
                }
                else if (areaNormalized.Contains("conveyor"))
                {
                    if (!string.IsNullOrWhiteSpace(r.Wms))
                        conveyor.Add(new AreaRecord(r.Wms, now));
                }
                else if (areaNormalized.Contains("sorting"))
                {
                    if (!string.IsNullOrWhiteSpace(r.Wms))
                        sorting.Add(new AreaRecord(r.Wms, now));
                }
                else
                {
                    // 未识别类型：按需处理，这里默认归到 storage（保存两值）
                    if (!string.IsNullOrWhiteSpace(r.Wms) || !string.IsNullOrWhiteSpace(r.Cargo))
                        storage.Add(new StorageAreaRecord(r.Wms, r.Cargo, now));
                }
            }

            return (
                new StorageAreaSnapshot(storage.AsReadOnly(), now),
                new AreaSnapshot(conveyor.AsReadOnly(), now),
                new AreaSnapshot(sorting.AsReadOnly(), now)
            );
        }

        //// 新增类型：用于区分存放
        //public record ContainerRecord(string ContainerCode, string Mark, DateTime RetrievedAt);
        //public record CargoRecord(string ContainerCode, string Mark, DateTime RetrievedAt);

        //// 将 _cargoLatest 中的 BaseCargoRecord 拆分到两个集合（mark 只保留 ASCII 数字）
        //private (IReadOnlyList<ContainerRecord> Containers, IReadOnlyList<CargoRecord> Cargos) SplitLatestBaseCargo()
        //{
        //    List<ContainerRecord> containers = new();
        //    List<CargoRecord> cargos = new();

        //    CargoSnapshot? snapshot;
        //    lock (_lock)
        //    {
        //        snapshot = _cargoLatest;
        //    }

        //    if (snapshot?.Items == null || snapshot.Items.Count == 0)
        //        return (containers.AsReadOnly(), cargos.AsReadOnly());

        //    static string ExtractDigitsAscii(string? mark)
        //    {
        //        if (string.IsNullOrEmpty(mark)) return string.Empty;
        //        var sb = new StringBuilder(Math.Min(16, mark.Length));
        //        foreach (var ch in mark)
        //        {
        //            if (ch >= '0' && ch <= '9') { sb.Append(ch); continue; }
        //            if (char.IsDigit(ch))
        //            {
        //                var val = (int)char.GetNumericValue(ch);
        //                if (val >= 0 && val <= 9) sb.Append((char)('0' + val));
        //            }
        //        }
        //        return sb.ToString();
        //    }

        //    foreach (var rec in snapshot.Items)
        //    {
        //        var code = rec.ContainerCode ?? string.Empty;
        //        var digits = ExtractDigitsAscii(rec.Mark);

        //        if (!string.IsNullOrEmpty(code) && code.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0)
        //        {
        //            containers.Add(new ContainerRecord(code, digits, rec.RetrievedAt));
        //        }
        //        else if (!string.IsNullOrEmpty(code) && code.IndexOf("cargo", StringComparison.OrdinalIgnoreCase) >= 0)
        //        {
        //            cargos.Add(new CargoRecord(code, digits, rec.RetrievedAt));
        //        }
        //        else
        //        {
        //            cargos.Add(new CargoRecord(code, digits, rec.RetrievedAt));
        //        }
        //    }

        //    return (containers.AsReadOnly(), cargos.AsReadOnly());
        //}


        public async Task Test(int times)
        {
            var (storageArea, conveyorArea, sortingArea) = await ReadCargoAreaInstancesAsync();

            var rnd = new Random();
            var tasks = new List<CyclicTaskModel>(Math.Max(0, times));

            if (storageArea?.Items == null || storageArea.Items.Count == 0
                || conveyorArea?.Items == null || conveyorArea.Items.Count == 0)
            {
                _logger?.LogWarning("无法生成任务：storageArea 或 conveyorArea 为空。storage={StorageCount}, conveyor={ConveyorCount}",
                    storageArea?.Items.Count ?? 0, conveyorArea?.Items.Count ?? 0);
                return;
            }

            // 在循环外按条件分好数组，避免在循环中分配
            var storageAllArr = storageArea.Items.ToArray();
            var storageWithContainerArr = storageAllArr
                .Where(s => !string.IsNullOrWhiteSpace(s.Cargo) && s.Cargo.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            var storageWithCargoArr = storageAllArr
                .Where(s => !string.IsNullOrWhiteSpace(s.Cargo) && s.Cargo.IndexOf("cargo", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            var storageAllCount = storageAllArr.Length;
            var containerCount = storageWithContainerArr.Length;
            var cargoCount = storageWithCargoArr.Length;
            var conveyorArr = conveyorArea.Items.ToArray();
            var conveyorCount = conveyorArr.Length;

            for (int i = 0; i < times; i++)
            {
                bool isInbound = rnd.Next(2) == 0;

                // 选择 TransferLocation 的 conveyor（按预计算数组与计数）
                var conveyor = conveyorArr[rnd.Next(conveyorCount)];

                // 根据入库/出库选择符合条件的 storage（使用预分好的数组，避免循环内 LINQ）
                StorageAreaRecord chosenStorage;
                if (isInbound)
                {
                    chosenStorage = containerCount > 0
                        ? storageWithContainerArr[rnd.Next(containerCount)]
                        : storageAllArr[rnd.Next(storageAllCount)];
                }
                else
                {
                    chosenStorage = cargoCount > 0
                        ? storageWithCargoArr[rnd.Next(cargoCount)]
                        : storageAllArr[rnd.Next(storageAllCount)];
                }

                // 解析 chosenStorage.Cargo 为 Warehouse:Carrier（简单、低开销）
                var cargoRaw = chosenStorage.Cargo ?? string.Empty;
                var idx = cargoRaw.IndexOf(':');
                var parsedWarehouse = idx >= 0 ? cargoRaw.Substring(0, idx).Trim() : cargoRaw.Trim();
                var parsedCarrier = idx >= 0 ? cargoRaw[(idx + 1)..].Trim() : string.Empty;

                var cyc = new CyclicTaskModel
                {
                    TaskNo = Guid.NewGuid().ToString("N"),
                    TaskType = isInbound ? "CONTAINER_INBOUND" : "CONTAINER_OUTBOUND",
                    CarrierCode = parsedCarrier,
                    Warehouse = parsedWarehouse,
                    Priority = 1,
                    CreatedTime = DateTime.UtcNow,
                    Quantity = 1,
                    SourceLocation = chosenStorage.WmsCode ?? string.Empty,
                    TransferLocation = conveyor.WmsCode ?? string.Empty,
                    TargetLocation = chosenStorage.WmsCode ?? string.Empty,
                };

                tasks.Add(cyc);
            }

            await SendSequentialAsync(tasks, CancellationToken.None);

            _logger?.LogInformation("生成周期任务数量：{Count}", tasks.Count);
        }

        // 顺序发送辅助方法：逐条发送，支持取消与日志
        private async Task SendSequentialAsync(IEnumerable<CyclicTaskModel> tasks, CancellationToken ct = default)
        {
            if (tasks == null) return;

            foreach (var t in tasks)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var (success, message) = await _wcsTaskHttpService.SendTaskAsync(t, "TargetSystem", ct).ConfigureAwait(false);
                    if (success)
                        _logger?.LogInformation("任务下发成功 TaskNo={TaskNo}", t.TaskNo);
                    else
                        _logger?.LogWarning("任务下发失败 TaskNo={TaskNo} Msg={Msg}", t.TaskNo, message);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger?.LogWarning("任务下发被取消 TaskNo={TaskNo}", t.TaskNo);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "任务下发异常 TaskNo={TaskNo}", t.TaskNo);
                }

                // 可选短延迟，防止瞬时过载目标系统（按需调整或移除）
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
        }
    }
}