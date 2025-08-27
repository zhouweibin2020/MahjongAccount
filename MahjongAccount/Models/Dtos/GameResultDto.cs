namespace MahjongAccount.Models.Dtos
{
    /// <summary>
    /// 玩家牌局结果DTO
    /// </summary>
    public class GameResultDto
    {
        /// <summary>
        /// 总赢金额
        /// </summary>
        public int TotalWin { get; set; }

        /// <summary>
        /// 总输金额
        /// </summary>
        public int TotalLose { get; set; }

        /// <summary>
        /// 净结果（赢-输）
        /// </summary>
        public int NetResult { get; set; }

        /// <summary>
        /// 玩家昵称
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// 玩家头像
        /// </summary>
        public string AvatarUrl { get; set; }

        /// <summary>
        /// 玩家ID
        /// </summary>
        public int UserId { get; set; }
    }
}
