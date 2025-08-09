using MahjongAccount.Models;
using Microsoft.EntityFrameworkCore;

namespace MahjongAccount.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<User> Users { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<GamePlayer> GamePlayers { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<GameResult> GameResults { get; set; }
        public DbSet<SettlementTransaction> SettlementTransactions { get; set; }
    }
}
