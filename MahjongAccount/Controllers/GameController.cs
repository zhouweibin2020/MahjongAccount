using MahjongAccount.Data;
using MahjongAccount.Hubs;
using MahjongAccount.Models;
using MahjongAccount.Models.Dtos;
using MahjongAccount.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace MahjongAccount.Controllers
{
    public class GameController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<GameHub> _hubContext;

        public GameController(AppDbContext context, IHubContext<GameHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 检查用户是否登录
        private bool IsUserLoggedIn()
        {
            return HttpContext.Session.GetInt32("SelectedUserId") != null;
        }

        private int GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("SelectedUserId") ?? 0;
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
        public async Task<IActionResult> Create(string name, string remarks)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var creatorId = GetCurrentUserId();
            var game = new Game
            {
                Name = name,
                Remarks = remarks,
                CreatorId = creatorId,
                Status = "ongoing",
                CreatedAt = DateTime.Now
            };

            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            // 添加创建者为参与者
            _context.GamePlayers.Add(new GamePlayer
            {
                GameId = game.Id,
                UserId = creatorId,
                JoinedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Room", new { gameId = game.Id });
        }

        // 获取进行中的牌局
        [HttpGet]
        public async Task<IActionResult> Ongoing()
        {
            if (!IsUserLoggedIn())
                return Json(new List<object>());

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

        // 加入牌局
        public async Task<IActionResult> Join(int gameId)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var userId = GetCurrentUserId();

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

            // 添加用户到牌局
            _context.GamePlayers.Add(new GamePlayer
            {
                GameId = gameId,
                UserId = userId,
                JoinedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Room", new { gameId });
        }

        // 牌局房间
        [HttpGet]
        public async Task<IActionResult> Room(int gameId)
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
                return RedirectToAction("Index", "Home");
            }

            // 获取所有玩家信息 - 使用具体DTO
            var players = await _context.GamePlayers
                .Where(gp => gp.GameId == gameId)
                .Select(gp => new GamePlayerDto
                {
                    Id = gp.UserId,
                    Nickname = gp.User.Nickname,
                    Avatar = gp.User.Avatar,
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
        
        // 牌局结果页
        public async Task<IActionResult> Result(int gameId)
        {
            if (!IsUserLoggedIn())
                return RedirectToAction("UserSelect", "User");

            var userId = GetCurrentUserId();

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
                currentUserResult.Avatar = currentUser.Avatar;
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
                        Avatar = user.Avatar,
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
                        FromAvatar = tf.fromUser.Avatar,
                        ToAvatar = toUser.Avatar,
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
    }
}