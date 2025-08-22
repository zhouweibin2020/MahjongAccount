namespace MahjongAccount.Models.ViewModels
{
    public class HistoryViewModel
    {
        /// <summary>
        /// 当前用户
        /// </summary>
        public required User CurrentUser { get; set; }

        /// <summary>
        /// 总游戏局数
        /// </summary>
        public int TotalGames { get; set; }

        /// <summary>
        /// 总净胜分
        /// </summary>
        public int TotalNetResult { get; set; }

        /// <summary>
        /// 总赢局数
        /// </summary>
        public int TotalWin { get; set; }

        /// <summary>
        /// 总输局数
        /// </summary>
        public int TotalLose { get; set; }

        /// <summary>
        /// 开始日期
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// 结束日期
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// 历史记录项列表
        /// </summary>
        public required List<HistoryItemViewModel> HistoryItems { get; set; }
    }

    // 历史记录项视图模型
    public class HistoryItemViewModel
    {
        public int GameId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public required string Status { get; set; }
        public int UserNetResult { get; set; }
        public int UserTotalWin { get; set; }
        public int UserTotalLose { get; set; }
    }
}
