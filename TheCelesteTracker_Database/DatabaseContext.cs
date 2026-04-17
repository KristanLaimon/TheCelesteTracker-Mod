using Microsoft.EntityFrameworkCore;

namespace TheCelesteTracker_Database
{
    internal class DatabaseContext : DbContext
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<SaveData> Saves => Set<SaveData>();
        public DbSet<Campaign> Campaigns => Set<Campaign>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<ChapterSide> ChapterSides => Set<ChapterSide>();
        public DbSet<ChapterRoom> ChapterRooms => Set<ChapterRoom>();
        public DbSet<GameSession> GameSessions => Set<GameSession>();
        public DbSet<GameSessionChapterRoomStats> SessionRoomStats => Set<GameSessionChapterRoomStats>();

        private readonly string _dbPath;

        public DatabaseContext(string dbPath)
        {
            _dbPath = dbPath;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={_dbPath}");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- Composite Keys Configuration ---

            // Stats table uses a composite PK of Session + Room
            modelBuilder.Entity<GameSessionChapterRoomStats>()
                .HasKey(s => new { s.GameSessionId, s.ChapterRoomId });

            // Ensure Campaign Business Logic Uniqueness (Save + Name)
            modelBuilder.Entity<Campaign>()
                .HasIndex(c => new { c.SaveDataId, c.CampaignNameId })
                .IsUnique();

            // --- Seeding Data ---

            // Mandatory Sides
            modelBuilder.Entity<ChapterSide>().HasData(
                new ChapterSide { Id = "SIDEA", Name = "Side A" },
                new ChapterSide { Id = "SIDEB", Name = "Side B" },
                new ChapterSide { Id = "SIDEC", Name = "Side C" }
            );

            // Default Application User
            modelBuilder.Entity<User>().HasData(
                new User { Id = 1, Name = "Celeste Climber" }
            );

            // --- Constraints ---
            modelBuilder.Entity<User>().HasIndex(u => u.Name).IsUnique();
        }
    }
}
