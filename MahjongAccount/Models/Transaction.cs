namespace MahjongAccount.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public int GameId { get; set; }
        public int FromUserId { get; set; }
        public int ToUserId { get; set; }
        public int Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 导航属性
        public Game Game { get; set; }
        public User FromUser { get; set; }
        public User ToUser { get; set; }
    }
}
