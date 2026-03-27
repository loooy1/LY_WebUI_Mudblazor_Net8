using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services
{

    // 定义一个标记接口，所有您的任务类都需要实现它
    public interface ITaskData
    {
        public string TaskNo { get; set; }
        //任务类型
        public string TaskType { get; set; }
        //托盘号
        public string CarrierCode { get; set; }
        //起点
        public string SourceLocation { get; set; }
        //接驳点
        public string TransferLocation { get; set; }
        //终点
        public string TargetLocation { get; set; }
        //仓库
        public string Warehouse { get; set; }
        //优先级
        public int Priority { get; set; }

        //创建时间
        public DateTime CreatedTime { get; set; }
    }

    // 修改后的接口，使用泛型 T
    public interface IWcsTaskHttpService
    {
        /// <summary>
        /// 发送一个泛型任务到指定的目标系统
        /// </summary>
        /// <typeparam name="T">任务数据的类型，必须实现 ITaskData 接口</typeparam>
        /// <param name="taskData">包含任务信息的对象</param>
        /// <param name="targetSystem">目标系统名称</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回一个元组，包含操作是否成功和消息</returns>
        Task<(bool Success, string Message)> SendTaskAsync<T>(T taskData, string targetSystem, CancellationToken cancellationToken = default) where T : class, ITaskData;
    }
}
