namespace MahjongAccount.Models.Dtos
{
    /// <summary>
    /// 结算转账记录DTO
    /// </summary>
    public class SettlementTransactionDto
    {
        /// <summary>
        /// 转账金额
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// 付款方昵称
        /// </summary>
        public string FromNickname { get; set; }

        /// <summary>
        /// 收款方昵称
        /// </summary>
        public string ToNickname { get; set; }

        /// <summary>
        /// 付款方头像
        /// </summary>
        public string FromAvatarUrl { get; set; }

        /// <summary>
        /// 收款方头像
        /// </summary>
        public string ToAvatarUrl { get; set; }

        /// <summary>
        /// 付款方ID
        /// </summary>
        public int FromUserId { get; set; }

        /// <summary>
        /// 收款方ID
        /// </summary>
        public int ToUserId { get; set; }
    }
}
