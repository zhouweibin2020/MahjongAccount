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
            HttpContext.Session.SetInt32("SelectedUserId", userId);
            return RedirectToAction("Index", "Home");
        }

        // 创建用户页面
        public IActionResult Create()
        {
            return View();
        }

        // 处理用户创建提交
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string nickname, IFormFile avatar)
        {
            if (string.IsNullOrWhiteSpace(nickname))
            {
                ModelState.AddModelError("Nickname", "昵称不能为空");
                return View();
            }
            // 检查昵称是否已存在
            var nicknameExists = await _context.Users.AnyAsync(u => u.Nickname == nickname);
            if (nicknameExists)
            {
                ModelState.AddModelError("Nickname", "该昵称已被使用，请选择其他昵称");
                return View();
            }

            var user = new User
            {
                Nickname = nickname,
                CreatedAt = DateTime.Now
            };

            // 处理头像上传
            if (avatar != null && avatar.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await avatar.CopyToAsync(memoryStream);
                    user.Avatar = memoryStream.ToArray();
                }
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 触发用户创建事件（可以在这里添加事件处理逻辑）
            OnUserCreated(user);

            return RedirectToAction("UserSelect");
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
