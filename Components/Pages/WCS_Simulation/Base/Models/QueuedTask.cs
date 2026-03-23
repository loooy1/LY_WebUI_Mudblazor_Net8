namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models
{
    // 队列任务模型（待发任务）
    public sealed class QueuedTask
    {
        public string TaskNo { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public string CarrierCode { get; set; } = string.Empty;
        public string SourceLocation { get; set; } = string.Empty;
        public string TargetLocation { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int Quantity { get; set; }
        public bool IsUrgent { get; set; }
        public string Status { get; set; } = "待发送";
        public DateTime CreatedTime { get; set; }
    }

}
