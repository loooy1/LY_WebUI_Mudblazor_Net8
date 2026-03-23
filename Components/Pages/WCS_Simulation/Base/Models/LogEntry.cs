namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models
{
    // 日志条目模型
    public sealed class LogEntry
    {
        public DateTime Time { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
