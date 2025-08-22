using MahjongAccount.Data;
using MahjongAccount.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 添加数据库上下文
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=data/database.db"));

// 添加SignalR
builder.Services.AddSignalR(options =>
{
    // 客户端超时时间（默认30秒，建议延长至5分钟）
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(8);
    // 心跳包间隔（应小于客户端超时时间，建议2分钟）
    options.KeepAliveInterval = TimeSpan.FromMinutes(4);
    // 握手超时时间（默认15秒，复杂环境可延长）
    options.HandshakeTimeout = TimeSpan.FromMinutes(20);
});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
}

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

app.Run();
