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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 1. User表：用户名唯一索引（之前已配置）
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Nickname)
                .IsUnique();

            // 2. Game表：优化按状态查询和创建时间排序（高频用于进行中牌局查询）
            modelBuilder.Entity<Game>()
                .HasIndex(g => new { g.Status, g.CreatedAt }); // 复合索引：状态+创建时间
            modelBuilder.Entity<Game>()
                .HasIndex(g => g.CreatorId); // 按创建者查询索引

            // 3. GamePlayer表：优化加入牌局检查和查询牌局玩家（高频关联查询）
            modelBuilder.Entity<GamePlayer>()
                .HasIndex(gp => new { gp.GameId, gp.UserId }) // 复合索引：牌局ID+用户ID（唯一组合）
                .IsUnique(); // 防止重复加入
            modelBuilder.Entity<GamePlayer>()
                .HasIndex(gp => gp.GameId); // 单独牌局ID索引（查询牌局内所有玩家）

            // 4. GameResult表：优化排行榜和极值查询（高频聚合查询）
            modelBuilder.Entity<GameResult>()
                .HasIndex(gr => gr.UserId); // 按用户ID查询结果
            modelBuilder.Entity<GameResult>()
                .HasIndex(gr => gr.GameId); // 按牌局ID查询结果（关联Game表）

            // 5. Transaction表：优化牌局内交易记录查询
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.GameId); // 按牌局ID查询交易

            // 6. SettlementTransaction表：优化结算记录查询
            modelBuilder.Entity<SettlementTransaction>()
                .HasIndex(st => st.GameId); // 按牌局ID查询结算记录
        }
    }
}