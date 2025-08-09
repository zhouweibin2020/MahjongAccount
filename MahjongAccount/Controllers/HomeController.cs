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

    // ����û��Ƿ���ѡ��ͨ��Session��֤����UserController����һ�£�
    private bool IsUserLoggedIn()
    {
        return HttpContext.Session.GetInt32("SelectedUserId") != null;
    }

    // ��ȡ��ǰѡ�е��û�ID
    private int GetCurrentUserId()
    {
        return HttpContext.Session.GetInt32("SelectedUserId") ?? 0;
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
            HttpContext.Session.Remove("SelectedUserId");
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
        ViewData["CurrentUser"] = currentUser;

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

        // ����ɸѡ��������ͼ
        ViewData["StartDate"] = startDate?.ToString("yyyy-MM-dd");
        ViewData["EndDate"] = endDate?.ToString("yyyy-MM-dd");

        // �����ܼ�
        ViewData["TotalGames"] = historyItems.Count;
        ViewData["TotalNetResult"] = historyItems.Sum(item => item.UserNetResult);
        ViewData["TotalWin"] = historyItems.Sum(item => item.UserTotalWin);
        ViewData["TotalLose"] = historyItems.Sum(item => item.UserTotalLose);

        return View(historyItems);
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
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.Avatar })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                Avatar = g.Key.Avatar,
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
                x.user.Avatar,
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
                Avatar = g.Key.Avatar,
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
                x.user.Avatar,
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
                Avatar = g.Key.Avatar,
                Period = g.Key.Month.ToString("yyyy-MM"),
                TotalResult = g.Sum(x => x.gr.NetResult)
            })
            .OrderByDescending(x => isTop ? x.TotalResult : -x.TotalResult)
            .Take(10)
            .ToListAsync<ExtremeDto>();
    }

    // �˳���¼/�л��û�����Session����ƥ�䣩
    public IActionResult Logout()
    {
        // ���Session�е��û�ѡ��
        HttpContext.Session.Remove("SelectedUserId");
        return RedirectToAction("UserSelect", "User");
    }
}

// ��ҳ��ͼģ��
public class IndexViewModel
{
    public User User { get; set; }
}
