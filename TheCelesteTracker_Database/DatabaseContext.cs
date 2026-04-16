using Microsoft.EntityFrameworkCore;

namespace TheCelesteTracker_Database
{
    internal class DatabaseContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<SaveData> Saves => Set<SaveData>();
        public DbSet<Campaign> Campaigns => Set<Campaign>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Run> Runs => Set<Run>();
        public DbSet<RoomDeath> RoomDeaths => Set<RoomDeath>();


        private string fullDatabasePath;
        public DatabaseContext(string databasePath)
        {
            fullDatabasePath = databasePath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={fullDatabasePath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Unique constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Name).IsUnique();
            modelBuilder.Entity<Campaign>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<Chapter>().HasIndex(c => new { c.CampaignId, c.SID, c.Mode }).IsUnique();
        }
    }
}
