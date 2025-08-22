using MahjongAccount.Data;
using MahjongAccount.Hubs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ������ݿ�������
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=data/database.db"));

// ���SignalR
builder.Services.AddSignalR(options =>
{
    // �ͻ��˳�ʱʱ�䣨Ĭ��30�룬�����ӳ���5���ӣ�
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(8);
    // �����������ӦС�ڿͻ��˳�ʱʱ�䣬����2���ӣ�
    options.KeepAliveInterval = TimeSpan.FromMinutes(4);
    // ���ֳ�ʱʱ�䣨Ĭ��15�룬���ӻ������ӳ���
    options.HandshakeTimeout = TimeSpan.FromMinutes(20);
});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ��ʼ�����ݿ�
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

// ���� WebSocket �м�������� UseRouting ֮ǰ��
app.UseWebSockets();
app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// SignalR·��
app.MapHub<GameHub>("/gameHub");

app.Run();
