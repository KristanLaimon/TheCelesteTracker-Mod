#nullable enable
using CommonCode;
using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Celeste.Mod.TheCelesteTracker_Mod.Database
{
    public class CelesteTrackerDb : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ISimpleLogger _logger;

        public User CurrentUser { get; private set; } = null!;

        public CelesteTrackerDb(string dbFilePath, ISimpleLogger logger)
        {
            _connection = new SqliteConnection($"Data Source={dbFilePath}");
            _connection.Open();
            _logger = logger;
            InitDatabase();
            EnsureCurrentUser();
        }

        private void InitDatabase()
        {
            // Create tables based on schema and module usage
            string sql = @"
                CREATE TABLE IF NOT EXISTS users (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL
                );

                CREATE TABLE IF NOT EXISTS save_datas (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id INTEGER NOT NULL,
                    slot_number INTEGER NOT NULL,
                    file_name TEXT NOT NULL,
                    FOREIGN KEY (user_id) REFERENCES users(id)
                );

                CREATE TABLE IF NOT EXISTS campaigns (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_data_id INTEGER NOT NULL,
                    campaign_name_id TEXT NOT NULL,
                    FOREIGN KEY (save_data_id) REFERENCES save_datas(id)
                );

                CREATE TABLE IF NOT EXISTS chapters (
                    sid TEXT PRIMARY KEY,
                    campaign_id INTEGER NOT NULL,
                    name TEXT,
                    FOREIGN KEY (campaign_id) REFERENCES campaigns(id)
                );

                CREATE TABLE IF NOT EXISTS chapter_sides (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    chapter_sid TEXT NOT NULL,
                    side_id TEXT NOT NULL,
                    berries_available INTEGER NOT NULL,
                    berries_collected INTEGER NOT NULL,
                    UNIQUE(chapter_sid, side_id),
                    FOREIGN KEY (chapter_sid) REFERENCES chapters(sid)
                );

                CREATE TABLE IF NOT EXISTS chapter_rooms (
                    chapter_sid TEXT NOT NULL,
                    name TEXT NOT NULL,
                    ""order"" INTEGER NOT NULL,
                    strawberries_available INTEGER NOT NULL,
                    PRIMARY KEY (chapter_sid, name),
                    FOREIGN KEY (chapter_sid) REFERENCES chapters(sid)
                );

                CREATE TABLE IF NOT EXISTS game_sessions (
                    id TEXT PRIMARY KEY,
                    chapter_side_id INTEGER NOT NULL,
                    date_time_start TEXT NOT NULL,
                    duration_ms INTEGER NOT NULL,
                    is_goldenberry_attempt INTEGER NOT NULL,
                    is_goldenberry_completed INTEGER NOT NULL,
                    FOREIGN KEY (chapter_side_id) REFERENCES chapter_sides(id)
                );

                CREATE TABLE IF NOT EXISTS game_session_chapter_room_stats (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    gamesession_id TEXT NOT NULL,
                    room_name TEXT NOT NULL,
                    visited_order INTEGER NOT NULL,
                    deaths_in_room INTEGER NOT NULL,
                    dashes_in_room INTEGER NOT NULL,
                    strawberries_achieved_in_room INTEGER NOT NULL,
                    hearts_achieved_in_room INTEGER NOT NULL,
                    jumps_in_room INTEGER NOT NULL,
                    FOREIGN KEY (gamesession_id) REFERENCES game_sessions(id)
                );
            ";
            _connection.Execute(sql);
        }

        public void ResetDatabase()
        {
            _connection.Execute(@"
                PRAGMA foreign_keys = OFF;
                DROP TABLE IF EXISTS game_session_chapter_room_stats;
                DROP TABLE IF EXISTS game_sessions;
                DROP TABLE IF EXISTS chapter_rooms;
                DROP TABLE IF EXISTS chapter_sides;
                DROP TABLE IF EXISTS chapters;
                DROP TABLE IF EXISTS campaigns;
                DROP TABLE IF EXISTS save_datas;
                DROP TABLE IF EXISTS users;
                PRAGMA foreign_keys = ON;
            ");
            InitDatabase();
            EnsureCurrentUser();
            _logger.Log("Database has been reset.");
        }

        private void EnsureCurrentUser()
        {
            var user = _connection.QueryFirstOrDefault<User>("SELECT * FROM users LIMIT 1");
            if (user == null)
            {
                string name = Environment.UserName ?? "Celeste climber";
                _connection.Execute("INSERT INTO users (name) VALUES (@name)", new { name = name });
                user = _connection.QueryFirstOrDefault<User>("SELECT * FROM users WHERE name = @name", new { name = name });
            }
            CurrentUser = user!;
        }

        public async Task<(GameSession Session, SaveData SaveData, Campaign Campaign, Chapter Chapter)> Session_EnsureInDB(global::Celeste.Session session)
        {
            var saveData = global::Celeste.SaveData.Instance;
            string saveName = saveData.Name;
            int saveSlot = saveData.FileSlot;
            string levelSet = session.Area.LevelSet;
            string areaSid = session.Area.GetSID();

            // 1. Ensure exists current "SaveData" in DB
            SaveData? saveFileFound = await SaveData_GetSingle(saveName, saveSlot);
            if (saveFileFound is null)
            {
                saveFileFound = await SaveData_InsertSingle(new SaveData { file_name = saveName, slot_number = saveSlot, user_id = CurrentUser.id });
            }

            // 2. Ensure exists current "Campaign" data linked to this "SaveData" in DB
            Campaign? foundActualCampaign = await Campaign_GetSingle(saveFileFound.id, levelSet);
            if (foundActualCampaign is null)
            {
                foundActualCampaign = await Campaign_InsertSingle(saveFileFound.id, levelSet);
            }

            // 3. Ensure exists current "Chapter" (also known as AreaData) linked to "Campaign" that is linked to current "SaveData"
            string expectedChapterSid = $"{foundActualCampaign.id}:{areaSid}";
            Chapter? foundActualChapterLevel = await Chapter_GetSingle(expectedChapterSid);
            if (foundActualChapterLevel is null)
            {
                foundActualChapterLevel = await Chapter_InsertSingle(expectedChapterSid, foundActualCampaign.id);
            }

            // 4. Ensure exists current ChapterSide and sync its berry stats
            int sideAvailableBerries = global::Celeste.AreaData.Get(session.Area).Mode[(int)session.Area.Mode].TotalStrawberries;
            int sideCollectedBerries = global::Celeste.SaveData.Instance.Areas[session.Area.ID].Modes[(int)session.Area.Mode].Strawberries.Count;
            string sideId = session.Area.Mode.ToStringId();

            int sideRowId = await ChapterSide_Upsert(new ChapterSide
            {
                chapter_sid = expectedChapterSid,
                side_id = sideId,
                berries_available = sideAvailableBerries,
                berries_collected = sideCollectedBerries
            });

            // 5. Create new session
            var newSession = GameSession_CreateNew(sideRowId);

            return (newSession, saveFileFound, foundActualCampaign, foundActualChapterLevel);
        }

        public async Task<int> ChapterSide_Upsert(ChapterSide side)
        {
            string sql = @"
                INSERT INTO chapter_sides (chapter_sid, side_id, berries_available, berries_collected)
                VALUES (@chapter_sid, @side_id, @berries_available, @berries_collected)
                ON CONFLICT(chapter_sid, side_id) DO UPDATE SET
                    berries_available = excluded.berries_available,
                    berries_collected = excluded.berries_collected
                RETURNING id;
            ";
            return await _connection.QuerySingleAsync<int>(sql, side);
        }

        public async Task ChapterSide_IncrementBerriesCollected(int id)
        {
            string sql = @"
                UPDATE chapter_sides 
                SET berries_collected = berries_collected + 1
                WHERE id = @id;
            ";
            await _connection.ExecuteAsync(sql, new { id });
        }

        public GameSession GameSession_CreateNew(int chapterSideId)
        {
            return new GameSession
            {
                id = Guid.NewGuid().ToString(),
                chapter_side_id = chapterSideId,
                date_time_start = DateTime.UtcNow,
                is_goldenberry_attempt = false,
                is_goldenberry_completed = false,
                duration_ms = 0,
                room_stats = new Dictionary<string, GameSessionChapterRoomStats>()
            };
        }

        public async Task<SaveData?> SaveData_GetSingle(string fileName, int slotNumber)
        {
            return await _connection.QueryFirstOrDefaultAsync<SaveData>(
                "SELECT * FROM save_datas WHERE file_name = @file_name OR slot_number = @slot_number",
                new { file_name = fileName, slot_number = slotNumber }
            );
        }

        public async Task<SaveData> SaveData_InsertSingle(SaveData saveData)
        {
            string sql = @"
                INSERT INTO save_datas (user_id, slot_number, file_name)
                VALUES (@user_id, @slot_number, @file_name);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, saveData);
            saveData.id = id;
            return saveData;
        }

        public async Task<Campaign?> Campaign_GetSingle(int saveDataId, string campaignNameId)
        {
            return await _connection.QueryFirstOrDefaultAsync<Campaign>(
                "SELECT * FROM campaigns WHERE save_data_id = @save_data_id AND campaign_name_id = @campaign_name_id",
                new { save_data_id = saveDataId, campaign_name_id = campaignNameId }
            );
        }

        public async Task<Campaign> Campaign_InsertSingle(int saveDataId, string campaignNameId)
        {
            var campaign = new Campaign { save_data_id = saveDataId, campaign_name_id = campaignNameId };
            string sql = @"
                INSERT INTO campaigns (save_data_id, campaign_name_id)
                VALUES (@save_data_id, @campaign_name_id);
                SELECT last_insert_rowid();
            ";
            int id = await _connection.ExecuteScalarAsync<int>(sql, campaign);
            campaign.id = id;
            return campaign;
        }

        public async Task<Chapter?> Chapter_GetSingle(string sid)
        {
            return await _connection.QueryFirstOrDefaultAsync<Chapter>(
                "SELECT * FROM chapters WHERE sid = @sid",
                new { sid = sid }
            );
        }

        public async Task<Chapter> Chapter_InsertSingle(string sid, int campaignId)
        {
            var chapter = new Chapter { sid = sid, campaign_id = campaignId };
            string sql = @"
                INSERT INTO chapters (sid, campaign_id)
                VALUES (@sid, @campaign_id)
            ";
            await _connection.ExecuteAsync(sql, chapter);
            return chapter;
        }

        public async Task GameSession_Insert(GameSession session)
        {
            using var transaction = _connection.BeginTransaction();
            try
            {
                var side = await _connection.QuerySingleAsync<ChapterSide>("SELECT * FROM chapter_sides WHERE id = @id", new { id = session.chapter_side_id }, transaction);
                string chapter_sid = side.chapter_sid;

                // Get Chapter Data for metadata lookup (strawberries available, etc.)
                // chapter_sid is treated as "1:Celeste/1-ForsakenCity", '1' being the SaveFileID : the actual SID in-game. (This is how this mod handles chapter sids, not related to celeste internals at all)
                var areaData = global::Celeste.AreaData.Get(chapter_sid.Split(':').LastOrDefault() ?? "");
                var levels = areaData?.Mode[0]?.MapData?.Levels;

                // 1. Ensure all Room/Levels are in the chapter_rooms table
                string chapterRoomsSql = @"
                    INSERT OR IGNORE INTO chapter_rooms (chapter_sid, name, ""order"", strawberries_available)
                    VALUES (@chapter_sid, @name, @order, @strawberries_available)
                ";

                foreach (var stat in session.room_stats.Values)
                {
                    var levelData = levels?.Find(l => l.Name == stat.room_name);
                    await _connection.ExecuteAsync(chapterRoomsSql, new
                    {
                        chapter_sid = chapter_sid,
                        name = stat.room_name,
                        order = levels?.IndexOf(levelData) ?? 0,
                        strawberries_available = levelData?.Strawberries ?? 0
                    }, transaction);
                }

                // 2. Insert the Game Session
                string sessionSql = @"
                    INSERT INTO game_sessions (id, chapter_side_id, date_time_start, duration_ms, is_goldenberry_attempt, is_goldenberry_completed)
                    VALUES (@id, @chapter_side_id, @date_time_start, @duration_ms, @is_goldenberry_attempt, @is_goldenberry_completed)
                ";
                await _connection.ExecuteAsync(sessionSql, new
                {
                    session.id,
                    session.chapter_side_id,
                    date_time_start = session.date_time_start.ToString("o"),
                    session.duration_ms,
                    is_goldenberry_attempt = session.is_goldenberry_attempt ? 1 : 0,
                    is_goldenberry_completed = session.is_goldenberry_completed ? 1 : 0
                }, transaction);

                // 3. Insert all Room Stats
                string statsSql = @"
                    INSERT INTO game_session_chapter_room_stats (gamesession_id, room_name, visited_order, deaths_in_room, dashes_in_room, strawberries_achieved_in_room, hearts_achieved_in_room, jumps_in_room)
                    VALUES (@gamesession_id, @room_name, @visited_order, @deaths_in_room, @dashes_in_room, @strawberries_achieved_in_room, @hearts_achieved_in_room, @jumps_in_room);
                ";
                foreach (var stat in session.room_stats.Values)
                {
                    await _connection.ExecuteAsync(statsSql, stat, transaction);
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.Log($"Error inserting game session: {ex.Message}");
                throw;
            }
        }

        public async Task GameSessionStat_InsertSingle(GameSessionChapterRoomStats stat)
        {
            string sql = @"
                INSERT INTO game_session_chapter_room_stats (gamesession_id, room_name, visited_order, deaths_in_room, dashes_in_room, strawberries_achieved_in_room, hearts_achieved_in_room)
                VALUES (@gamesession_id, @room_name, @visited_order, @deaths_in_room, @dashes_in_room, @strawberries_achieved_in_room, @hearts_achieved_in_room);
            ";
            await _connection.ExecuteAsync(sql, stat);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }
}
//   }
//    }
//}
