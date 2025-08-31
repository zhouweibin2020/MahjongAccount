using MahjongAccount.Data;
using MahjongAccount.Hubs;
using MahjongAccount.Models.Dtos;
using Microsoft.EntityFrameworkCore;
using Serilog;

// 配置Serilog日志
var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "Logs");
if (!Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .Enrich.FromLogContext()
    .WriteTo.File(
        Path.Combine(logDirectory, "mahjong-.log"),
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// 配置日志
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

builder.Services.AddHttpClient();
builder.Services.Configure<HomeAssistant>(builder.Configuration.GetSection("HomeAssistant"));

// 添加数据库上下文 - MySQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("MySqlConnection"),
        new MySqlServerVersion(new Version(8, 0, 23))
    )
    .EnableSensitiveDataLogging(false) // 生产环境禁用敏感数据日志
    .EnableDetailedErrors(false) // 生产环境禁用详细错误
);

// 添加SignalR
builder.Services.AddSignalR(options =>
{
    // 客户端超时时间（默认8分钟）
    var clientTimeout = TimeSpan.FromMinutes(8);
    if (TimeSpan.TryParse(builder.Configuration["SignalR:ClientTimeoutInterval"], out var configClientTimeout))
    {
        clientTimeout = configClientTimeout;
    }
    options.ClientTimeoutInterval = clientTimeout;

    // 心跳包间隔（默认4分钟）
    var keepAliveInterval = TimeSpan.FromMinutes(4);
    if (TimeSpan.TryParse(builder.Configuration["SignalR:KeepAliveInterval"], out var configKeepAlive))
    {
        keepAliveInterval = configKeepAlive;
    }
    options.KeepAliveInterval = keepAliveInterval;

    // 握手超时时间（默认20分钟）
    var handshakeTimeout = TimeSpan.FromMinutes(20);
    if (TimeSpan.TryParse(builder.Configuration["SignalR:HandshakeTimeout"], out var configHandshake))
    {
        handshakeTimeout = configHandshake;
    }
    options.HandshakeTimeout = handshakeTimeout;
});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// 启用 WebSocket 中间件（需在 UseRouting 之前）
app.UseWebSockets();
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR路由
app.MapHub<GameHub>("/gameHub");

// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // 确保数据库已创建
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();
