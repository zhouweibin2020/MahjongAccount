namespace MahjongAccount.Models.Dtos
{
    /// <summary>
    /// 用户排行榜数据模型
    /// </summary>
    public class UserRankingDto
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
        /// 总积分/总赢金额
        /// </summary>
        public int TotalPoints { get; set; }

        /// <summary>
        /// 参与牌局数
        /// </summary>
        public int GameCount { get; set; }
    }
}
