using LY_WebUI_Mudblazor_net8.Components;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Shared.Services;
using MudBlazor;
using MudBlazor.Services;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 添加MudBlazor服务并统一配置全局 Snackbar 行为/外观
builder.Services.AddMudServices(config =>
{
    // 全局 Snackbar 配置（按需调整）
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight; // 位置
    config.SnackbarConfiguration.PreventDuplicates = true;                           // 防止重复消息
    config.SnackbarConfiguration.NewestOnTop = false;                               // 新消息是否置顶
    config.SnackbarConfiguration.ShowCloseIcon = true;                              // 显示关闭图标
    config.SnackbarConfiguration.VisibleStateDuration = 5000;                       // 可见时长（ms）
    config.SnackbarConfiguration.HideTransitionDuration = 500;                      // 隐藏过渡（ms）
    config.SnackbarConfiguration.ShowTransitionDuration = 500;                      // 显示过渡（ms）
    config.SnackbarConfiguration.MaxDisplayedSnackbars = 10;                        // 最大同时显示条数
    config.SnackbarConfiguration.RequireInteraction = false;                        // 是否需要用户交互（不自动消失）
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;                  // Variant.Filled(默认) Variant.Text (纯文字) 和 Variant.Outlined (边框样式)。
    config.SnackbarConfiguration.ClearAfterNavigation = false;                      // 导航后关闭
});

//注册HTTP客户端服务
builder.Services.AddHttpClient<IWcsTaskHttpService, WcsTaskHttpService>(client =>
{
    client.BaseAddress = new Uri("https://your-target-system-host/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

//注册全局内存读写服务
builder.Services.AddScoped<IAppMemoryStore, AppMemoryStore>();

// 注册数据库访问服务（使用配置读取器）
builder.Services.AddScoped<IRcsDbService, RcsDbService>();

// 注册循环任务通用服务
builder.Services.AddScoped<ICyclicTasksIssuing, CyclicTasksIssuing>();

//注册特定项目的循环任务服务
builder.Services.AddScoped<TWDproject>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
