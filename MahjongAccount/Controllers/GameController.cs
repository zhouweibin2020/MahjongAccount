using MahjongAccount.Data;
using MahjongAccount.Hubs;
using MahjongAccount.Models;
using MahjongAccount.Models.Dtos;
using MahjongAccount.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Controllers
{
    public class GameController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<GameController> _logger; // 日志服务

        public GameController(AppDbContext context, IHubContext<GameHub> hubContext, ILogger<GameController> logger)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        // 检查用户是否登录
        private bool IsUserLoggedIn()
        {
            return HttpContext.Request.Cookies.Keys.Contains("SelectedUserId");
        }

        private int GetCurrentUserId()
        {
            return Convert.ToInt32(HttpContext.Request.Cookies["SelectedUserId"]);
        }

        // 创建牌局
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(string name, string remarks, string type)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var creatorId = GetCurrentUserId();

            // 验证牌局类型（限制为预设值）
            var validTypes = new List<string> { "川麻", "宝中宝" };
            if (!validTypes.Contains(type))
            {
                ModelState.AddModelError("Type", "无效的牌局类型");
                return View();
            }

            try
            {
                var game = new Game
                {
                    Name = name,
                    Remarks = remarks,
                    CreatorId = creatorId,
                    Status = "ongoing",
                    BeginDirection = GetRandomDirection(), // 随机分配一个开局方位
                    CreatedAt = DateTime.Now,
                    Type = type
                };

                _context.Games.Add(game);
                await _context.SaveChangesAsync();

                // 添加创建者为参与者
                _context.GamePlayers.Add(new GamePlayer
                {
                    GameId = game.Id,
                    UserId = creatorId,
                    Direction = GetRandomDirection(), // 创建者随机分配一个方向
                    JoinedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                return RedirectToAction("Room", new { gameId = game.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建牌局失败 - 创建者ID: {CreatorId}", creatorId);
                ModelState.AddModelError(string.Empty, "创建牌局失败，请重试");
                return View();
            }
        }

        // 定义所有可能的方向
        private readonly List<string> _allDirections = new List<string> { "东", "西", "南", "北" };

        /// <summary>
        /// 从东西南北中随机选择一个未被选中的方向
        /// </summary>
        /// <param name="selectedDirections">已选的方向列表</param>
        /// <returns>随机选中的方向，如果所有方向都已被选则返回null</returns>
        public string GetRandomDirection(List<string> selectedDirections = null)
        {
            // 校验输入参数，避免空引用异常
            if (selectedDirections == null)
            {
                selectedDirections = new List<string>();
            }

            // 筛选出未被选中的方向
            var availableDirections = _allDirections
                .Where(d => !selectedDirections.Contains(d, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // 如果没有可用方向，返回null
            if (availableDirections.Count == 0)
            {
                return null;
            }

            // 随机选择一个可用方向
            var random = new Random();
            int index = random.Next(availableDirections.Count);
            return availableDirections[index];
        }

        // 获取进行中的牌局
        [HttpGet]
        public async Task<IActionResult> Ongoing()
        {
            if (!IsUserLoggedIn())
                return Json(new List<object>());

            try
            {
                var ongoingGames = await _context.Games
                    .Where(g => g.Status == "ongoing")
                    .Join(
                        _context.Users,
                        g => g.CreatorId,
                        u => u.Id,
                        (game, user) => new
                        {
                            Id = game.Id,
                            CreatorName = user.Nickname,
                            CreatedAt = game.CreatedAt
                        }
                    )
                    .OrderByDescending(g => g.CreatedAt)
                    .ToListAsync();

                return Json(ongoingGames);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取进行中牌局失败");
                return Json(new List<object>());
            }
        }

        // 加入牌局
        public async Task<IActionResult> Join(int gameId)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var userId = GetCurrentUserId();

            try
            {
                // 检查牌局是否存在且未结束
                var game = await _context.Games
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)                
                    return RedirectToAction("SimpleError", "Home", new { message = "牌局不存在" });
                
                // 检查是否已在牌局中
                var isAlreadyJoined = await _context.GamePlayers
                    .AnyAsync(gp => gp.GameId == gameId && gp.UserId == userId);

                if (isAlreadyJoined)
                    return RedirectToAction("Room", new { gameId });

                var selected = _context.GamePlayers.Where(f => f.GameId == gameId).Select(f => f.Direction).ToList();

                // 添加用户到牌局
                _context.GamePlayers.Add(new GamePlayer
                {
                    GameId = gameId,
                    UserId = userId,
                    Direction = GetRandomDirection(selected),
                    JoinedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                return RedirectToAction("Room", new { gameId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "用户加入牌局失败 - 牌局ID: {GameId}, 用户ID: {UserId}", gameId, userId);
                return RedirectToAction("SimpleError", "Home", new { message = "加入牌局失败" });
            }
        }

        // 牌局房间
        [HttpGet]
        public async Task<IActionResult> Room(int gameId)
        {
            try
            {
                // 验证牌局是否存在
                var game = await _context.Games
                    .FirstOrDefaultAsync(g => g.Id == gameId);

                if (game == null)
                    return RedirectToAction("SimpleError", "Home", new { message = "牌局不存在" });                

                // 获取当前用户ID
                var currentUserId = GetCurrentUserId();

                // 验证用户是否参与该牌局
                var isParticipant = await _context.GamePlayers
                    .AnyAsync(gp => gp.GameId == gameId && gp.UserId == currentUserId);

                if (!isParticipant)
                {
                    return RedirectToAction("Index", "Home", new { autoGoGame = false });
                }

                // 获取所有玩家信息 - 使用具体DTO
                var players = await _context.GamePlayers
                    .Where(gp => gp.GameId == gameId)
                    .Select(gp => new GamePlayerDto
                    {
                        Id = gp.UserId,
                        Nickname = gp.User.Nickname,
                        AvatarUrl = gp.User.AvatarUrl,
                        Direction = gp.Direction,
                        IsReady = gp.IsReady,
                        IsCreator = gp.UserId == game.CreatorId
                    })
                    .ToListAsync();

                // 获取所有交易记录 - 使用具体DTO
                var transactions = await _context.Transactions
                    .Where(t => t.GameId == gameId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new TransactionDto
                    {
                        Id = t.Id,
                        FromUserId = t.FromUserId,
                        FromNickname = t.FromUser.Nickname,
                        ToUserId = t.ToUserId,
                        ToNickname = t.ToUser.Nickname,
                        Amount = t.Amount,
                        CreatedAt = t.CreatedAt
                    })
                    .ToListAsync();

                // 计算玩家余额
                var playerBalances = CalculatePlayerBalances(players, transactions);

                // 准备视图模型
                var viewModel = new RoomViewModel
                {
                    Game = game,
                    Players = players,
                    Transactions = transactions,
                    PlayerBalances = playerBalances,
                    TotalPlayers = players.Count,
                    ReadyPlayers = players.Count(p => p.IsReady),
                    CurrentUserId = currentUserId,
                    CurrentUser = players.FirstOrDefault(p => p.Id == currentUserId)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载牌局房间失败 - 牌局ID: {GameId}", gameId);
                return RedirectToAction("SimpleError", "Home", new { message = "加载房间失败" });
            }
        }

        // 计算玩家余额的辅助方法
        private Dictionary<int, int> CalculatePlayerBalances(List<GamePlayerDto> players, List<TransactionDto> transactions)
        {
            var balances = new Dictionary<int, int>();
            players.ForEach(f => balances[f.Id] = 0);

            foreach (var t in transactions)
            {
                // 处理付款方
                balances[t.FromUserId] -= t.Amount;

                // 处理收款方
                balances[t.ToUserId] += t.Amount;
            }

            return balances;
        }

        // 记录交易
        [HttpPost]
        public async Task<IActionResult> RecordTransaction([FromBody] Transaction transaction)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            try
            {
                // 1. 保存交易记录
                _context.Transactions.Add(transaction);
                await _context.SaveChangesAsync();

                // 2. 通过SignalR通知房间内所有用户
                await _hubContext.Clients.Group($"Game_{transaction.GameId}").SendAsync("NewTransaction", transaction);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录交易失败 - 牌局ID: {GameId}, 交易: {TransactionId}", transaction.GameId, transaction.Id);
                return Json(new { success = false, message = "服务器处理失败" });
            }
        }

        // 准备/取消准备
        [HttpPost]
        public async Task<IActionResult> ToggleReady([FromBody] ToggleReadyDto input)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var userId = GetCurrentUserId();

            try
            {
                var gamePlayer = await _context.GamePlayers
                    .FirstOrDefaultAsync(gp => gp.GameId == input.GameId && gp.UserId == userId);

                if (gamePlayer == null)
                    return Json(new { success = false, message = "当前牌局查找失败" });

                // 更新准备状态
                gamePlayer.IsReady = input.IsReady;
                _context.GamePlayers.Update(gamePlayer);
                await _context.SaveChangesAsync();

                // 检查是否所有玩家都已准备
                var allPlayers = await _context.GamePlayers
                    .Where(gp => gp.GameId == input.GameId)
                    .ToListAsync();

                var totalPlayers = allPlayers.Count;
                var readyPlayers = allPlayers.Count(gp => gp.IsReady);

                // 所有玩家都准备好，结束牌局并计算结果
                if (totalPlayers > 0 && totalPlayers == readyPlayers)
                {
                    var game = await _context.Games.FindAsync(input.GameId);
                    game.Status = "ended";
                    game.EndedAt = DateTime.Now;
                    _context.Games.Update(game);

                    var gameResults = new List<GameResult>();
                    // 计算每个玩家的结果
                    foreach (var player in allPlayers)
                    {
                        var totalWin = await _context.Transactions
                            .Where(t => t.GameId == input.GameId && t.ToUserId == player.UserId)
                            .SumAsync(t => t.Amount);

                        var totalLose = await _context.Transactions
                            .Where(t => t.GameId == input.GameId && t.FromUserId == player.UserId)
                            .SumAsync(t => t.Amount);

                        var netResult = totalWin - totalLose;

                        gameResults.Add(new GameResult
                        {
                            GameId = input.GameId,
                            UserId = player.UserId,
                            TotalWin = totalWin,
                            TotalLose = totalLose,
                            NetResult = netResult
                        });
                    }
                    await _context.GameResults.AddRangeAsync(gameResults);

                    // 计算结算转账记录
                    var playersNet = gameResults.Select(gr => new { gr.UserId, gr.NetResult });

                    var winners = playersNet.Where(p => p.NetResult > 0).ToList();
                    var losers = playersNet.Where(p => p.NetResult < 0)
                        .Select(p => new LoserInfoDto { UserId = p.UserId, NeedPay = -p.NetResult })
                        .ToList();

                    foreach (var winner in winners)
                    {
                        var needReceive = winner.NetResult;
                        while (needReceive > 0 && losers.Any())
                        {
                            var currentLoser = losers[0];
                            var transferAmount = Math.Min(currentLoser.NeedPay, needReceive);

                            _context.SettlementTransactions.Add(new SettlementTransaction
                            {
                                GameId = input.GameId,
                                FromUserId = currentLoser.UserId,
                                ToUserId = winner.UserId,
                                Amount = transferAmount,
                                CreatedAt = DateTime.Now
                            });

                            currentLoser.NeedPay -= transferAmount;
                            needReceive -= transferAmount;

                            if (currentLoser.NeedPay == 0)
                                losers.RemoveAt(0);
                        }
                    }

                    await _context.SaveChangesAsync();
                }

                // 通知房间内用户
                await _hubContext.Clients.Group($"Game_{input.GameId}")
                    .SendAsync("ReadyStatusChanged",
                    new
                    {
                        GameId = input.GameId,
                        ReadyCount = readyPlayers,
                        TotalPlayers = totalPlayers,
                        UserId = userId
                    });

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "切换准备状态失败 - 牌局ID: {GameId}, 用户ID: {UserId}", input.GameId, userId);
                return Json(new { success = false, message = "操作失败，请重试" });
            }
        }

        // 牌局结果页
        public async Task<IActionResult> Result(int gameId)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var userId = GetCurrentUserId();

            try
            {
                // 获取牌局信息
                var game = await _context.Games
                    .FirstOrDefaultAsync(g => g.Id == gameId && g.Status == "ended");

                if (game == null)
                    return RedirectToAction("Join", "Game", new { gameId });

                // 当前用户结果
                var currentUserResultEntity = await _context.GameResults
                    .FirstOrDefaultAsync(gr => gr.GameId == gameId && gr.UserId == userId);

                var currentUserResult = currentUserResultEntity != null ? new GameResultDto
                {
                    TotalWin = currentUserResultEntity.TotalWin,
                    TotalLose = currentUserResultEntity.TotalLose,
                    NetResult = currentUserResultEntity.NetResult,
                    UserId = userId
                } : new GameResultDto();

                // 获取当前用户信息以完善DTO
                var currentUser = await _context.Users.FindAsync(userId);
                if (currentUser != null)
                {
                    currentUserResult.Nickname = currentUser.Nickname;
                    currentUserResult.AvatarUrl = currentUser.AvatarUrl;
                }

                // 所有玩家结果
                var results = await _context.GameResults
                    .Where(gr => gr.GameId == gameId)
                    .Join(
                        _context.Users,
                        gr => gr.UserId,
                        u => u.Id,
                        (gr, user) => new GameResultDto
                        {
                            TotalWin = gr.TotalWin,
                            TotalLose = gr.TotalLose,
                            NetResult = gr.NetResult,
                            Nickname = user.Nickname,
                            AvatarUrl = user.AvatarUrl,
                            UserId = user.Id
                        }
                    )
                    .ToListAsync();

                // 结算记录
                var settlementTransactions = await _context.SettlementTransactions
                    .Where(st => st.GameId == gameId)
                    .Join(
                        _context.Users,
                        st => st.FromUserId,
                        u => u.Id,
                        (st, fromUser) => new { st, fromUser }
                    )
                    .Join(
                        _context.Users,
                        tf => tf.st.ToUserId,
                        u => u.Id,
                        (tf, toUser) => new SettlementTransactionDto
                        {
                            Amount = tf.st.Amount,
                            FromNickname = tf.fromUser.Nickname,
                            ToNickname = toUser.Nickname,
                            FromAvatarUrl = tf.fromUser.AvatarUrl,
                            ToAvatarUrl = toUser.AvatarUrl,
                            FromUserId = tf.fromUser.Id,
                            ToUserId = toUser.Id
                        }
                    )
                    .ToListAsync();

                // 构建视图模型
                var viewModel = new ResultViewModel
                {
                    Game = game,
                    CurrentUserResult = currentUserResult,
                    Results = results,
                    Settlements = settlementTransactions
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载牌局结果失败 - 牌局ID: {GameId}, 用户ID: {UserId}", gameId, userId);
                return RedirectToAction("SimpleError", "Home", new { message = "加载结果失败" });
            }
        }
    }
}