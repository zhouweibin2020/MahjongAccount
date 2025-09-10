namespace MahjongAccount.Models.ViewModels
{
    public class StatisticsViewModel
    {
        /// <summary>
        /// 牌局信息
        /// </summary>
        public Game Game { get; set; }

        /// <summary>
        /// 人员金额(输)统计
        /// </summary>
        public TotalAmountDto[] LoseTotalAmountStatistics { get; set; }

        /// <summary>
        /// 人员金额(赢)统计
        /// </summary>
        public TotalAmountDto[] WinTotalAmountStatistics { get; set; }

        /// <summary>
        /// 金额计次统计
        /// </summary>
        public AmountCountStatisticsDto[] AmountCountStatistics { get; set; }

        /// <summary>
        /// 曲线数据
        /// </summary>
        public CurveDataDto[] CurveDatas { get; set; }
    }

    public class TotalAmountDto
    {
        public User User { get; set; }

        /// <summary>
        /// 总金额
        /// </summary>
        public int TotalAmount { get; set; }
    }

    /// <summary>
    /// 金额计次统计
    /// </summary>
    public class AmountCountStatisticsDto
    {
        /// <summary>
        /// 金额
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// 赢次
        /// </summary>
        public int WinCount { get; set; }

        /// <summary>
        /// 输次
        /// </summary>
        public int LoseCount { get; set; }
    }

    /// <summary>
    /// 曲线数据
    /// </summary>
    public class CurveDataDto
    {
        /// <summary>
        /// 金额
        /// </summary>
        public int Amount { get; set; }
        
        /// <summary>
        /// 创建事件
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}