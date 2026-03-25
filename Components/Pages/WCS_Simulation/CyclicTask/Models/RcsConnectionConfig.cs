namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models
{
    // 连接状态枚举，前端与后端共用
    public enum ConnState
    {
        Unknown,
        Testing,
        Connected,
        Disconnected
    }

    public sealed class RcsConnectionConfig
    {

        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 3306;
        public string Database { get; set; } = "rcs";
        public string User { get; set; } = "WCS_Simulation";
        public string Password { get; set; } = "WCS_Simulation123"; // 开发时可放此处，生产应使用更安全的存储
        public int ConnectTimeoutSeconds { get; set; } = 5;

        // 新增：把连接状态放到配置里以便保持
        public ConnState ConnectionState { get; set; } = ConnState.Unknown;

        // 可选：最后一次检测信息（前端显示）
        public string LastStatusMessage { get; set; } = string.Empty;
        public DateTime? LastCheckedUtc { get; set; }

        public RcsConnectionConfig Clone() => new()
        {
            Host = Host,
            Port = Port,
            Database = Database,
            User = User,
            Password = Password,
            ConnectTimeoutSeconds = ConnectTimeoutSeconds,
            ConnectionState = ConnectionState,
            LastStatusMessage = LastStatusMessage,
            LastCheckedUtc = LastCheckedUtc
        };
    }
}