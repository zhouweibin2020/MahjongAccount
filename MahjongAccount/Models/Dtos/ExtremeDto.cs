namespace MahjongAccount.Models.Dtos
{
    public class ExtremeDto
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public bool IsCurrentUser { get; set; }

        /// <summary>
        /// 用户昵称
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// 用户头像
        /// </summary>
        public string AvatarUrl { get; set; }

        /// <summary>
        /// 日期
        /// </summary>
        public string Period { get; set; }

        /// <summary>
        /// 金额
        /// </summary>
        public int TotalResult { get; set; }
    }
}
