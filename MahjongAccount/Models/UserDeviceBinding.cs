using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Models
{
    public class UserDeviceBinding
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string AccessToken { get; set; }

        public DateTime BindingTime { get; set; }

        public string DeviceInfo { get; set; }

        // 导航属性（可选，关联用户表）
        public virtual User User { get; set; }
    }
}
