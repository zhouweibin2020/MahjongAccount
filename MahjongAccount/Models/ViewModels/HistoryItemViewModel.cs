namespace MahjongAccount.Models.ViewModels
{
    // 历史记录项视图模型
    public class HistoryItemViewModel
    {
        public int GameId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string Status { get; set; }
        public int UserNetResult { get; set; }
        public int UserTotalWin { get; set; }
        public int UserTotalLose { get; set; }
    }
}
