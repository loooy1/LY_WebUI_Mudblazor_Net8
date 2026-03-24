
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



todo 
新建一个界面 批量下发任务；连接数据库；获取库存； 任务显示界面有状态显示，任务完成后状态显示为完成；任务失败后状态显示为失败；任务正在执行状态显示为执行中；任务未开始状态显示为未开始；界面有刷新按钮，点击刷新按钮可以刷新任务列表和状态；界面有搜索功能，可以根据任务名称或者状态搜索任务；界面有分页功能，每页显示10条任务；界面有排序功能，可以根据任务名称或者状态排序任务；界面有导出功能，可以将任务列表导出为Excel文件；界面有删除功能，可以删除选中的任务；界面有编辑功能，可以编辑选中的任务；界面有新增功能，可以新增一个任务；界面有批量操作功能，可以批量删除或者批量编辑选中的任务。