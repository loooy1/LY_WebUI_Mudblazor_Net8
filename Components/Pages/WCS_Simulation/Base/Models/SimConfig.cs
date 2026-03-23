namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models
{
    // 系统配置模型（参数配置模块）
    public sealed class SimConfig
    {
        public string TargetSystem { get; set; } = string.Empty;
        public string DefaultTaskType { get; set; } = string.Empty;
        public int DefaultPriority { get; set; }
        public int RetryCount { get; set; }
        public int TimeoutSeconds { get; set; }
        public bool AutoAck { get; set; }
    }
}
