using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Services
{
    public sealed class ApiDispatchConfigStore : IApiDispatchConfigReader, IApiDispatchConfigWriter
    {
        private ApiDispatchConfig _current = new();

        public ApiDispatchConfig Get() => _current.Clone();

        public void Save(ApiDispatchConfig config) => _current = config.Clone();
    }
}
