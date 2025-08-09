namespace MahjongAccount.Models
{
    public class GameResult
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int UserId { get; set; }
        public int TotalWin { get; set; }
        public int TotalLose { get; set; }
        public int NetResult { get; set; }

        // 导航属性
        public Game Game { get; set; }
        public User User { get; set; }
    }
}
