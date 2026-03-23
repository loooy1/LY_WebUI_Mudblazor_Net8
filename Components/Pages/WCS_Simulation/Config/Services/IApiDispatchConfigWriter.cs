using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Services
{
    public interface IApiDispatchConfigWriter
    {
        void Save(ApiDispatchConfig config);
    }
}
