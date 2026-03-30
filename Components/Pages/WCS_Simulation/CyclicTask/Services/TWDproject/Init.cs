using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Shared.Services; // 新增
using Microsoft.Extensions.Logging;
using MySqlConnector;
using static LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject.CyclicTasksIssuingTWD;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject
{
    // 定义结果记录，前端可以直接读取这个列表展示成功/失败及原因
    public record DeliveryResult(CyclicTaskModel Task, bool Success, string Message, DateTime Time);

    // 定义三个记录类型：StorageAreaRecord（包含 cargo 字段），AreaRecord（不包含 cargo 字段），以及它们的快照类型
    public record StorageAreaRecord(string WmsCode, string Cargo, DateTime RetrievedAt);

    // StorageAreaSnapshot 包含 StorageAreaRecord 列表和快照时间
    public record StorageAreaSnapshot(IReadOnlyList<StorageAreaRecord> Items, DateTime SnapshotTime);

    // ConveyorArea 和 SortingArea 只需要 WmsCode 和 RetrievedAt，因此使用 AreaRecord
    public record AreaRecord(string WmsCode, DateTime RetrievedAt);

    // ConveyorAreaSnapshot 和 SortingAreaSnapshot 都使用 AreaRecord 列表和快照时间
    public record AreaSnapshot(IReadOnlyList<AreaRecord> Items, DateTime SnapshotTime);

    public interface ICyclicTasksIssuing
    {
        // 读取 cargo_area_instances 并分成三个区域快照
        Task<(StorageAreaSnapshot StorageArea, AreaSnapshot ConveyorArea, AreaSnapshot SortingArea)> ReadCargoAreaInstancesAsync(CancellationToken ct = default);

        // 生成周期任务的主方法：根据读取的三个区域快照生成任务
        Task Thailand_TWD(int times);
    }

    public sealed class CyclicTasksIssuingTWD : ICyclicTasksIssuing
    {
        private readonly IRcsDbService _db;
        private readonly ILogger<CyclicTasksIssuingTWD>? _logger;
        private readonly IWcsTaskHttpService _wcsTaskHttpService;
        private readonly IAppMemoryStore _appMemoryStore; // 通用内存存储
        private const string SqlReadCargoAreaInstances = "SELECT cargo_area, wms_code, cargo FROM cargo_area_instances";

        // 用于内存写操作的同步
        private readonly object _memoryLock = new();

        // 构造函数注入数据库服务、HTTP 服务、内存存储和可选的日志服务
        public CyclicTasksIssuingTWD(IRcsDbService db, IWcsTaskHttpService wcsTaskHttpService, IAppMemoryStore appMemoryStore, ILogger<CyclicTasksIssuingTWD>? logger = null)
        {
            _db = db;
            _logger = logger;
            _wcsTaskHttpService = wcsTaskHttpService;
            _appMemoryStore = appMemoryStore;
        }

        // 读取 cargo_area_instances 表，按 cargo_area 字段分类到三个快照中
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

        // 生成周期任务的主方法：根据读取的三个区域快照生成任务
        public async Task Thailand_TWD(int times)
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
        // 任务下发后把任务结果（成功/失败）保存到通用内存存储，前端可从内存读取展示
        private async Task SendSequentialAsync(IEnumerable<CyclicTaskModel> tasks, CancellationToken ct = default)
        {
            if (tasks == null) return;

            foreach (var t in tasks)
            {
                ct.ThrowIfCancellationRequested();

                string resultMessage = string.Empty;
                bool success = false;

                try
                {
                    var (sendSuccess, message) = await _wcsTaskHttpService.SendTaskAsync(t, "TargetSystem", ct).ConfigureAwait(false);
                    success = sendSuccess;
                    resultMessage = message ?? string.Empty;

                    if (sendSuccess)
                        _logger?.LogInformation("任务下发成功 TaskNo={TaskNo}", t.TaskNo);
                    else
                        _logger?.LogWarning("任务下发失败 TaskNo={TaskNo} Msg={Msg}", t.TaskNo, message);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    success = false;
                    resultMessage = "已取消";
                    _logger?.LogWarning("任务下发被取消 TaskNo={TaskNo}", t.TaskNo);
                    throw;
                }
                catch (Exception ex)
                {
                    success = false;
                    resultMessage = ex.Message;
                    _logger?.LogError(ex, "任务下发异常 TaskNo={TaskNo}", t.TaskNo);
                }

                // 将本次发送结果追加到内存存储的结果列表（线程安全写入）
                var record = new DeliveryResult(t, success, resultMessage, DateTime.UtcNow);
                lock (_memoryLock)
                {
                    var list = _appMemoryStore.GetOrDefault<List<DeliveryResult>>() ?? new List<DeliveryResult>();
                    // 新 list 避免并发读写影响引用
                    var newList = new List<DeliveryResult>(list) { record };
                    _appMemoryStore.Set(newList);
                }

                // 可选短延迟，防止瞬时过载目标系统（按需调整或移除）
                await Task.Delay(50, ct).ConfigureAwait(false);
            }
        }
    }
}