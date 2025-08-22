using MahjongAccount.Data;
using MahjongAccount.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Controllers
{
    public class UserController : Controller
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        // 用户选择页面
        public async Task<IActionResult> UserSelect()
        {
            // 获取所有用户列表
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        // 选择用户后的处理
        public async Task<IActionResult> ChooseUser(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // 这里可以实现用户选择逻辑，例如：
            // 1. 记录用户会话
            // 2. 跳转到首页
            HttpContext.Response.Cookies.Append("SelectedUserId", userId.ToString());
            return RedirectToAction("Index", "Home");
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
                using (var memoryStream = new MemoryStream())
                {
                    await avatar.CopyToAsync(memoryStream);
                    user.Avatar = memoryStream.ToArray();
                }
            }
            // 如果是更新且没有上传新头像，则保留原有头像

            await _context.SaveChangesAsync();

            // 触发用户创建/更新事件
            if (!id.HasValue)
            {
                OnUserCreated(user);
            }
            else
            {
                OnUserUpdated(user);
            }

            return RedirectToAction("UserSelect");
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
