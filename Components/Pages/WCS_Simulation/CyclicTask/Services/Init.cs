using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Shared.Services; // 新增
using Microsoft.Extensions.Logging;
using MySqlConnector;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services
{

    //rcs数据库中货物实例的统计结果
    public record StorageCounts(int TotalTrays, int TotalCargo);

    public interface ICyclicTasksIssuing
    {
        // 读取 cargo_area_instances 并分成三个区域快照
        Task<(StorageAreaSnapshot StorageArea, AreaSnapshot ConveyorArea, AreaSnapshot SortingArea)> ReadCargoAreaInstancesAsync(CancellationToken ct = default);

        // 计算托盘/货物数量并保存到内存
        Task<(int totalCargoContainers,int totalCargo)> CalculateInventory();
    }

    public sealed class CyclicTasksIssuing : ICyclicTasksIssuing
    {
        private readonly IRcsDbService _db;
        private readonly ILogger<CyclicTasksIssuing>? _logger;
        private const string SqlReadCargoAreaInstances = "SELECT cargo_area, wms_code, cargo FROM cargo_area_instances";


        // 构造函数注入数据库服务、HTTP 服务、内存存储和可选的日志服务
        public CyclicTasksIssuing(IRcsDbService db, IWcsTaskHttpService wcsTaskHttpService, IAppMemoryStore appMemoryStore, ILogger<CyclicTasksIssuing>? logger = null)
        {
            _db = db;
            _logger = logger;
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

        // 计算托盘/货物数量并保存到内存（仅保存总数）
        public async Task<(int totalCargoContainers,int totalCargo)> CalculateInventory()
        {

            try
            {
                var (storageArea, conveyorArea, sortingArea) = await ReadCargoAreaInstancesAsync();
                var storages = storageArea.Items.ToArray();

                var totalCargoContainers = storages?.Length ?? 0;
                var totalCargo = storages?.Count(s => s.Cargo != null && s.Cargo.Contains("Cargo", StringComparison.OrdinalIgnoreCase)) ?? 0;

                return (totalCargoContainers, totalCargo);

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "保存托盘/货物数量到内存失败");
                return (0, 0);
            }


        }
    }
}