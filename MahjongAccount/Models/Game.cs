using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MahjongAccount.Models
{
    public class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Remarks { get; set; }
        public int CreatorId { get; set; }
        public string Status { get; set; } = "ongoing"; // ongoing/ended
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? EndedAt { get; set; }

        // 导航属性
        public User Creator { get; set; }
        public ICollection<GamePlayer> GamePlayers { get; set; }
        public ICollection<Transaction> Transactions { get; set; }
        public ICollection<GameResult> GameResults { get; set; }
        public ICollection<SettlementTransaction> SettlementTransactions { get; set; }
    }
}
