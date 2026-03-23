using LY_WebUI_Mudblazor_net8.Components;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Base.Services;
using LY_WebUI_Mudblazor_net8.Components.Pages.WCS_Simulation.Config.Services;
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

//注册API调度配置服务
builder.Services.AddScoped<ApiDispatchConfigStore>();
builder.Services.AddScoped<IApiDispatchConfigReader>(sp => sp.GetRequiredService<ApiDispatchConfigStore>());
builder.Services.AddScoped<IApiDispatchConfigWriter>(sp => sp.GetRequiredService<ApiDispatchConfigStore>());

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
