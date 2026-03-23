using System.Text.Json.Serialization;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models
{

    /// <summary>
    /// 发送给外部系统的任务组请求
    /// </summary>
    public sealed class WcsTasksRequest
    {
        [JsonPropertyName("GroupId")]
        public string GroupId { get; set; } = string.Empty;

        [JsonPropertyName("MsgTime")]
        public DateTime MsgTime { get; set; }

        [JsonPropertyName("PriorityCode")]
        public int PriorityCode { get; set; }

        [JsonPropertyName("Warehouse")]
        public string Warehouse { get; set; } = string.Empty;

        [JsonPropertyName("Tasks")]
        public List<WcsTasksItem> Tasks { get; set; } = [];
    }

    public sealed class WcsTasksItem
    {
        [JsonPropertyName("TaskId")]
        public string TaskId { get; set; } = string.Empty;

        [JsonPropertyName("TaskType")]
        public string TaskType { get; set; } = string.Empty;

        [JsonPropertyName("ContainerCode")]
        public string ContainerCode { get; set; } = string.Empty;

        [JsonPropertyName("StationCode")]
        public string StationCode { get; set; } = string.Empty;

        [JsonPropertyName("AreaCode")]
        public string AreaCode { get; set; } = string.Empty;
    }
}
