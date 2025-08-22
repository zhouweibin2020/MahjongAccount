using MahjongAccount.Data;
using MahjongAccount.Models.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace MahjongAccount.Hubs
{
    public class GameHub : Hub
    {
        private readonly AppDbContext _context;
        private readonly ILogger<GameHub> _logger; // 日志服务
        // 线程锁对象，用于同步静态列表的访问
        private static readonly object _lock = new object();
        // 静态列表存储连接信息
        private static List<HubConnectionDto> _hHubConnections = new();

        // 构造函数注入日志服务
        public GameHub(AppDbContext context, ILogger<GameHub> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 加入牌局房间（线程安全版）
        /// </summary>
        public async Task JoinGame(int gameId, int userId)
        {
            var connectionId = Context.ConnectionId;

            try
            {
                // 所有对静态列表的操作都需要加锁
                lock (_lock)
                {
                    // 查找用户已有的连接
                    var connection = _hHubConnections.FirstOrDefault(c => c.UserId == userId);

                    if (connection != null)
                    {
                        // 如果用户已有连接，先从原房间移除
                        _ = Groups.RemoveFromGroupAsync(connection.ConnectionId, $"Game_{connection.GameId}");
                        // 更新连接信息
                        connection.GameId = gameId;
                        connection.ConnectionId = connectionId;
                    }
                    else
                    {
                        // 添加新连接
                        _hHubConnections.Add(new HubConnectionDto
                        {
                            GameId = gameId,
                            UserId = userId,
                            ConnectionId = connectionId
                        });
                    }
                }

                // 加入新的房间分组
                await Groups.AddToGroupAsync(connectionId, $"Game_{gameId}");

                // 获取用户信息并通知其他用户
                var joinUser = await _context.Users.FindAsync(userId);
                var userName = joinUser?.Nickname ?? userId.ToString();

                await Clients.OthersInGroup($"Game_{gameId}").SendAsync(
                    "UserJoined",
                    new { GameId = gameId, userId, Message = $"用户 {userName} 加入了牌局" }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"用户加入牌局失败 - UserId: {userId}, GameId: {gameId}");
                throw; // 抛出异常让客户端处理
            }
        }

        /// <summary>
        /// 断开连接时清理（线程安全版）
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;
            HubConnectionDto? connection = null;

            try
            {
                // 加锁查找并移除连接
                lock (_lock)
                {
                    connection = _hHubConnections.FirstOrDefault(c => c.ConnectionId == connectionId);
                    if (connection != null)
                    {
                        _hHubConnections.Remove(connection);
                    }
                }

                // 如果找到连接，从房间中移除
                if (connection != null)
                {
                    await Groups.RemoveFromGroupAsync(connectionId, $"Game_{connection.GameId}");
                }

                if (exception != null)
                {
                    _logger.LogWarning(exception, $"连接异常断开 - ConnectionId: {connectionId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"断开连接时清理失败 - ConnectionId: {connectionId}");
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }
    }
}
