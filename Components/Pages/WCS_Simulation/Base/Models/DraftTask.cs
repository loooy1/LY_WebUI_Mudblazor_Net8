namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models
{
    // 任务草稿模型（任务下发模块的表单数据）
    public sealed class DraftTask
    {
        public string TaskNo { get; set; } = string.Empty;
        public string CarrierCode { get; set; } = string.Empty;
        public string SourceLocation { get; set; } = string.Empty;
        public string TransferLocation { get; set; } = string.Empty;
        public string TargetLocation { get; set; } = string.Empty;
        public int Priority { get; set; } = 3;
        public int Quantity { get; set; } = 1;
        public bool IsUrgent { get; set; }
    }
}
