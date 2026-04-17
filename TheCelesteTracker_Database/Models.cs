using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TheCelesteTracker_Database
{
    /// <summary>
    /// Represents a unique application user.
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "Celeste Climber";

        public List<SaveData> SaveFiles { get; set; } = new();
    }

    /// <summary>
    /// Represents a specific Celeste save slot (e.g., 0.celeste).
    /// </summary>
    public class SaveData
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>
        /// The physical slot index in the game.
        /// </summary>
        public int SlotNumber { get; set; }

        [Required]
        public string FileName { get; set; } = "";

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
        public List<Campaign> Campaigns { get; set; } = new();
    }

    /// <summary>
    /// Represents a set of maps (Vanilla, Strawberry Jam, etc.) associated with a SaveData.
    /// Uses a Composite Semantic ID: "{SaveDataId}:{CampaignNameId}".
    /// </summary>
    public class Campaign
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = null!;

        [Required]
        public int SaveDataId { get; set; }

        [Required]
        public string CampaignNameId { get; set; } = null!;

        [ForeignKey(nameof(SaveDataId))]
        public SaveData SaveData { get; set; } = null!;
        public List<Chapter> Chapters { get; set; } = new();
    }

    /// <summary>
    /// Represents a map/level within a campaign.
    /// </summary>
    public class Chapter
    {
        /// <summary>
        /// Unique String Identifier (SID) from the game data.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string SID { get; set; } = null!;

        [Required]
        public string CampaignId { get; set; } = null!;

        public string Name { get; set; } = "";
        public int BerriesAvailable { get; set; }
        [ForeignKey(nameof(CampaignId))]
        public Campaign Campaign { get; set; } = null!;
        public List<ChapterRoom> Rooms { get; set; } = new();
    }

    /// <summary>
    /// Constant values for map sides: SIDEA, SIDEB, SIDEC.
    /// </summary>
    public class ChapterSide
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [MaxLength(10)]
        public string Id { get; set; } = null!; // e.g., SIDEA
        public string Name { get; set; } = "";
    }

    /// <summary>
    /// Represents a specific room/screen within a Chapter.
    /// </summary>
    public class ChapterRoom
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; } = null!;

        [Required]
        public string ChapterSID { get; set; } = null!;

        public string Name { get; set; } = "";
        public int Order { get; set; }
        public int StrawberriesAvailable { get; set; }

        [ForeignKey(nameof(ChapterSID))]
        public Chapter Chapter { get; set; } = null!;
    }

    /// <summary>
    /// Tracks a specific gameplay session or "Run" for a chapter side.
    /// </summary>
    public class GameSession
    {
        [Key]
        public string Id { get; set; } = null!;

        [Required]
        public string ChapterSID { get; set; } = null!;

        [Required]
        public string ChapterSideId { get; set; } = null!;

        public long TimeTicksPlaytime { get; set; }
        public DateTime DateTimeStarted { get; set; }
        public bool IsGoldenBerryAttempt { get; set; }
        public bool IsGoldenBerryCompletedRun { get; set; }

        [ForeignKey(nameof(ChapterSID))]
        public Chapter Chapter { get; set; } = null!;

        [ForeignKey(nameof(ChapterSideId))]
        public ChapterSide ChapterSide { get; set; } = null!;

        public List<GameSessionChapterRoomStats> RoomStats { get; set; } = new();
    }

    /// <summary>
    /// Cross-reference table for stats per room within a session.
    /// </summary>
    public class GameSessionChapterRoomStats
    {
        // [Key] Handled in OnModelCreating via HasKey
        public string GameSessionId { get; set; } = null!;

        // [Key] Handled in OnModelCreating via HasKey
        public string ChapterRoomId { get; set; } = null!;

        public int Deaths { get; set; }
        public int Dashes { get; set; }

        //I know, theres no strict validation against the total chapter strawberry available strawberries.
        //Should be validated in runtime i guess.
        public int StrawberriesAchieved { get; set; }

        [ForeignKey(nameof(GameSessionId))]
        public GameSession GameSession { get; set; } = null!;

        [ForeignKey(nameof(ChapterRoomId))]
        public ChapterRoom ChapterRoom { get; set; } = null!;
    }
}
