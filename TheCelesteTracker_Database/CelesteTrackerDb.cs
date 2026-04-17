using CommonCode;
using TheCelesteTracker_Database;

public class CelesteTrackerDb
{
    private DatabaseContext _db;
    private ISimpleLogger? _logger;
    private string _path;
    private User _currentUser;

    public CelesteTrackerDb(string path, ISimpleLogger? loggerToUse = null)
    {
        _db = new DatabaseContext(path);
        _path = path;
        _logger = loggerToUse;
        _currentUser = _db.Users.First()!; //Theres always at least one user in db, seeded in "DatabaseContext.cs" 
        _db.Entry(_currentUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached; // Do not let EF track this, not necessary.
    }

    public async Task InitializeDb()
    {
        if (File.Exists(_path))
            return;

        await _db.Database.EnsureCreatedAsync();
        await _db.SaveChangesAsync();
    }


    public async Task ResetDatabase()
    {
        _db.Dispose();

        using (var tempContext = new DatabaseContext(_path))
        {
            await tempContext.Database.EnsureDeletedAsync();
            await tempContext.Database.EnsureCreatedAsync();
            await tempContext.SaveChangesAsync();
        }

        _db = new DatabaseContext(_path);
        _logger?.Log("Database reset. Internal tracker cache cleared.");
    }

    public async Task AddFullSaveData(XMLSaveData saveFile, int slotNumber)
    {
        // 1. Insert SaveData first to generate its database PK (Id) for the Composite Semantic IDs
        var saveData = new SaveData
        {
            SlotNumber = slotNumber,
            FileName = saveFile.Name,
            UserId = _currentUser.Id,
            Campaigns = new List<Campaign>()
        };

        await _db.Saves.AddAsync(saveData);
        await _db.SaveChangesAsync();

        var sessions = new List<GameSession>();

        // 2. Parse Vanilla Campaign (Root Areas)
        if (saveFile.Areas?.AreaStats?.Any() == true)
        {
            saveData.Campaigns.Add(ProcessCampaign(saveData, "Celeste Vanilla", saveFile.Areas.AreaStats, sessions));
        }

        // 3. Parse Modded Campaigns (LevelSets)
        if (saveFile.LevelSets?.LevelSetStats?.Any() == true)
        {
            foreach (var levelSet in saveFile.LevelSets.LevelSetStats)
            {
                // Skip empty tracking nodes
                if (levelSet.Areas?.AreaStats == null || !levelSet.Areas.AreaStats.Any())
                    continue;

                string modName = string.IsNullOrWhiteSpace(levelSet.Name) ? "Unknown Mod" : levelSet.Name;
                saveData.Campaigns.Add(ProcessCampaign(saveData, modName, levelSet.Areas.AreaStats, sessions));
            }
        }

        // 4. Save the Campaigns and Chapters
        await _db.SaveChangesAsync();

        // 5. Save the GameSessions associated with the chapters
        if (sessions.Any())
        {
            await _db.GameSessions.AddRangeAsync(sessions);
            await _db.SaveChangesAsync();
        }
    }

    private Campaign ProcessCampaign(SaveData saveData, string campaignNameId, List<AreaStats> areaStats, List<GameSession> sessions)
    {
        string campaignId = $"{saveData.Id}:{campaignNameId}";

        var campaign = new Campaign
        {
            Id = campaignId,
            SaveDataId = saveData.Id,
            CampaignNameId = campaignNameId,
            Chapters = new List<Chapter>()
        };


        //All Chapters of this campaign (Celeste Vanilla, levels in a mod...)
        foreach (var area in areaStats)
        {
            if (area.Modes?.AreaModeStats == null) continue;

            // Prefix SID with the campaign ID to ensure absolute uniqueness across multiple save files
            string chapterSid = $"{campaignId}:{area.SID ?? $"Unknown_SID_{area.ID}"}";

            var chapter = new Chapter
            {
                SID = chapterSid,
                CampaignId = campaignId,
                Name = area.SID?.Split('/').LastOrDefault() ?? $"Area {area.ID}",
                BerriesCollected = 0,
                BerriesAvailable = 0, // XML structure does not clearly define total global maximums here
                GoldenBerryCollected = false,
                Rooms = new List<ChapterRoom>() // Room death data is not available in the top-level AreaModeStats XML
            };

            //All sides (Commonly SIDEA, SIDEB, SIDEC) of this chapter
            for (int i = 0; i < area.Modes.AreaModeStats.Count; i++)
            {
                var modeStat = area.Modes.AreaModeStats[i];

                // Skip unplayed modes
                if (modeStat.TimePlayed == 0 && modeStat.Deaths == 0) continue;

                chapter.BerriesCollected += (int)modeStat.TotalStrawberries;

                // Heuristic for goldens based on standard tracker logic
                bool isGolden = modeStat.SingleRunCompleted && modeStat.Deaths == 0;
                chapter.GoldenBerryCollected |= isGolden;

                string sideId = i switch
                {
                    0 => "SIDEA",
                    1 => "SIDEB",
                    2 => "SIDEC",
                    _ => "SIDEA"
                };

                var session = new GameSession
                {
                    Id = Guid.NewGuid().ToString(),
                    ChapterSID = chapterSid,
                    ChapterSideId = sideId,
                    TimeTicksPlaytime = modeStat.TimePlayed,
                    DateTimeStarted = DateTime.UtcNow,
                    IsGoldenBerryAttempt = isGolden,
                    IsGoldenBerryCompletedRun = isGolden,
                    RoomStats = new List<GameSessionChapterRoomStats>()
                };

                sessions.Add(session);
            }

            if (sessions.Any(s => s.ChapterSID == chapterSid))
            {
                campaign.Chapters.Add(chapter);
            }
        }

        return campaign;
    }
}