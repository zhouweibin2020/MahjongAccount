using MahjongAccount.Data;
using MahjongAccount.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Controllers
{
    public class UserController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UserController> _logger; // 日志服务

        public UserController(AppDbContext context, ILogger<UserController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 用户选择页面
        public async Task<IActionResult> UserSelect()
        {
            // 获取客户端实际IP地址，考虑反向代理情况
            var clientIp = InternalHelper.GetClientIpAddress(HttpContext);

            // 判断是否为内网访问
            var isLocal = InternalHelper.IsInternalIpAddress(clientIp);

            // 获取所有用户列表
            var users = await _context.Users.ToListAsync();

            ViewBag.IsLocalNetwork = isLocal;

            return View(users);
        }
        
        // 选择用户后的处理，包含设备绑定随机码
        public async Task<IActionResult> ChooseUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var oldBindings = await _context.UserDeviceBindings.Where(b => b.UserId == userId).ToListAsync();

            // 获取客户端实际IP地址，考虑反向代理情况
            var clientIp = InternalHelper.GetClientIpAddress(HttpContext);
            // 判断是否为内网访问
            var isLocal = InternalHelper.IsInternalIpAddress(clientIp);
            if (!isLocal && !HttpContext.Request.Cookies.Keys.Contains("access_token") && oldBindings.Any()) // 非内网且无有效Cookie且已有绑定
            {
                return RedirectToAction("WeUIError", "Home", new { title = "仅允许内网访问此操作" });
            }

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,  // 增强安全性，防止JS访问
                Secure = HttpContext.Request.IsHttps,  // HTTPS环境下才传输
                SameSite = SameSiteMode.Strict,
                // 设置Cookie过期时间为1年（永久性Cookie）
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            };
            // 1. 检查是否已有access_token的Cookie
            var accessToken = HttpContext.Request.Cookies["access_token"];
            if (string.IsNullOrEmpty(accessToken) || !oldBindings.Any(f => f.AccessToken.Equals(accessToken)))
            {
                // 如果没有，则生成新的并存储到Cookie
                accessToken = GenerateAccessToken();
                HttpContext.Response.Cookies.Append("access_token", accessToken, cookieOptions);
                // 2. 保存用户与设备绑定关系到数据库
                await SaveUserDeviceBinding(userId, accessToken);
            }
            HttpContext.Response.Cookies.Append("SelectedUserId", userId.ToString(), cookieOptions);

            // 跳转到首页并携带设备码（或使用视图过渡）
            return RedirectToAction("Index", "Home");
        }

        // 保存用户与access_token的绑定关系
        private async Task SaveUserDeviceBinding(int userId, string accessToken)
        {
            var binding = new UserDeviceBinding
            {
                UserId = userId,
                AccessToken = accessToken,  // 存储access_token
                BindingTime = DateTime.Now,
                DeviceInfo = GetDeviceInfo()
            };

            _context.UserDeviceBindings.Add(binding);
            await _context.SaveChangesAsync();
        }

        // 获取设备相关信息
        private string GetDeviceInfo()
        {
            var userAgent = HttpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            return $"IP: {ipAddress}, Browser: {userAgent}";
        }

        // 生成符合标准的access_token（32位长度）
        private string GenerateAccessToken()
        {
            // 包含大小写字母、数字和特殊字符，增强安全性
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}|;:,.<>?";

            var data = new byte[32];  // 32字节用于生成32位字符
            System.Security.Cryptography.RandomNumberGenerator.Fill(data);  // 填充随机字节

            var result = new char[32];
            for (int i = 0; i < data.Length; i++)
            {
                // 将随机字节映射到字符集索引
                result[i] = chars[data[i] % chars.Length];
            }
            return new string(result);
        }

        // 创建或更新用户页面
        public async Task<IActionResult> CreateOrUpdate(int? userId)
        {
            if (userId.HasValue)
            {
                // 如果有用户ID，加载现有用户数据用于编辑
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }
                return View(user);
            }
            // 否则返回空视图用于创建新用户
            return View(new User());
        }

        // 处理用户创建或更新提交
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrUpdate(int? id, string nickname, IFormFile? avatar)
        {
            if (!id.HasValue)
            {
                // 获取客户端实际IP地址，考虑反向代理情况
                var clientIp = InternalHelper.GetClientIpAddress(HttpContext);

                // 判断是否为内网访问
                var isLocal = InternalHelper.IsInternalIpAddress(clientIp);

                if (!isLocal)
                {
                    return RedirectToAction("WeUIError", "Home", new { title = "仅允许内网访问此操作" });
                }
            }

            // 确保头像文件夹存在
            var avatarFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "avatar");
            if (!Directory.Exists(avatarFolder))
            {
                Directory.CreateDirectory(avatarFolder);
            }

            User user;
            if (id.HasValue)
            {
                // 更新现有用户
                user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound();
                }
                user.Nickname = nickname;
            }
            else
            {
                // 创建新用户
                user = new User
                {
                    Nickname = nickname,
                    CreatedAt = DateTime.Now
                };
                _context.Users.Add(user);
            }

            if (string.IsNullOrWhiteSpace(nickname))
            {
                ModelState.AddModelError("Nickname", "昵称不能为空");
                return View(user);
            }

            // 检查昵称唯一性（更新时排除当前用户）
            var nicknameExists = id.HasValue
                ? await _context.Users.AnyAsync(u => u.Nickname == nickname && u.Id != id)
                : await _context.Users.AnyAsync(u => u.Nickname == nickname);

            if (nicknameExists)
            {
                ModelState.AddModelError("Nickname", "该昵称已被使用，请选择其他昵称");
                return View(user);
            }

            // 处理头像上传（如果有新头像则更新）
            if (avatar != null && avatar.Length > 0)
            {
                // 生成唯一文件名（避免重名）
                var fileExtension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(avatarFolder, fileName);

                // 保存文件到服务器
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                // 删除旧头像（如果是更新操作且存在旧头像）
                if (id.HasValue && !string.IsNullOrEmpty(user.AvatarUrl))
                {
                    var oldFilePath = Path.Combine(avatarFolder, Path.GetFileName(user.AvatarUrl));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        try
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                        catch (Exception ex)
                        {
                            // 记录删除旧文件失败的日志
                            _logger.LogWarning(ex, "删除旧头像文件失败: {FilePath}", oldFilePath);
                        }
                    }
                }

                // 保存相对路径到数据库（便于前端访问）
                user.AvatarUrl = $"/avatar/{fileName}";
            }
            // 如果是更新且没有上传新头像，则保留原有头像

            await _context.SaveChangesAsync();

            // 触发用户创建/更新事件
            if (!id.HasValue)
            {
                OnUserCreated(user);
                return RedirectToAction("UserSelect");
            }
            else
            {
                OnUserUpdated(user);
                return RedirectToAction("Index", "Home");
            }
        }


        // 添加用户更新事件处理方法
        private void OnUserUpdated(User user)
        {
            // 用户更新后的逻辑，如日志记录
            // _logger.LogInformation($"用户更新: ID={user.Id}, 昵称={user.Nickname}");
        }

        // 用户创建事件处理方法
        private void OnUserCreated(User user)
        {
            // 1. 可以记录日志
            // _logger.LogInformation($"新用户创建: ID={user.Id}, 昵称={user.Nickname}");

            // 2. 可以发送通知（如WebSocket通知其他在线用户）
            // _notificationService.NotifyNewUser(user);

            // 3. 可以初始化用户相关数据
            // InitializeUserSettings(user.Id);
        }
    }
}
