namespace MahjongAccount.Models
{
    public class GamePlayer
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int UserId { get; set; }
        public bool IsReady { get; set; } = false;
        public DateTime JoinedAt { get; set; } = DateTime.Now;

        // 导航属性
        public Game Game { get; set; }
        public User User { get; set; }
        public List<GameResult> GameResults { get; set; }
    }
}
