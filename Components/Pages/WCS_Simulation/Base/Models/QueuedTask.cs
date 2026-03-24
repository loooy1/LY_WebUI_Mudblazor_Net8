namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models
{
    // 待发任务模型
    public sealed class QueuedTask
    {
        //任务id
        public string TaskNo { get; set; } = string.Empty;
        //任务类型
        public string TaskType { get; set; } = string.Empty;
        //托盘号
        public string CarrierCode { get; set; } = string.Empty;
        //起点
        public string SourceLocation { get; set; } = string.Empty;
        //接驳点
        public string TransferLocation { get; set; } = string.Empty;
        //终点
        public string TargetLocation { get; set; } = string.Empty;
        //仓库
        public string Warehouse { get; set; } = string.Empty;
        //优先级
        public int Priority { get; set; }
        //数量
        public int Quantity { get; set; }
        //是否紧急
        public bool IsUrgent { get; set; }
        //任务状态（待发送、发送中、已完成、发送失败等）
        public string Status { get; set; } = "待发送";
        //创建时间
        public DateTime CreatedTime { get; set; }
    }

}
