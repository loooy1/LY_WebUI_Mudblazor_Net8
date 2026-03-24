namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Models
{
    public sealed class ApiDispatchConfig
    {
        public string BaseUrl { get; set; } = "http://127.0.0.1:8224/";
        public string DispatchPath { get; set; } = "api/v1/task_receive";
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
