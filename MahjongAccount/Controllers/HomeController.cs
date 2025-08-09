using MahjongAccount.Data;
using MahjongAccount.Models;
using MahjongAccount.Models.Dtos;
using MahjongAccount.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    // 检查用户是否已选择（通过Session验证，与UserController保持一致）
    private bool IsUserLoggedIn()
    {
        return HttpContext.Session.GetInt32("SelectedUserId") != null;
    }

    // 获取当前选中的用户ID
    private int GetCurrentUserId()
    {
        return HttpContext.Session.GetInt32("SelectedUserId") ?? 0;
    }

    // 首页
    public async Task<IActionResult> Index()
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        // 获取当前用户信息
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            HttpContext.Session.Remove("SelectedUserId");
            return RedirectToAction("UserSelect", "User");
        }

        // 准备首页视图模型
        var viewModel = new IndexViewModel
        {
            User = user
        };

        return View(viewModel);
    }

    // 获取进行中的牌局（供前端AJAX调用）
    public async Task<IActionResult> OngoingGames()
    {
        if (!IsUserLoggedIn())
        {
            return Json(new { success = false, message = "请先选择用户" });
        }

        try
        {
            var ongoingGames = await _context.Games
                .Where(g => g.Status == "ongoing")
                .Include(g => g.Creator)
                .Include(g => g.GamePlayers)
                .Select(g => new
                {
                    id = g.Id,
                    creator_name = g.Creator.Nickname,
                    created_at = g.CreatedAt,
                    player_count = g.GamePlayers.Count,
                    // 排除已加入该牌局的用户
                    can_join = !g.GamePlayers.Any(gp => gp.UserId == GetCurrentUserId())
                })
                .OrderByDescending(g => g.created_at)
                .ToListAsync();

            return Json(ongoingGames);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "加载牌局失败：" + ex.Message });
        }
    }

    // 历史记录页 - 主方法
    public async Task<IActionResult> History(DateTime? startDate = null, DateTime? endDate = null)
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        var userId = GetCurrentUserId();

        // 获取当前用户信息
        var currentUser = await _context.Users.FindAsync(userId);
        ViewData["CurrentUser"] = currentUser;

        // 获取当前用户参与的所有牌局ID
        var gameIdsQuery = _context.GamePlayers
            .Where(gp => gp.UserId == userId)
            .Select(gp => gp.GameId);

        // 构建牌局查询
        var gamesQuery = _context.Games
            .Where(g => gameIdsQuery.Contains(g.Id));

        // 应用日期筛选
        if (startDate.HasValue)
        {
            gamesQuery = gamesQuery.Where(g => g.CreatedAt >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            // 结束日期包含当天的所有记录
            var endDateWithTime = endDate.Value.Date.AddDays(1).AddTicks(-1);
            gamesQuery = gamesQuery.Where(g => g.CreatedAt <= endDateWithTime);
        }

        // 执行查询并排序
        var games = await gamesQuery
            .Include(g => g.GameResults)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        // 处理每个游戏，获取当前用户的结果
        var historyItems = games.Select(game =>
        {
            var userResult = game.GameResults.FirstOrDefault(gr => gr.UserId == userId);

            return new HistoryItemViewModel
            {
                GameId = game.Id,
                CreatedAt = game.CreatedAt,
                EndedAt = game.EndedAt,
                Status = game.Status,
                UserNetResult = userResult?.NetResult ?? 0,
                UserTotalWin = userResult?.TotalWin ?? 0,
                UserTotalLose = userResult?.TotalLose ?? 0
            };
        }).ToList();

        // 传递筛选参数到视图
        ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
        ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");

        // 计算总计
        ViewData["TotalGames"] = historyItems.Count;
        ViewData["TotalNetResult"] = historyItems.Sum(item => item.UserNetResult);
        ViewData["TotalWin"] = historyItems.Sum(item => item.UserTotalWin);
        ViewData["TotalLose"] = historyItems.Sum(item => item.UserTotalLose);

        return View(historyItems);
    }

    // 排行榜页
    public async Task<IActionResult> Rankings(string period = "total", string type = "amount", string extreme = "day")
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        var userId = GetCurrentUserId();
        // 准备时间筛选条件
        DateTime? startDate = null;
        if (period == "month")
        {
            // 当月第一天
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }
        else if (period == "year")
        {
            // 当年第一天
            startDate = new DateTime(DateTime.Now.Year, 1, 1);
        }

        // 金额榜或次数榜数据
        var rankings = type == "amount"
            ? await GetAmountRankings(startDate, userId)
            : await GetTimesRankings(startDate, userId);
        // 获取当前用户的排名信息
        var currentUserRanking = rankings.FirstOrDefault(r => r.UserId == userId);

        //// 极值榜数据（单日/单月）
        var topExtremes = extreme == "day"
            ? await GetTopDailyExtremes(userId)
            : await GetTopMonthlyExtremes(userId);
        var bottomExtremes = extreme == "day"
            ? await GetBottomDailyExtremes(userId)
            :  await GetBottomMonthlyExtremes(userId);

        // 构建视图模型
        var viewModel = new RankingsViewModel
        {
            Period = period,
            RankType = type,
            ExtremeType = extreme,
            Rankings = rankings,
            TopExtremes = topExtremes,
            BottomExtremes = bottomExtremes
        };

        return View(viewModel);
    }

    // 获取金额排行榜数据
    private async Task<List<UserRankingDto>> GetAmountRankings(DateTime? startDate, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)  // 显式加载Game导航属性
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .Where(x => !startDate.HasValue || x.gr.Game.CreatedAt >= startDate)
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.Avatar })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                Avatar = g.Key.Avatar,
                TotalPoints = g.Sum(x => x.gr.NetResult),
                GameCount = g.Select(x => x.gr.GameId).Distinct().Count()  // 增加参与局数
            })
            .OrderByDescending(x => x.TotalPoints)
            .Take(10)
            .ToListAsync<UserRankingDto>();
    }

    // 获取次数排行榜数据
    private async Task<List<UserRankingDto>> GetTimesRankings(DateTime? startDate, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)  // 显式加载Game导航属性
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .Where(x => !startDate.HasValue || x.gr.Game.CreatedAt >= startDate)
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.Avatar })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                Avatar = g.Key.Avatar,
                GameCount = g.Count(x => x.gr.NetResult > 0)
            })
            .OrderByDescending(x => x.GameCount)
            .Take(10)
            .ToListAsync<UserRankingDto>();
    }

    // 获取单日赢最多数据
    private async Task<List<ExtremeDto>> GetTopDailyExtremes(int userId)
    {
        return await GetDailyExtremes(true, userId);
    }

    // 获取单日输最多数据
    private async Task<List<ExtremeDto>> GetBottomDailyExtremes(int userId)
    {
        return await GetDailyExtremes(false, userId);
    }

    // 通用单日极值查询
    private async Task<List<ExtremeDto>> GetDailyExtremes(bool isTop, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .GroupBy(x => new
            {
                x.user.Id,
                x.user.Nickname,
                x.user.Avatar,
                Date = x.gr.Game.CreatedAt.Date  // 按日期分组
            })
            .Where(g => isTop
                ? g.Sum(x => x.gr.NetResult) > 0
                : g.Sum(x => x.gr.NetResult) < 0)
            .Select(g => new ExtremeDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                Avatar = g.Key.Avatar,
                Period = g.Key.Date.ToString("yyyy-MM-dd"),
                TotalResult = g.Sum(x => x.gr.NetResult)
            })
            .OrderByDescending(x => isTop ? x.TotalResult : -x.TotalResult)
            .Take(10)
            .ToListAsync<ExtremeDto>();
    }

    // 获取单月赢最多数据
    private async Task<List<ExtremeDto>> GetTopMonthlyExtremes(int userId)
    {
        return await GetMonthlyExtremes(true, userId);
    }

    // 获取单月输最多数据
    private async Task<List<ExtremeDto>> GetBottomMonthlyExtremes(int userId)
    {
        return await GetMonthlyExtremes(false, userId);
    }

    // 通用单月极值查询
    private async Task<List<ExtremeDto>> GetMonthlyExtremes(bool isTop, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .GroupBy(x => new
            {
                x.user.Id,
                x.user.Nickname,
                x.user.Avatar,
                Month = new DateTime(x.gr.Game.CreatedAt.Year, x.gr.Game.CreatedAt.Month, 1)  // 按月份分组
            })
            .Where(g => isTop
                ? g.Sum(x => x.gr.NetResult) > 0
                : g.Sum(x => x.gr.NetResult) < 0)
            .Select(g => new ExtremeDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                Avatar = g.Key.Avatar,
                Period = g.Key.Month.ToString("yyyy-MM"),
                TotalResult = g.Sum(x => x.gr.NetResult)
            })
            .OrderByDescending(x => isTop ? x.TotalResult : -x.TotalResult)
            .Take(10)
            .ToListAsync<ExtremeDto>();
    }

    // 退出登录/切换用户（与Session机制匹配）
    public IActionResult Logout()
    {
        // 清除Session中的用户选择
        HttpContext.Session.Remove("SelectedUserId");
        return RedirectToAction("UserSelect", "User");
    }
}

// 首页视图模型
public class IndexViewModel
{
    public User User { get; set; }
}
