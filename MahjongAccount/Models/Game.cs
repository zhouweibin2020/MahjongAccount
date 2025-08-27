using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MahjongAccount.Models
{
    public class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } = "川麻"; // 川麻/宝中宝
        public string? Remarks { get; set; }
        public int CreatorId { get; set; }
        public string Status { get; set; } = "ongoing"; // ongoing/ended

        /// <summary>
        /// 开局方位：东、南、西、北
        /// </summary>
        public string BeginDirection { get; set; }
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
