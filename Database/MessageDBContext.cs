using Microsoft.EntityFrameworkCore;

namespace Lagrange.HikawaHina.Database
{
    internal sealed class MessageDBContext : DbContext
    {
        public MessageDBContext() : base()
        {
            // 使用SQLite数据库
            Database.SetConnectionString("Data Source=message.db");
            Database.EnsureCreated();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite();
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ModelMessage>().Ignore(e => e.TempIndex);
            base.OnModelCreating(modelBuilder);
        }
        public DbSet<ModelMessage> Messages { get; set; }
    }
}
