using MahjongAccount.Data;
using MahjongAccount.Models.Dtos;
using MahjongAccount.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Globalization;
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

    // 检查用户是否登录
    private bool IsUserLoggedIn()
    {
        return HttpContext.Request.Cookies.Keys.Contains("SelectedUserId");
    }

    private int GetCurrentUserId()
    {
        return Convert.ToInt32(HttpContext.Request.Cookies["SelectedUserId"]);
    }

    /// <summary>
    /// 首页
    /// </summary>
    /// <param name="autoGoGame">是否自动进入牌局，默认：是</param>
    /// <returns></returns>
    public async Task<IActionResult> Index(bool autoGoGame = true)
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
            HttpContext.Response.Cookies.Delete("SelectedUserId");
            return RedirectToAction("UserSelect", "User");
        }

        if (autoGoGame)
        {
            // 获取最新的进行中牌局（仅显示一条）
            var ongoingGames = await _context.Games
                .Where(g => g.Status == "ongoing")
                .Include(g => g.GamePlayers)
                .Where(f => f.GamePlayers.Any(u => u.UserId.Equals(userId)))
                .OrderByDescending(g => g.CreatedAt)
                .FirstOrDefaultAsync();
            if (ongoingGames is not null)
            {
                return RedirectToAction("Room", "Game", new { gameId = ongoingGames.Id });
            }
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

    // 排行榜页
    public async Task<IActionResult> Rankings(string periodType = "total", string period = null, string type = "amount", string extreme = "day")
    {
        if (!IsUserLoggedIn())
        {
            return RedirectToAction("UserSelect", "User");
        }

        var userId = GetCurrentUserId();
        var years = await _context.Games
            .Select(g => g.CreatedAt.Year)
            .Distinct()
            .OrderByDescending(year => year)
            .Select(year => year.ToString())
            .ToArrayAsync();
        var yearMonths = await _context.Games
            .Select(g => g.CreatedAt.Year * 100 + g.CreatedAt.Month)
            .Distinct()
            .OrderByDescending(ym => ym)
            .Select(ym => ym.ToString())
            .ToArrayAsync();

        // 准备时间筛选条件
        DateTime? startDate = null;
        DateTime? endDate = null;
        if (periodType == "month")
        {
            if (string.IsNullOrEmpty(period))
            {
                // 当月第一天
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                period = startDate.Value.ToString("yyyyMM");
            }
            else
            {
                startDate = DateTime.ParseExact(period, "yyyyMM", CultureInfo.InvariantCulture);
            }
            endDate = startDate.Value.AddMonths(1).AddDays(-1);
        }
        else if (periodType == "year")
        {
            if (string.IsNullOrEmpty(period))
            {
                // 当年第一天
                startDate = new DateTime(DateTime.Now.Year, 1, 1);
                period = startDate.Value.ToString("yyyy");
            }
            else
            {
                startDate = new DateTime(Convert.ToInt32(period), 1, 1);
            }
            endDate = startDate.Value.AddYears(1).AddDays(-1);
        }

        // 金额榜或赢次榜数据
        var rankings = type == "amount"
            ? await GetAmountRankings(startDate, endDate, userId)
            : await GetTimesRankings(startDate, endDate, userId);

        //// 极值榜数据（单日/单月）
        var topExtremes = extreme == "day"
            ? await GetTopDailyExtremes(userId)
            : await GetTopMonthlyExtremes(userId);
        var bottomExtremes = extreme == "day"
            ? await GetBottomDailyExtremes(userId)
            : await GetBottomMonthlyExtremes(userId);

        // 构建视图模型
        var viewModel = new RankingsViewModel
        {
            PeriodType = periodType,
            Period = period,
            RankType = type,
            ExtremeType = extreme,
            Rankings = rankings,
            TopExtremes = topExtremes,
            BottomExtremes = bottomExtremes,
            Yaers = years,
            YearMonths = yearMonths
        };

        return View(viewModel);
    }

    // 获取金额排行榜数据
    private async Task<List<UserRankingDto>> GetAmountRankings(DateTime? startDate, DateTime? endDate, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)  // 显式加载Game导航属性
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .Where(x => (!startDate.HasValue || x.gr.Game.CreatedAt >= startDate) && (!endDate.HasValue || x.gr.Game.CreatedAt < endDate.Value.AddDays(1)))
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.AvatarUrl })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                AvatarUrl = g.Key.AvatarUrl,
                TotalPoints = g.Sum(x => x.gr.NetResult)               
            })
            .OrderByDescending(x => x.TotalPoints)
            .Take(10)
            .ToListAsync<UserRankingDto>();
    }

    // 获取次数排行榜数据
    private async Task<List<UserRankingDto>> GetTimesRankings(DateTime? startDate, DateTime? endDate, int userId)
    {
        return await _context.GameResults
            .Include(gr => gr.Game)  // 显式加载Game导航属性
            .Join(
                _context.Users,
                gr => gr.UserId,
                u => u.Id,
                (gr, user) => new { gr, user }
            )
            .Where(x => (!startDate.HasValue || x.gr.Game.CreatedAt >= startDate) && (!endDate.HasValue || x.gr.Game.CreatedAt < endDate.Value.AddDays(1)))
            .GroupBy(x => new { x.user.Id, x.user.Nickname, x.user.AvatarUrl })
            .Select(g => new UserRankingDto
            {
                UserId = g.Key.Id,
                IsCurrentUser = g.Key.Id == userId,
                Nickname = g.Key.Nickname,
                AvatarUrl = g.Key.AvatarUrl,
                TotalGameCount = g.Count(),
                WinGameCount = g.Count(x => x.gr.NetResult > 0)
            })
            .OrderByDescending(x => x.WinGameCount)
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
                x.user.AvatarUrl,
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
                AvatarUrl = g.Key.AvatarUrl,
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
                x.user.AvatarUrl,
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

    // 退出登录/切换用户
    public IActionResult Logout()
    {
        // 清除Cookies中的用户选择
        HttpContext.Response.Cookies.Delete("SelectedUserId");
        return RedirectToAction("UserSelect", "User");
    }

    /// <summary>
    /// 处理设备控制请求
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ControlDevice(string action)
    {
        // 获取客户端实际IP地址，考虑反向代理情况
        var clientIp = InternalHelper.GetClientIpAddress(HttpContext);

        // 判断是否为内网访问
        var isLocal = InternalHelper.IsInternalIpAddress(clientIp);

        if (!isLocal)
        {
            return Json(new { success = false, message = "仅允许内网访问此操作" });
        }
        try
        {
            if (string.IsNullOrEmpty(action))
            {
                return Json(new { success = false, message = "操作指令不能为空" });
            }

            // 调用Home Assistant API执行操作
            var success = await ExecuteHomeAssistantAction(action);

            return Json(new
            {
                success = success,
                message = success ? "操作成功" : "操作失败，请检查设备连接"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设备控制出错，操作: {Action}", action);
            return Json(new { success = false, message = "系统错误，请稍后重试" });
        }
    }

    /// <summary>
    /// 执行Home Assistant操作
    /// </summary>
    private async Task<bool> ExecuteHomeAssistantAction(string action)
    {
        // 映射操作到HA的服务和实体ID
        var (serviceUrl, entityIds) = action switch
        {
            //"trun_on_entrance_guard" => ("/api/services/switch/turn_on", new[] { "switch.giot_cn_888793280_v83ksm_on_p_3_1", "switch.giot_cn_888793280_v83ksm_on_p_4_1" }), // 开门禁实体ID
            //"open_door" => ("/api/services/button/press", new[] { "button.giot_cn_1007444492_v51ksm_all_open_a_15_1" }), // 开门实体ID
            "turn_on_mahjong_machine" => ("/api/services/switch/turn_on", new[] { "switch.cuco_cn_573061905_v3_on_p_2_1" }), // 麻将机开启实体ID
            "turn_off_mahjong_machine" => ("/api/services/switch/turn_off", new[] { "switch.cuco_cn_573061905_v3_on_p_2_1" }), // 麻将机关闭实体ID
            _ => (null, null)
        };

        // 验证映射结果
        if (string.IsNullOrEmpty(serviceUrl) || entityIds == null || !entityIds.Any())
        {
            _logger.LogWarning("未找到对应的设备操作映射: {Action}", action);
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
                    // 多个请求间隔500毫秒，避免请求过快被拒绝
                    await Task.Delay(500);
                }
                string? entityId = entityIds[i];
                // 构建请求内容
                var requestBody = new { entity_id = entityId };
                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                // 发送请求到Home Assistant
                var response = await client.PostAsync($"{_haConfigDto.BaseUrl}{serviceUrl}", content);
                response.EnsureSuccessStatusCode();
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "调用Home Assistant API失败，操作: {Action}", action);
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