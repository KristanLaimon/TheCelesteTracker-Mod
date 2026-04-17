

using CommonCode;
using TheCelesteTracker_Database;
using TheCelesteTracker_ManualTesting;

internal class Program
{
    private static async Task Main(string[] args)
    {
        JsonLogger l = new();
        string dbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "TheCelesteTracker_TEST_DB.db");
        var db = new CelesteTrackerDb(dbPath, loggerToUse: new JsonLogger());
        await db.ResetDatabase();

        string logOuputFileJson = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "datasave.json");
        var CelesteSaveFiles = new CelesteSaveFiles(Path.Join(Utils.GetCelestePath(), "Saves - TESTCOPY"));
        l.LogToFile(logOuputFileJson, CelesteSaveFiles.Vanilla_GetSaveFiles().ElementAt(1));
        Console.WriteLine("Ready");
    }
}