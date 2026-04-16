using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace TheCelesteTracker_Database
{
    public class CelesteSaveFiles
    {
        private string _saveFilesFolder;
        private Regex _vanillaSaveFileNameRegex = new Regex("^(\\d|debug)\\.celeste$");
        private XmlSerializer _xmlSaveDataSerializer = new XmlSerializer(typeof(SaveData));

        public CelesteSaveFiles(string saveFilesFolder)
        {
            if (!Directory.Exists(saveFilesFolder))
                throw new ArgumentException($"[Class: CelesteSaveFiles, Method: *constructor]: Bad args, Save FileFolder passed to constructor doesn't exist!, Value: {saveFilesFolder}");
            _saveFilesFolder = saveFilesFolder;
        }

        public XMLSaveData[] Vanilla_GetSaveFiles()
        {
            IEnumerable<string>? allFilesInSafeFolder = Directory.EnumerateFiles(_saveFilesFolder);

            if (allFilesInSafeFolder is null)
                return [];

            if (allFilesInSafeFolder.Count() == 0)
                return [];

            IEnumerable<string> allCelesteVanillaFiles = allFilesInSafeFolder
               .Where(fullPath => _vanillaSaveFileNameRegex.IsMatch(Path.GetFileName(fullPath)));

            SaveData[] allVanillaSaves = allCelesteVanillaFiles
               .Select(fullPath =>
                {
                    try
                    {
                        using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            return (SaveData?)_xmlSaveDataSerializer.Deserialize(fs);
                    }
                    catch (IOException)
                    {
                        // The game is probably writing to this file right now.
                        //Logger.Log($"File locked by Celeste, skipping for now: {fullPath}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        // Extraemos el error específico que normalmente está oculto en la InnerException
                        string specificError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                        //Logger.Log($"XML Error at {fullPath}: {specificError}");
                        return null; // O return null as SaveData; dependiendo de cómo dejaste el código anterior
                    }
                })
                .OfType<SaveData>()
                .ToArray();
            return allVanillaSaves;
        }
    }

    //================ SAVE FILE CELESTE XML TYPE DEFINITIONS =====================
    [XmlRoot(ElementName = "Assists")]
    public class Assists
    {

        [XmlElement(ElementName = "GameSpeed")]
        public long GameSpeed { get; set; }

        [XmlElement(ElementName = "Invincible")]
        public bool Invincible { get; set; }

        [XmlElement(ElementName = "DashMode")]
        public string DashMode { get; set; }

        [XmlElement(ElementName = "DashAssist")]
        public bool DashAssist { get; set; }

        [XmlElement(ElementName = "InfiniteStamina")]
        public bool InfiniteStamina { get; set; }

        [XmlElement(ElementName = "MirrorMode")]
        public bool MirrorMode { get; set; }

        [XmlElement(ElementName = "ThreeSixtyDashing")]
        public bool ThreeSixtyDashing { get; set; }

        [XmlElement(ElementName = "InvisibleMotion")]
        public bool InvisibleMotion { get; set; }

        [XmlElement(ElementName = "NoGrabbing")]
        public bool NoGrabbing { get; set; }

        [XmlElement(ElementName = "LowFriction")]
        public bool LowFriction { get; set; }

        [XmlElement(ElementName = "SuperDashing")]
        public bool SuperDashing { get; set; }

        [XmlElement(ElementName = "Hiccups")]
        public bool Hiccups { get; set; }

        [XmlElement(ElementName = "PlayAsBadeline")]
        public bool PlayAsBadeline { get; set; }
    }

    [XmlRoot(ElementName = "Flags")]
    public class Flags
    {

        [XmlElement(ElementName = "string")]
        public List<string> String { get; set; }
    }

    [XmlRoot(ElementName = "Poem")]
    public class Poem
    {

        [XmlElement(ElementName = "string")]
        public List<string> String { get; set; }
    }

    [XmlRoot(ElementName = "SummitGems")]
    public class SummitGems
    {

        [XmlElement(ElementName = "boolean")]
        public List<bool> Boolean { get; set; }
    }

    [XmlRoot(ElementName = "LastArea")]
    public class LastArea
    {

        [XmlAttribute(AttributeName = "ID")]
        public int ID { get; set; }

        [XmlAttribute(AttributeName = "Mode")]
        public required string Mode { get; set; }

        [XmlAttribute(AttributeName = "SID")]
        public string SID { get; set; }
    }

    [XmlRoot(ElementName = "AreaModeStats")]
    public class AreaModeStats
    {

        [XmlElement(ElementName = "Strawberries")]
        public Strawberries Strawberries { get; set; }

        [XmlElement(ElementName = "Checkpoints")]
        public Checkpoints Checkpoints { get; set; }

        [XmlAttribute(AttributeName = "TotalStrawberries")]
        public long TotalStrawberries { get; set; }

        [XmlAttribute(AttributeName = "Completed")]
        public bool Completed { get; set; }

        [XmlAttribute(AttributeName = "SingleRunCompleted")]
        public bool SingleRunCompleted { get; set; }

        [XmlAttribute(AttributeName = "FullClear")]
        public bool FullClear { get; set; }

        [XmlAttribute(AttributeName = "Deaths")]
        public long Deaths { get; set; }

        [XmlAttribute(AttributeName = "TimePlayed")]
        public long TimePlayed { get; set; }

        [XmlAttribute(AttributeName = "BestTime")]
        public long BestTime { get; set; }

        [XmlAttribute(AttributeName = "BestFullClearTime")]
        public long BestFullClearTime { get; set; }

        [XmlAttribute(AttributeName = "BestDashes")]
        public long BestDashes { get; set; }

        [XmlAttribute(AttributeName = "BestDeaths")]
        public long BestDeaths { get; set; }

        [XmlAttribute(AttributeName = "HeartGem")]
        public bool HeartGem { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "Modes")]
    public class Modes
    {

        [XmlElement(ElementName = "AreaModeStats")]
        public List<AreaModeStats> AreaModeStats { get; set; }
    }

    [XmlRoot(ElementName = "AreaStats")]
    public class AreaStats
    {

        [XmlElement(ElementName = "Modes")]
        public Modes Modes { get; set; }

        [XmlAttribute(AttributeName = "ID")]
        public int ID { get; set; }

        [XmlAttribute(AttributeName = "Cassette")]
        public bool Cassette { get; set; }

        [XmlAttribute(AttributeName = "SID")]
        public string SID { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "EntityID")]
    public class EntityID
    {

        [XmlAttribute(AttributeName = "Key")]
        public required string Key { get; set; }
    }

    [XmlRoot(ElementName = "Strawberries")]
    public class Strawberries
    {

        [XmlElement(ElementName = "EntityID")]
        public List<EntityID> EntityID { get; set; }
    }

    [XmlRoot(ElementName = "Checkpoints")]
    public class Checkpoints
    {

        [XmlElement(ElementName = "string")]
        public List<string> String { get; set; }
    }

    [XmlRoot(ElementName = "Areas")]
    public class Areas
    {

        [XmlElement(ElementName = "AreaStats")]
        public List<AreaStats> AreaStats { get; set; }
    }

    [XmlRoot(ElementName = "LevelSetStats")]
    public class LevelSetStats
    {

        [XmlElement(ElementName = "Areas")]
        public Areas Areas { get; set; }

        [XmlElement(ElementName = "Poem")]
        public Poem Poem { get; set; }

        [XmlElement(ElementName = "UnlockedAreas")]
        public int UnlockedAreas { get; set; }

        [XmlElement(ElementName = "TotalStrawberries")]
        public long TotalStrawberries { get; set; }

        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "LevelSets")]
    public class LevelSets
    {

        [XmlElement(ElementName = "LevelSetStats")]
        public List<LevelSetStats> LevelSetStats { get; set; }
    }

    [XmlRoot(ElementName = "LevelSetRecycleBin")]
    public class LevelSetRecycleBin
    {

        [XmlElement(ElementName = "LevelSetStats")]
        public List<LevelSetStats> LevelSetStats { get; set; }
    }

    [XmlRoot(ElementName = "LastArea_Safe")]
    public class LastAreaSafe
    {

        [XmlAttribute(AttributeName = "ID")]
        public int ID { get; set; }

        [XmlAttribute(AttributeName = "Mode")]
        public string Mode { get; set; }

        [XmlAttribute(AttributeName = "SID")]
        public string SID { get; set; }
    }

    [XmlRoot(ElementName = "SaveData")]
    public class XMLSaveData
    {

        [XmlElement(ElementName = "Version")]
        public string Version { get; set; }

        [XmlElement(ElementName = "Name")]
        public string Name { get; set; }

        [XmlElement(ElementName = "Time")]
        public long Time { get; set; }

        [XmlElement(ElementName = "LastSave")]
        public DateTime LastSave { get; set; }

        [XmlElement(ElementName = "CheatMode")]
        public bool CheatMode { get; set; }

        [XmlElement(ElementName = "AssistMode")]
        public bool AssistMode { get; set; }

        [XmlElement(ElementName = "VariantMode")]
        public bool VariantMode { get; set; }

        [XmlElement(ElementName = "Assists")]
        public Assists Assists { get; set; }

        [XmlElement(ElementName = "TheoSisterName")]
        public string TheoSisterName { get; set; }

        [XmlElement(ElementName = "UnlockedAreas")]
        public int UnlockedAreas { get; set; }

        [XmlElement(ElementName = "TotalDeaths")]
        public long TotalDeaths { get; set; }

        [XmlElement(ElementName = "TotalStrawberries")]
        public long TotalStrawberries { get; set; }

        [XmlElement(ElementName = "TotalGoldenStrawberries")]
        public long TotalGoldenStrawberries { get; set; }

        [XmlElement(ElementName = "TotalJumps")]
        public long TotalJumps { get; set; }

        [XmlElement(ElementName = "TotalWallJumps")]
        public long TotalWallJumps { get; set; }

        [XmlElement(ElementName = "TotalDashes")]
        public long TotalDashes { get; set; }

        [XmlElement(ElementName = "Flags")]
        public Flags Flags { get; set; }

        [XmlElement(ElementName = "Poem")]
        public Poem Poem { get; set; }

        [XmlElement(ElementName = "SummitGems")]
        public required SummitGems SummitGems { get; set; }

        [XmlElement(ElementName = "RevealedChapter9")]
        public bool RevealedChapter9 { get; set; }

        [XmlElement(ElementName = "LastArea")]
        public required LastArea LastArea { get; set; }

        [XmlElement(ElementName = "Areas")]
        public required Areas Areas { get; set; }

        [XmlElement(ElementName = "LevelSets")]
        public required LevelSets LevelSets { get; set; }

        [XmlElement(ElementName = "LevelSetRecycleBin")]
        public required LevelSetRecycleBin LevelSetRecycleBin { get; set; }

        [XmlElement(ElementName = "HasModdedSaveData")]
        public bool HasModdedSaveData { get; set; }

        [XmlElement(ElementName = "LastArea_Safe")]
        public required LastAreaSafe LastAreaSafe { get; set; }

        [XmlAttribute(AttributeName = "xsi")]
        public required string Xsi { get; set; }

        [XmlAttribute(AttributeName = "xsd")]
        public required string Xsd { get; set; }

        [XmlText]
        public string? Text { get; set; }
    }


}
