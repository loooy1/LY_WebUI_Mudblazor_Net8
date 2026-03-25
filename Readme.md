
此项目是用MudBlazor组件的Blazor项目

## FAQ

1：防止主线程卡顿
```
@code {

	//按钮点击事件
	private async Task OnClick()
	{
		//火并忘记  启动异步任务但不等待
		_ = SendSingleInBackgroundAsync(task);

		//直接返回，继续执行其他操作
		return Task.CompletedTask;
	}

	//在后台线程中执行耗时操作
	 private async Task SendSingleInBackgroundAsync(QueuedTask task)
    {
            //在后台线程中执行耗时操作，避免阻塞UI线程
            var result = await TaskHttpService.SendTaskAsync(task, _config.TargetSystem);

			//强制切回主线程，确保线程安全
            await InvokeAsync(() =>
            {
                .....
            });
    }

}
```
2：http请求超时机制
```
(1):全局超时
	httpClient.Timeout = TimeSpan.FromSeconds(5);
	不能修改，所有http实例共用一个超时时间
(2):单独超时
	var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
	timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
	每个实例单独用一个超时时间，推荐





todo
读其他两个表 然后下发周期任务 并显示

学习 工作单元和efcore读写本身数据库



BUG
