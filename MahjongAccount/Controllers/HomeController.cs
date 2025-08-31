using MahjongAccount.Data;
using MahjongAccount.Models.Dtos;
using MahjongAccount.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace MahjongAccount.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HomeController> _logger;
    private readonly HomeAssistant _haConfigDto;

    public HomeController(AppDbContext context, IHttpClientFactory httpClientFactory, ILogger<HomeController> logger, IOptions<HomeAssistant> options)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _haConfigDto = options.Value;
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

    /// <summary>
    /// �����豸��������
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ControlDevice(string action)
    {
        // ��ȡ�ͻ���ʵ��IP��ַ�����Ƿ���������
        var clientIp = InternalHelper.GetClientIpAddress(HttpContext);

        // �ж��Ƿ�Ϊ��������
        var isLocal = InternalHelper.IsInternalIpAddress(clientIp);

        if (!isLocal)
        {
            return Json(new { success = false, message = "�������������ʴ˲���" });
        }
        try
        {
            if (string.IsNullOrEmpty(action))
            {
                return Json(new { success = false, message = "����ָ���Ϊ��" });
            }

            // ����Home Assistant APIִ�в���
            var success = await ExecuteHomeAssistantAction(action);

            return Json(new
            {
                success = success,
                message = success ? "�����ɹ�" : "����ʧ�ܣ������豸����"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "�豸���Ƴ�������: {Action}", action);
            return Json(new { success = false, message = "ϵͳ�������Ժ�����" });
        }
    }

    /// <summary>
    /// ִ��Home Assistant����
    /// </summary>
    private async Task<bool> ExecuteHomeAssistantAction(string action)
    {
        // ӳ�������HA�ķ����ʵ��ID
        var (serviceUrl, entityIds) = action switch
        {
            //"trun_on_entrance_guard" => ("/api/services/switch/turn_on", new[] { "switch.giot_cn_888793280_v83ksm_on_p_3_1", "switch.giot_cn_888793280_v83ksm_on_p_4_1" }), // ���Ž�ʵ��ID
            //"open_door" => ("/api/services/button/press", new[] { "button.giot_cn_1007444492_v51ksm_all_open_a_15_1" }), // ����ʵ��ID
            "turn_on_mahjong_machine" => ("/api/services/switch/turn_on", new[] { "switch.cuco_cn_573061905_v3_on_p_2_1" }), // �齫������ʵ��ID
            "turn_off_mahjong_machine" => ("/api/services/switch/turn_off", new[] { "switch.cuco_cn_573061905_v3_on_p_2_1" }), // �齫���ر�ʵ��ID
            _ => (null, null)
        };

        // ��֤ӳ����
        if (string.IsNullOrEmpty(serviceUrl) || entityIds == null || !entityIds.Any())
        {
            _logger.LogWarning("δ�ҵ���Ӧ���豸����ӳ��: {Action}", action);
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _haConfigDto.AccessToken);

            for (int i = 0; i < entityIds.Length; i++)
            {
                if (i > 0)
                {
                    // ���������500���룬����������챻�ܾ�
                    await Task.Delay(500);
                }
                string? entityId = entityIds[i];
                // ������������
                var requestBody = new { entity_id = entityId };
                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                // ��������Home Assistant
                var response = await client.PostAsync($"{_haConfigDto.BaseUrl}{serviceUrl}", content);
                response.EnsureSuccessStatusCode();
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "����Home Assistant APIʧ�ܣ�����: {Action}", action);
            return false;
        }
    }

    public IActionResult WeUIError(string title, string error = "")
    {
        ViewBag.Title = title;
        ViewBag.ErrorMessage = error;
        return View();
    }
}