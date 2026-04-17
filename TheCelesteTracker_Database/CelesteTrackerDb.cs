using CommonCode;
using TheCelesteTracker_Database;

public class CelesteTrackerDb
{
    public DatabaseContext ctx;
    private ISimpleLogger? _logger;
    private string _path;
    public User CurrentUser;


    public CelesteTrackerDb(string path, ISimpleLogger? loggerToUse = null)
    {
        ctx = new DatabaseContext(path);
        _path = path;
        _logger = loggerToUse;
        CurrentUser = ctx.Users.First()!; //Theres always at least one user in db, seeded in "DatabaseContext.cs" 
        ctx.Entry(CurrentUser).State = Microsoft.EntityFrameworkCore.EntityState.Detached; // Do not let EF track this, not necessary.
    }

    public async Task InitializeDb()
    {
        if (File.Exists(_path))
            return;

        await ctx.Database.EnsureCreatedAsync();
        await ctx.SaveChangesAsync();
    }


    public async Task ResetDatabase()
    {
        ctx.Dispose();

        using (var tempContext = new DatabaseContext(_path))
        {
            await tempContext.Database.EnsureDeletedAsync();
            await tempContext.Database.EnsureCreatedAsync();
            await tempContext.SaveChangesAsync();
        }

        ctx = new DatabaseContext(_path);
        _logger?.Log("Database reset. Internal tracker cache cleared.");
    }

    public async Task AddFullSaveData(XMLSaveData saveFile, int slotNumber)
    {
        // 1. Insert SaveData first to generate its database PK (Id) for the Composite Semantic IDs
        var saveData = new SaveData
        {
            SlotNumber = slotNumber,
            FileName = saveFile.Name,
            UserId = CurrentUser.Id,
            Campaigns = new List<Campaign>()
        };

        await ctx.Saves.AddAsync(saveData);
        await ctx.SaveChangesAsync();

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

                // Prevent unique constraint violation if there are multiple unnamed or duplicate LevelSets
                int counter = 1;
                string uniqueModName = modName;
                while (saveData.Campaigns.Any(c => c.CampaignNameId == uniqueModName))
                {
                    uniqueModName = $"{modName} ({counter++})";
                }

                saveData.Campaigns.Add(ProcessCampaign(saveData, uniqueModName, levelSet.Areas.AreaStats, sessions));
            }
        }

        // 4. Save the Campaigns and Chapters
        await ctx.SaveChangesAsync();

        // 5. Save the GameSessions associated with the chapters
        if (sessions.Any())
        {
            await ctx.GameSessions.AddRangeAsync(sessions);
            await ctx.SaveChangesAsync();
        }
    }

    //public static Campaign_GenerateId(int saveDataId, string campaignName) => $"{saveDataId}:{campaignName}";


    public async Task Campaign_InsertSingle(int saveDataId, string campaignName)
    {
        string campaignId = $"{saveDataId}:{campaignName}";

        var campaign = new Campaign
        {
            Id = campaignId,
            SaveDataId = saveDataId,
            CampaignNameId = campaignName,
            Chapters = new List<Chapter>()
        };

        await ctx.Campaigns.AddAsync(campaign);
        await ctx.SaveChangesAsync();
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
                BerriesAvailable = 0, // XML structure does not clearly define total global maximums here
                Rooms = new List<ChapterRoom>() // Room death data is not available in the top-level AreaModeStats XML
            };

            //All sides (Commonly SIDEA, SIDEB, SIDEC) of this chapter
            for (int i = 0; i < area.Modes.AreaModeStats.Count; i++)
            {
                var modeStat = area.Modes.AreaModeStats[i];

                // Skip unplayed modes
                if (modeStat.TimePlayed == 0 && modeStat.Deaths == 0) continue;

                // Heuristic for goldens based on standard tracker logic
                // In a save file, Deaths is cumulative. BestDeaths == 0 means a golden run was completed.
                bool isGolden = modeStat.Completed && modeStat.BestDeaths == 0;

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
                    IsGoldenBerryAttempt = isGolden, // If they have a golden, we treat the initial import as a successful attempt
                    IsGoldenBerryCompletedRun = isGolden,
                    RoomStats = new List<GameSessionChapterRoomStats>()
                };

                // Extract room names and strawberries collected in this mode/session
                int parsedBerriesCount = 0;
                if (modeStat.Strawberries?.EntityID != null)
                {
                    var sessionRoomBerries = new Dictionary<string, int>();
                    foreach (var entity in modeStat.Strawberries.EntityID)
                    {
                        var parts = entity.Key.Split(':');
                        if (parts.Length >= 1)
                        {
                            var roomName = parts[0];
                            if (!sessionRoomBerries.ContainsKey(roomName))
                                sessionRoomBerries[roomName] = 0;
                            sessionRoomBerries[roomName]++;
                        }
                    }

                    foreach (var kvp in sessionRoomBerries)
                    {
                        var roomId = $"{chapterSid}:{kvp.Key}";

                        // Ensure ChapterRoom exists in Chapter
                        if (!chapter.Rooms.Any(r => r.Id == roomId))
                        {
                            chapter.Rooms.Add(new ChapterRoom
                            {
                                Id = roomId,
                                ChapterSID = chapterSid,
                                Name = kvp.Key,
                                Order = chapter.Rooms.Count,
                                StrawberriesAvailable = 0
                            });
                        }

                        session.RoomStats.Add(new GameSessionChapterRoomStats
                        {
                            GameSessionId = session.Id,
                            ChapterRoomId = roomId,
                            Deaths = 0, // We don't know deaths per room from XML
                            Dashes = 0,
                            StrawberriesAchieved = kvp.Value
                        });

                        parsedBerriesCount += kvp.Value;
                    }
                }

                // Vanilla/Mod fallback: if there are more strawberries in TotalStrawberries than parsed in EntityIDs
                // OR if we need to store the total deaths (which we can't map to specific rooms)
                if (modeStat.TotalStrawberries > parsedBerriesCount || modeStat.Deaths > 0)
                {
                    int missingBerries = Math.Max(0, (int)modeStat.TotalStrawberries - parsedBerriesCount);
                    string legacyRoomName = "Imported_Unsorted_Data";
                    string roomId = $"{chapterSid}:{legacyRoomName}";

                    if (!chapter.Rooms.Any(r => r.Id == roomId))
                    {
                        chapter.Rooms.Add(new ChapterRoom
                        {
                            Id = roomId,
                            ChapterSID = chapterSid,
                            Name = "Imported Unsorted Data",
                            Order = -1,
                            StrawberriesAvailable = 0
                        });
                    }

                    session.RoomStats.Add(new GameSessionChapterRoomStats
                    {
                        GameSessionId = session.Id,
                        ChapterRoomId = roomId,
                        Deaths = (int)modeStat.Deaths, // Store total deaths here as fallback
                        Dashes = (int)modeStat.BestDashes, // Store best dashes as fallback
                        StrawberriesAchieved = missingBerries
                    });
                }

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