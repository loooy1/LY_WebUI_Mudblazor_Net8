using LY_WebUI_Mudblazor_net8.Components;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.CyclicTask.Services.TWDproject;
using MudBlazor.Services;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//添加MudBlazor服务
builder.Services.AddMudServices();

//注册HTTP客户端服务
builder.Services.AddHttpClient<IWcsTaskHttpService, WcsTaskHttpService>(client =>
{
    client.BaseAddress = new Uri("https://your-target-system-host/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// 注册Config 读写服务，并把两个接口映射到同一个实例
builder.Services.AddScoped<ApiDispatchConfigStore>();
builder.Services.AddScoped<IApiDispatchConfigReader>(sp => sp.GetRequiredService<ApiDispatchConfigStore>());
builder.Services.AddScoped<IApiDispatchConfigWriter>(sp => sp.GetRequiredService<ApiDispatchConfigStore>());


// 注册CyclicTask 读写服务，并把两个接口映射到同一个实例
builder.Services.AddSingleton<CyclicConfigStore>();
builder.Services.AddSingleton<ICyclicConfigReader>(sp => sp.GetRequiredService<CyclicConfigStore>());
builder.Services.AddSingleton<ICyclicConfigWriter>(sp => sp.GetRequiredService<CyclicConfigStore>());

// 注册数据库访问服务（使用配置读取器）
builder.Services.AddScoped<IRcsDbService, RcsDbService>();

// 注册TWD项目循环任务下发服务
builder.Services.AddScoped<ICyclicTasksIssuing, CyclicTasksIssuingTWD>();

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
