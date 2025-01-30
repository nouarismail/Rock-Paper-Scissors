using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RockPaperScissorsApi.Models;

namespace RockPaperScissorsApi.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            
        }

        public DbSet<MatchHistory> MatchHistories { get; set; }
        public DbSet<GameTransactions> GameTransactions { get; set; }
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            
            builder.Entity<MatchHistory>()
                .HasOne(m => m.Player1)
                .WithMany()
                .HasForeignKey(m => m.Player1Id)
                .OnDelete(DeleteBehavior.Restrict); 

            builder.Entity<MatchHistory>()
                .HasOne(m => m.Player2)
                .WithMany()
                .HasForeignKey(m => m.Player2Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GameTransactions>()
                .HasOne(t => t.Sender)
                .WithMany()
                .HasForeignKey(t => t.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GameTransactions>()
                .HasOne(t => t.Receiver)
                .WithMany()
                .HasForeignKey(t => t.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            
            builder.Entity<MatchHistory>()
                .HasIndex(m => m.Player1Id)
                .IncludeProperties(m => new { m.Player2Id, m.MatchDate });

            builder.Entity<MatchHistory>()
                .HasIndex(m => m.MatchDate);

            builder.Entity<GameTransactions>()
                .HasIndex(t => t.SenderId)
                .IncludeProperties(t => new { t.ReceiverId, t.TransactionDate });

            builder.Entity<GameTransactions>()
                .HasIndex(t => t.TransactionDate);
        }

    }
}