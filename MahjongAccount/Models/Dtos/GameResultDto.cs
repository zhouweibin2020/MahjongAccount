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
        public decimal TotalWin { get; set; }

        /// <summary>
        /// 总输金额
        /// </summary>
        public decimal TotalLose { get; set; }

        /// <summary>
        /// 净结果（赢-输）
        /// </summary>
        public decimal NetResult { get; set; }

        /// <summary>
        /// 玩家昵称
        /// </summary>
        public string Nickname { get; set; }

        /// <summary>
        /// 玩家头像
        /// </summary>
        public byte[] Avatar { get; set; }

        /// <summary>
        /// 玩家ID
        /// </summary>
        public int UserId { get; set; }
    }
}
