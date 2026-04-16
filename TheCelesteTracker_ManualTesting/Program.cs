//var CelesteSaveFiles = new CelesteSaveFiles();
//Logger.Log(CelesteSaveFiles.Vanilla_GetSaveFiles().ElementAt(1).LastArea);


using CommonCode;

internal class Program
{
    private static async Task Main(string[] args)
    {
        JsonLogger l = new();
        string dbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TheCelesteTracker_TEST_DB.db");
        //string dbPath = Path.Join(Utils.GetCelestePath(), "Saves - TESTCOPY", "");
        var db = new CelesteTrackerDb(dbPath, loggerToUse: new JsonLogger());
        await db.InitializeDb();
        l.Log(File.Exists(dbPath));
    }
}