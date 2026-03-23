using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services
{
    public sealed class WcsTaskHttpService(HttpClient httpClient) : IWcsTaskHttpService
    {
        public async Task<(bool Success, string Message)> SendTaskAsync(
            QueuedTask task,
            string targetSystem,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                targetSystem,
                taskNo = task.TaskNo,
                taskType = task.TaskType,
                carrierCode = task.CarrierCode,
                sourceLocation = task.SourceLocation,
                targetLocation = task.TargetLocation,
                priority = task.Priority,
                quantity = task.Quantity,
                isUrgent = task.IsUrgent,
                createdTime = task.CreatedTime  
            };

            var response = await httpClient.PostAsJsonAsync("api/tasks/dispatch", payload, cancellationToken);

            if (response.IsSuccessStatusCode)
                return (true, "下发成功");

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return (false, $"HTTP {(int)response.StatusCode}: {error}");
        }
    }
}
