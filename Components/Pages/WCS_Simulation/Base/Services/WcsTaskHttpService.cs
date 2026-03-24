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

                httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

                var requestUri = new Uri(new Uri(config.BaseUrl), config.DispatchPath);

                //拼接站点列表，过滤掉空值
                var stationCode = new[]
                {
                        task.SourceLocation,   // 起点
                        task.TransferLocation, // 接驳点
                        task.TargetLocation    // 终点
                }
                .Where(loc => !string.IsNullOrWhiteSpace(loc))
                .ToList();

                WcsTasksRequest requestTask = new WcsTasksRequest
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
                            StationCode = stationCode,
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

                var response = await httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return (true, "下发成功");

                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return (false, $"HTTP {(int)response.StatusCode}: {error}");
            }
            catch (TaskCanceledException)
            {
                return (false, "请求超时，请检查接口地址或目标系统状态");
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
