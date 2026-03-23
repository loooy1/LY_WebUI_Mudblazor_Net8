namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Models
{
    public sealed class ApiDispatchConfig
    {
        public string BaseUrl { get; set; } = "https://your-target-system-host/";
        public string DispatchPath { get; set; } = "api/tasks/dispatch";
        public string HttpMethod { get; set; } = "POST";
        public int TimeoutSeconds { get; set; } = 10;
        public bool UseBearerToken { get; set; }
        public string BearerToken { get; set; } = string.Empty;

        public ApiDispatchConfig Clone() => new()
        {
            BaseUrl = BaseUrl,
            DispatchPath = DispatchPath,
            HttpMethod = HttpMethod,
            TimeoutSeconds = TimeoutSeconds,
            UseBearerToken = UseBearerToken,
            BearerToken = BearerToken
        };
    }
}
