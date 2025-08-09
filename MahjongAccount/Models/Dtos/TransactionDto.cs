namespace MahjongAccount.Models.Dtos
{
    /// <summary>
    /// 交易记录DTO
    /// </summary>
    public class TransactionDto
    {
        /// <summary>
        /// 交易ID
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 交易ID
        /// </summary>
        public int GameId { get; set; }

        /// <summary>
        /// 付款用户ID
        /// </summary>
        public int FromUserId { get; set; }

        /// <summary>
        /// 付款用户昵称
        /// </summary>
        public string FromNickname { get; set; }

        /// <summary>
        /// 收款用户ID
        /// </summary>
        public int ToUserId { get; set; }

        /// <summary>
        /// 收款用户昵称
        /// </summary>
        public string ToNickname { get; set; }

        /// <summary>
        /// 交易金额
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// 交易时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
