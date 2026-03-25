using System.Net.Http.Headers;
using System.Net.Http.Json;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Services;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services
{
    public sealed class WcsTaskHttpService(
        HttpClient httpClient,
        IApiDispatchConfigReader configReader) : IWcsTaskHttpService
    {
        public async Task<(bool Success, string Message)> SendTaskAsync(
            QueuedTask task,
            string targetSystem,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var config = configReader.Get();
                var requestUri = new Uri(new Uri(config.BaseUrl), config.DispatchPath);

                var stationCode = new[]
                {
                    task.SourceLocation,
                    task.TransferLocation,
                    task.TargetLocation
                }
                .Where(loc => !string.IsNullOrWhiteSpace(loc))
                .ToList();

                var requestTask = new WcsTasksRequest
                {
                    GroupId = Guid.NewGuid().ToString(),
                    MsgTime = DateTime.UtcNow,
                    PriorityCode = task.Priority,
                    Warehouse = task.Warehouse,
                    Tasks = new List<WcsTasksItem>
                    {
                        new WcsTasksItem
                        {
                            TaskId = task.TaskNo,
                            TaskType = task.TaskType,
                            ContainerCode = task.CarrierCode,
                            StationCode = stationCode
                        }
                    }
                };

                using var request = new HttpRequestMessage(new HttpMethod(config.HttpMethod), requestUri)
                {
                    Content = JsonContent.Create(requestTask)
                };

                if (config.UseBearerToken && !string.IsNullOrWhiteSpace(config.BearerToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.BearerToken);
                }

                //超时控制
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                //发送http
                var response = await httpClient.SendAsync(request, timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                    return (true, "下发成功");

                var error = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                return (false, $"HTTP {(int)response.StatusCode}: {error}");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (false, "请求超时，请检查接口地址或目标系统状态");
            }
            catch (TaskCanceledException)
            {
                return (false, "请求已取消");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"网络请求失败：{ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"下发异常：{ex.Message}");
            }
        }
    }
}
