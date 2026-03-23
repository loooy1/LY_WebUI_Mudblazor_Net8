using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services
{
    public interface IWcsTaskHttpService
    {
        Task<(bool Success, string Message)> SendTaskAsync(QueuedTask task, string targetSystem, CancellationToken cancellationToken = default);
    }
}
