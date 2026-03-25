using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services
{
    public interface ICyclicConfigReader
    {
        RcsConnectionConfig Get();
    }
}