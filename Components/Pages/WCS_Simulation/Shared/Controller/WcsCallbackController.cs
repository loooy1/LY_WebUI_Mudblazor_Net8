using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Shared.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Shared.Controller
{
    [ApiController]
    [Route("api/task_stage_change")]
    [IgnoreAntiforgeryToken]
    public class WcsCallbackController : ControllerBase
    {
        private readonly IAppMemoryStore _appMemoryStore;
        private readonly ILogger<WcsCallbackController> _logger;
        private static readonly object _memoryLock = new();

        public WcsCallbackController(IAppMemoryStore appMemoryStore, ILogger<WcsCallbackController> logger)
        {
            _appMemoryStore = appMemoryStore;
            _logger = logger;
        }

        // 调试用：确认路由和控制器可达（GET）
        [HttpGet]
        public IActionResult GetAlive()
        {
            _logger.LogInformation("GET /api/task_stage_change -> alive");
            return Ok(new { Ok = true, Message = "WcsCallbackController alive" });
        }

        // 接收 RCS 回调（POST）
        [HttpPost]
        public IActionResult PostStatus([FromBody] JsonElement payload)
        {
            try
            {
                if (payload.ValueKind != JsonValueKind.Object)
                {
                    return BadRequest(new { MsgTime = DateTime.UtcNow.ToString("o"), Success = false, Message = "Invalid payload" });
                }

                // 解析入参（以单条 Task 字段为主）
                string? msgTime = payload.TryGetProperty("MsgTime", out var mt) && mt.ValueKind == JsonValueKind.String ? mt.GetString() : DateTime.UtcNow.ToString("o");
                string? taskId = payload.TryGetProperty("TaskId", out var tid) && tid.ValueKind == JsonValueKind.String ? tid.GetString() : null;
                string? warehouse = payload.TryGetProperty("Warehouse", out var wh) && wh.ValueKind == JsonValueKind.String ? wh.GetString() : null;
                string? stationCode = payload.TryGetProperty("StationCode", out var sc) && sc.ValueKind == JsonValueKind.String ? sc.GetString() : null;
                string? containerCode = payload.TryGetProperty("ContainerCode", out var cc) && cc.ValueKind == JsonValueKind.String ? cc.GetString() : null;
                string? stage = payload.TryGetProperty("Stage", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;
                string? incomingMessage = payload.TryGetProperty("Message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

                if (string.IsNullOrWhiteSpace(taskId))
                {
                    return BadRequest(new { MsgTime = msgTime, Success = false, Message = "Missing TaskId", Data = new { taskId, warehouse, stationCode, containerCode, stage } });
                }

                // 精确匹配 Stage 枚举字符串： START / LOAD_FINISH / FINISHED
                DeliveryStatus mappedStatus = DeliveryStatus.Unknown;
                if (!string.IsNullOrWhiteSpace(stage))
                {
                    switch (stage.Trim().ToUpperInvariant())
                    {
                        case "START":
                            mappedStatus = DeliveryStatus.InProgress; // rcs 已开始任务
                            break;
                        case "LOAD_FINISH":
                            mappedStatus = DeliveryStatus.LoadFinished; // rcs 取货完成
                            break;
                        case "FINISHED":
                            mappedStatus = DeliveryStatus.Done; // rcs 任务完成
                            break;
                        default:
                            var low = stage.Trim().ToLowerInvariant();
                            if (low.Contains("start") || low.Contains("开始"))
                                mappedStatus = DeliveryStatus.InProgress;
                            else if (low.Contains("load") || low.Contains("取货") || low.Contains("load_finish"))
                                mappedStatus = DeliveryStatus.LoadFinished;
                            else if (low.Contains("finish") || low.Contains("完成"))
                                mappedStatus = DeliveryStatus.Done;
                            else
                                mappedStatus = DeliveryStatus.Unknown;
                            break;
                    }
                }

                var updatedCount = 0;

                // 从内存读取最新数据（不要依赖注入时已有的本地缓存）
                lock (_memoryLock)
                {
                    var list = _appMemoryStore.GetOrDefault<List<DeliveryResult>>() ?? new List<DeliveryResult>();

                    if (list.Count == 0)
                    {
                        _logger.LogInformation("收到回调，但内存中没有 DeliveryResult 列表，TaskId={TaskId}", taskId);
                    }
                    else
                    {
                        // 使用副本更新并写回，保证并发安全和原子替换
                        var newList = new List<DeliveryResult>(list);
                        var idx = newList.FindLastIndex(dr => string.Equals(dr.Task.TaskNo, taskId, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            var old = newList[idx];
                            var newStatus = mappedStatus != DeliveryStatus.Unknown ? mappedStatus : old.Status;
                            var newMsg = string.IsNullOrWhiteSpace(incomingMessage) ? old.Message : incomingMessage;
                            var newRecord = old with { Status = newStatus, Message = newMsg, Time = DateTime.UtcNow };
                            newList[idx] = newRecord;
                            _appMemoryStore.Set(newList); // 写回最新列表
                            updatedCount = 1;
                            _logger.LogInformation("更新内存任务状态 TaskId={TaskId} OldStatus={OldStatus} NewStatus={NewStatus}", taskId, old.Status, newStatus);
                        }
                        else
                        {
                            _logger.LogInformation("未在内存中找到匹配 TaskId={TaskId}", taskId);
                        }
                    }
                }

                var resp = new
                {
                    MsgTime = msgTime,
                    Success = true,
                    Message = updatedCount > 0 ? $"已处理并更新 {updatedCount} 条任务状态" : "已接收，未在内存中找到匹配任务",
                    Data = new
                    {
                        TaskId = taskId,
                        Warehouse = warehouse,
                        StationCode = stationCode,
                        ContainerCode = containerCode,
                        Stage = stage
                    }
                };

                _logger.LogInformation("收到回调 TaskId={TaskId} Stage={Stage} MappedStatus={MappedStatus} Updated={Updated}", taskId, stage, mappedStatus, updatedCount);
                return Ok(resp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理 RCS 回调失败");
                return StatusCode(500, new { MsgTime = DateTime.UtcNow.ToString("o"), Success = false, Message = ex.Message });
            }
        }
    }
}