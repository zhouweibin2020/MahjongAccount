namespace MahjongAccount.Models
{
    public class GamePlayer
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int UserId { get; set; }

        /// <summary>
        /// 玩家方位：东、南、西、北
        /// </summary>
        public string Direction { get; set; }
        public bool IsReady { get; set; } = false;
        public DateTime JoinedAt { get; set; } = DateTime.Now;

        // 导航属性
        public Game Game { get; set; }
        public User User { get; set; }
        public List<GameResult> GameResults { get; set; }
    }
}
