#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using CommonCode;
using Celeste.Mod.TheCelesteTracker_Mod.Database;

namespace Celeste.Mod.TheCelesteTracker_Mod.Database
{
    public class CelesteTrackerDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ISimpleLogger _logger;

        public User CurrentUser { get; private set; } = null!;

        public CelesteTrackerDb(string dbPath, ISimpleLogger logger)
        {
            _connection = new SqliteConnection($"Data Source={dbPath}");
            _connection.Open();
            _logger = logger;
            InitDatabase();
            EnsureCurrentUser();
        }

        private void InitDatabase()
        {
            // Create tables based on schema and module usage
            string sql = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT UNIQUE NOT NULL
                );

                CREATE TABLE IF NOT EXISTS SaveDatas (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    SlotNumber INTEGER NOT NULL,
                    FileName TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS Campaigns (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SaveDataId INTEGER NOT NULL,
                    CampaignNameId TEXT NOT NULL,
                    FOREIGN KEY (SaveDataId) REFERENCES SaveDatas(Id)
                );

                CREATE TABLE IF NOT EXISTS Chapters (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CampaignId INTEGER NOT NULL,
                    SID TEXT NOT NULL,
                    Name TEXT,
                    Mode TEXT,
                    TotalBerries INTEGER,
                    FOREIGN KEY (CampaignId) REFERENCES Campaigns(Id)
                );

                CREATE TABLE IF NOT EXISTS GameSessions (
                    Id TEXT PRIMARY KEY,
                    ChapterSID TEXT NOT NULL,
                    ChapterSideId TEXT NOT NULL,
                    DateTimeStarted TEXT NOT NULL,
                    IsGoldenBerryAttempt INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS GameSessionChapterRoomStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GameSessionId TEXT NOT NULL,
                    RoomName TEXT NOT NULL,
                    Deaths INTEGER NOT NULL,
                    FOREIGN KEY (GameSessionId) REFERENCES GameSessions(Id)
                );
            ";
            _connection.Execute(sql);
        }

        public void ResetDatabase()
        {
            _connection.Execute(@"
                PRAGMA foreign_keys = OFF;
                DROP TABLE IF EXISTS GameSessionChapterRoomStats;
                DROP TABLE IF EXISTS GameSessions;
                DROP TABLE IF EXISTS Chapters;
                DROP TABLE IF EXISTS Campaigns;
                DROP TABLE IF EXISTS SaveDatas;
                DROP TABLE IF EXISTS Users;
                PRAGMA foreign_keys = ON;
            ");
            InitDatabase();
            EnsureCurrentUser();
            _logger.Log("Database has been reset.");
        }

        private void EnsureCurrentUser()
        {
            var user = _connection.QueryFirstOrDefault<User>("SELECT * FROM Users LIMIT 1");
            if (user == null)
            {
                string name = Environment.UserName ?? "DefaultUser";
                _connection.Execute("INSERT INTO Users (Name) VALUES (@Name)", new { Name = name });
                user = _connection.QueryFirstOrDefault<User>("SELECT * FROM Users WHERE Name = @Name", new { Name = name });
            }
            CurrentUser = user!;
        }

        public async Task<GameSession> GetCurrentSessionFullStats(string saveName, int saveSlot, string levelSet, string areaSid, string modeId, int totalBerries)
        {
            // 1. Ensure exists current "SaveData" in DB
            SaveData? saveFileFound = await GetSaveData(saveName, saveSlot);
            if (saveFileFound is null)
            {
                saveFileFound = await InsertSaveData(new SaveData { FileName = saveName, SlotNumber = saveSlot, UserId = CurrentUser.Id });
            }

            // 2. Ensure exists current "Campaign" data linked to this "SaveData" in DB
            Campaign? foundActualCampaign = await GetCampaign(saveFileFound.Id, levelSet);
            if (foundActualCampaign is null)
            {
                foundActualCampaign = await Campaign_InsertSingle(saveFileFound.Id, levelSet);
            }

            // 3. Ensure exists current "Chapter" (also known as AreaData) linked to "Campaign" that is linked to current "SaveData"
            string expectedChapterSid = $"{foundActualCampaign.Id}:{areaSid}";
            Chapter? foundActualChapterLevel = await GetChapter(expectedChapterSid);
            if (foundActualChapterLevel is null)
            {
                foundActualChapterLevel = await Chapter_InsertSingle(expectedChapterSid, foundActualCampaign.Id, totalBerries);
            }

            // 4. Create and insert new session
            var newSession = new GameSession
            {
                Id = Guid.NewGuid().ToString(),
                ChapterSID = foundActualChapterLevel.SID,
                ChapterSideId = modeId,
                DateTimeStarted = DateTime.UtcNow,
                IsGoldenBerryAttempt = false,
                RoomStats = new List<GameSessionChapterRoomStats>()
            };
            await InsertGameSession(newSession);

            return newSession;
        }

        public async Task<SaveData?> GetSaveData(string fileName, int slotNumber)
        {
            return await _connection.QueryFirstOrDefaultAsync<SaveData>(
                "SELECT * FROM SaveDatas WHERE FileName = @FileName OR SlotNumber = @SlotNumber",
                new { FileName = fileName, SlotNumber = slotNumber }
            );
        }

        public async Task<SaveData> InsertSaveData(SaveData saveData)
        {
            string sql = @"
                INSERT INTO SaveDatas (UserId, SlotNumber, FileName)
                VALUES (@UserId, @SlotNumber, @FileName);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, saveData);
            saveData.Id = id;
            return saveData;
        }

        public async Task<Campaign?> GetCampaign(int saveDataId, string campaignNameId)
        {
            return await _connection.QueryFirstOrDefaultAsync<Campaign>(
                "SELECT * FROM Campaigns WHERE SaveDataId = @SaveDataId AND CampaignNameId = @CampaignNameId",
                new { SaveDataId = saveDataId, CampaignNameId = campaignNameId }
            );
        }

        public async Task<Campaign> Campaign_InsertSingle(int saveDataId, string campaignNameId)
        {
            var campaign = new Campaign { SaveDataId = saveDataId, CampaignNameId = campaignNameId };
            string sql = @"
                INSERT INTO Campaigns (SaveDataId, CampaignNameId)
                VALUES (@SaveDataId, @CampaignNameId);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, campaign);
            campaign.Id = id;
            return campaign;
        }

        public async Task<Chapter?> GetChapter(string sid)
        {
            return await _connection.QueryFirstOrDefaultAsync<Chapter>(
                "SELECT * FROM Chapters WHERE SID = @SID",
                new { SID = sid }
            );
        }

        public async Task<Chapter> Chapter_InsertSingle(string sid, int campaignId, int totalBerries)
        {
            var chapter = new Chapter { SID = sid, CampaignId = campaignId, TotalBerries = totalBerries };
            string sql = @"
                INSERT INTO Chapters (SID, CampaignId, TotalBerries)
                VALUES (@SID, @CampaignId, @TotalBerries);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, chapter);
            chapter.Id = id;
            return chapter;
        }

        public async Task InsertGameSession(GameSession session)
        {
            string sql = @"
                INSERT INTO GameSessions (Id, ChapterSID, ChapterSideId, DateTimeStarted, IsGoldenBerryAttempt)
                VALUES (@Id, @ChapterSID, @ChapterSideId, @DateTimeStarted, @IsGoldenBerryAttempt)
            ";
            await _connection.ExecuteAsync(sql, new
            {
                session.Id,
                session.ChapterSID,
                session.ChapterSideId,
                DateTimeStarted = session.DateTimeStarted.ToString("o"),
                IsGoldenBerryAttempt = session.IsGoldenBerryAttempt ? 1 : 0
            });
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
