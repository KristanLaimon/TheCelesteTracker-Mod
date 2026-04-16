using CommonCode;
using TheCelesteTracker_Database;

public class CelesteTrackerDb
{
    private DatabaseContext _db;
    private ISimpleLogger? _logger;
    private string _path;
    private User? CurrentUser;

    public CelesteTrackerDb(string path, ISimpleLogger? loggerToUse = null)
    {
        _db = new DatabaseContext(path);
        _path = path;
        _logger = loggerToUse;
        //hola
    }

    public async Task InitializeDb()
    {
        if (File.Exists(_path))
        {
            return;
        }

        await _db.Database.EnsureCreatedAsync();
        await SetDefaultUser();
        await _db.SaveChangesAsync();
    }

    private async Task SetDefaultUser()
    {
        var newUser = new User { Name = "Celeste Climber" };
        await _db.Users.AddAsync(newUser);
        _db.ChangeTracker.Clear();
        CurrentUser = newUser;

    }

    public async Task ResetDatabase()
    {
        _db.Dispose();

        using (var tempContext = new DatabaseContext(_path))
        {
            // 2. Perform the wipe
            await tempContext.Database.EnsureDeletedAsync();
            await tempContext.Database.EnsureCreatedAsync();

            // Seed
            await SetDefaultUser();
            await tempContext.SaveChangesAsync();
        }

        // 3. RE-INITIALIZE the main connection
        _db = new DatabaseContext(_path);

        _logger?.Log("Database reset. Internal tracker cache cleared.");
    }

    // Your other methods use the current _db instance
    public async Task AddDeath(string room)
    {
        await _db.RoomDeaths.AddAsync(new RoomDeath { RoomName = room, Deaths = 1 });
        await _db.SaveChangesAsync();
    }

    public async Task AddFullSaveData(XMLSaveData saveFile, int slotNumber)
    {
        // Slot
        await _db.Saves.AddAsync(new SaveData { SlotNumber = slotNumber, FileName = saveFile.Name, })
    }
}