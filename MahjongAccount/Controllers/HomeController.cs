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

    // ����û��Ƿ��¼
    private bool IsUserLoggedIn()
    {
        return HttpContext.Request.Cookies.Keys.Contains("SelectedUserId");
    }

    private int GetCurrentUserId()
    {
        return Convert.ToInt32(HttpContext.Request.Cookies["SelectedUserId"]);
    }

    // ��ҳ
    public async Task<IActionResult> Index()
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        // ��ȡ��ǰ�û���Ϣ
        var userId = GetCurrentUserId();
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
        {
            HttpContext.Response.Cookies.Delete("SelectedUserId");
            return RedirectToAction("UserSelect", "User");
        }

        // ׼����ҳ��ͼģ��
        var viewModel = new IndexViewModel
        {
            User = user
        };

        return View(viewModel);
    }

    // ��ȡ�����е��ƾ֣���ǰ��AJAX���ã�
    public async Task<IActionResult> OngoingGames()
    {
        if (!IsUserLoggedIn())
        {
            return Json(new { success = false, message = "����ѡ���û�" });
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
                    // �ų��Ѽ�����ƾֵ��û�
                    can_join = !g.GamePlayers.Any(gp => gp.UserId == GetCurrentUserId())
                })
                .OrderByDescending(g => g.created_at)
                .ToListAsync();

            return Json(ongoingGames);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = "�����ƾ�ʧ�ܣ�" + ex.Message });
        }
    }

    // ��ʷ��¼ҳ - ������
    public async Task<IActionResult> History(DateTime? startDate = null, DateTime? endDate = null)
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        var userId = GetCurrentUserId();

        // ��ȡ��ǰ�û���Ϣ
        var currentUser = await _context.Users.FindAsync(userId);

        // ��ȡ��ǰ�û�����������ƾ�ID
        var gameIdsQuery = _context.GamePlayers
            .Where(gp => gp.UserId == userId)
            .Select(gp => gp.GameId);

        // �����ƾֲ�ѯ
        var gamesQuery = _context.Games
            .Where(g => gameIdsQuery.Contains(g.Id));

        // Ӧ������ɸѡ
        if (startDate.HasValue)
        {
            gamesQuery = gamesQuery.Where(g => g.CreatedAt >= startDate.Value);
        }
        if (endDate.HasValue)
        {
            // �������ڰ�����������м�¼
            var endDateWithTime = endDate.Value.Date.AddDays(1).AddTicks(-1);
            gamesQuery = gamesQuery.Where(g => g.CreatedAt <= endDateWithTime);
        }

        // ִ�в�ѯ������
        var games = await gamesQuery
            .Include(g => g.GameResults)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        // ����ÿ����Ϸ����ȡ��ǰ�û��Ľ��
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

        var historyViewModel = new HistoryViewModel
        {
            CurrentUser = currentUser,
            TotalGames = historyItems.Count,
            TotalNetResult = historyItems.Sum(item => item.UserNetResult),
            TotalWin = historyItems.Count(item => item.UserNetResult > 0),
            TotalLose = historyItems.Count(item => item.UserNetResult < 0),
            StartDate = startDate,
            EndDate = endDate,
            HistoryItems = historyItems
        };

        return View(historyViewModel);
    }

    // ���а�ҳ
    public async Task<IActionResult> Rankings(string period = "total", string type = "amount", string extreme = "day")
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        var userId = GetCurrentUserId();
        // ׼��ʱ��ɸѡ����
        DateTime? startDate = null;
        if (period == "month")
        {
            // ���µ�һ��
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        }
        else if (period == "year")
        {
            // �����һ��
            startDate = new DateTime(DateTime.Now.Year, 1, 1);
        }

        // ��������������
        var rankings = type == "amount"
            ? await GetAmountRankings(startDate, userId)
            : await GetTimesRankings(startDate, userId);
        // ��ȡ��ǰ�û���������Ϣ
        var currentUserRanking = rankings.FirstOrDefault(r => r.UserId == userId);

        //// ��ֵ�����ݣ�����/���£�
        var topExtremes = extreme == "day"
            ? await GetTopDailyExtremes(userId)
            : await GetTopMonthlyExtremes(userId);
        var bottomExtremes = extreme == "day"
            ? await GetBottomDailyExtremes(userId)
            :  await GetBottomMonthlyExtremes(userId);

        // ������ͼģ��
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

    // ��ȡ������а�����
    private async Task<List<UserRankingDto>> GetAmountRankings(DateTime? startDate, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)  // ��ʽ����Game��������
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .Where(x => !startDate.HasValue || x.gr.Game.CreatedAt >= startDate)
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.AvatarUrl })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                AvatarUrl = g.Key.AvatarUrl,
                TotalPoints = g.Sum(x => x.gr.NetResult),
                GameCount = g.Select(x => x.gr.GameId).Distinct().Count()  // ���Ӳ������
            })
            .OrderByDescending(x => x.TotalPoints)
            .Take(10)
            .ToListAsync<UserRankingDto>();
    }

    // ��ȡ�������а�����
    private async Task<List<UserRankingDto>> GetTimesRankings(DateTime? startDate, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)  // ��ʽ����Game��������
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .Where(x => !startDate.HasValue || x.gr.Game.CreatedAt >= startDate)
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.AvatarUrl })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                AvatarUrl = g.Key.AvatarUrl,
                GameCount = g.Count(x => x.gr.NetResult > 0)
            })
            .OrderByDescending(x => x.GameCount)
            .Take(10)
            .ToListAsync<UserRankingDto>();
    }

    // ��ȡ����Ӯ�������
    private async Task<List<ExtremeDto>> GetTopDailyExtremes(int userId)
    {
        return await GetDailyExtremes(true, userId);
    }

    // ��ȡ�������������
    private async Task<List<ExtremeDto>> GetBottomDailyExtremes(int userId)
    {
        return await GetDailyExtremes(false, userId);
    }

    // ͨ�õ��ռ�ֵ��ѯ
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
                x.user.AvatarUrl,
                Date = x.gr.Game.CreatedAt.Date  // �����ڷ���
            })
            .Where(g => isTop
                ? g.Sum(x => x.gr.NetResult) > 0
                : g.Sum(x => x.gr.NetResult) < 0)
            .Select(g => new ExtremeDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                AvatarUrl = g.Key.AvatarUrl,
                Period = g.Key.Date.ToString("yyyy-MM-dd"),
                TotalResult = g.Sum(x => x.gr.NetResult)
            })
            .OrderByDescending(x => isTop ? x.TotalResult : -x.TotalResult)
            .Take(10)
            .ToListAsync<ExtremeDto>();
    }

    // ��ȡ����Ӯ�������
    private async Task<List<ExtremeDto>> GetTopMonthlyExtremes(int userId)
    {
        return await GetMonthlyExtremes(true, userId);
    }

    // ��ȡ�������������
    private async Task<List<ExtremeDto>> GetBottomMonthlyExtremes(int userId)
    {
        return await GetMonthlyExtremes(false, userId);
    }

    // ͨ�õ��¼�ֵ��ѯ
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
                x.user.AvatarUrl,
                Month = new DateTime(x.gr.Game.CreatedAt.Year, x.gr.Game.CreatedAt.Month, 1)  // ���·ݷ���
            })
            .Where(g => isTop
                ? g.Sum(x => x.gr.NetResult) > 0
                : g.Sum(x => x.gr.NetResult) < 0)
            .Select(g => new ExtremeDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                AvatarUrl = g.Key.AvatarUrl,
                Period = g.Key.Month.ToString("yyyy-MM"),
                TotalResult = g.Sum(x => x.gr.NetResult)
            })
            .OrderByDescending(x => isTop ? x.TotalResult : -x.TotalResult)
            .Take(10)
            .ToListAsync<ExtremeDto>();
    }

    public IActionResult SimpleError(string message, string title = "")
    {
        ViewData["title"] = title;
        ViewData["message"] = message;

        return View();
    }

    // �˳���¼/�л��û�
    public IActionResult Logout()
    {
        // ���Cookies�е��û�ѡ��
        HttpContext.Response.Cookies.Delete("SelectedUserId");
        return RedirectToAction("UserSelect", "User");
    }
}