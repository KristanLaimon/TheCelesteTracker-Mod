namespace TheCelesteTracker_Database
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Celeste Climber";
        public List<SaveData> SaveFiles { get; set; } = new();
    }

    public class SaveData
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int SlotNumber { get; set; }
        public string FileName { get; set; } = "";

        public User User { get; set; } = null!;
        public List<Campaign> Campaigns { get; set; } = new();
    }

    public class Campaign
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<SaveData> SaveFiles { get; set; } = new();
        public List<Chapter> Chapters { get; set; } = new();
    }

    public class Chapter
    {
        public int Id { get; set; }
        public int CampaignId { get; set; }
        public string SID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Mode { get; set; } = "";

        public Campaign Campaign { get; set; } = null!;
    }

    public class Run
    {
        public int Id { get; set; }
        public int SaveDataId { get; set; }
        public int ChapterId { get; set; }
        public string? CompletionTime { get; set; }
        public long TimeTicks { get; set; }
        public int Screens { get; set; }
        public int Deaths { get; set; }
        public int Strawberries { get; set; }
        public bool Golden { get; set; }

        public List<RoomDeath> RoomDeaths { get; set; } = new();
    }

    public class RoomDeath
    {
        public int Id { get; set; }
        public int RunId { get; set; }
        public string RoomName { get; set; } = "";
        public int Deaths { get; set; }
    }
}
