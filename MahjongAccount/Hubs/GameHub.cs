using Microsoft.AspNetCore.SignalR;
using MahjongAccount.Models;
using MahjongAccount.Models.Dtos;
using MahjongAccount.Data;
using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Hubs
{
    public class GameHub : Hub
    {
        private readonly AppDbContext _context;

        public GameHub(AppDbContext context)
        {
            _context = context;
        }

        // 存储“连接ID-用户ID”映射（用于识别连接的用户）
        private static readonly Dictionary<string, int> _connectionUsers = new();
        // 存储“牌局ID-连接组”映射（用于房间隔离）
        private static readonly Dictionary<int, HashSet<string>> _gameRooms = new();

        /// <summary>
        /// 加入牌局房间
        /// </summary>
        public async Task JoinGame(int gameId, int userId)
        {
            // 1. 记录连接与用户的关系
            var connectionId = Context.ConnectionId;
            _connectionUsers[connectionId] = userId;

            // 2. 将连接加入对应牌局的分组（实现房间隔离）
            if (!_gameRooms.ContainsKey(gameId))
            {
                _gameRooms[gameId] = new HashSet<string>();
            }
            _gameRooms[gameId].Add(connectionId);
            await Groups.AddToGroupAsync(connectionId, $"Game_{gameId}");

            var joinUser = await _context.Users.FindAsync(userId);

            // 3. 通知房间内其他用户“有新用户加入”
            await Clients.OthersInGroup($"Game_{gameId}").SendAsync(
                "UserJoined",
                new { GameId = gameId, Message = $"用户 {(joinUser?.Nickname ?? userId.ToString())} 加入了牌局" }
            );
        }

        /// <summary>
        /// 断开连接时清理
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var connectionId = Context.ConnectionId;

            // 1. 移除连接-用户映射
            if (_connectionUsers.TryGetValue(connectionId, out var userId))
            {
                _connectionUsers.Remove(connectionId);

                // 2. 移除牌局房间中的连接
                var gameIdToRemove = _gameRooms.FirstOrDefault(g => g.Value.Contains(connectionId)).Key;
                if (gameIdToRemove != 0)
                {
                    _gameRooms[gameIdToRemove].Remove(connectionId);
                    await Groups.RemoveFromGroupAsync(connectionId, $"Game_{gameIdToRemove}");

                    var leftUser = await _context.Users.FindAsync(userId);

                    // 3. 通知房间内其他用户
                    await Clients.OthersInGroup($"Game_{gameIdToRemove}").SendAsync(
                        "UserLeft",
                        new { GameId = gameIdToRemove, Message = $"用户 {(leftUser?.Nickname ?? userId.ToString())} 离开了牌局" }
                    );
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
