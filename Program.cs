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

// 最小：启用 Controllers（API）
builder.Services.AddControllers();

// MudBlazor 服务
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.VisibleStateDuration = 5000;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.MaxDisplayedSnackbars = 10;
    config.SnackbarConfiguration.RequireInteraction = false;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
    config.SnackbarConfiguration.ClearAfterNavigation = false;
});

// 注册其它服务
builder.Services.AddHttpClient<IWcsTaskHttpService, WcsTaskHttpService>(client =>
{
    client.BaseAddress = new Uri("https://your-target-system-host/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<IAppMemoryStore, AppMemoryStore>();
builder.Services.AddScoped<IRcsDbService, RcsDbService>();
builder.Services.AddScoped<ICyclicTasksIssuing, CyclicTasksIssuing>();
builder.Services.AddScoped<TWDproject>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// 正确的最小中间件顺序：
app.UseRouting();

// 如有认证： app.UseAuthentication();
app.UseAuthorization();

// 必须在 UseRouting() 后、MapControllers() 前调用 UseAntiforgery()
app.UseAntiforgery();

// 映射 Controllers（API）
app.MapControllers();


// 映射 Blazor 交互式组件
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
