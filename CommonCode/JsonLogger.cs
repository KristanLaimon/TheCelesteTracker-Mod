using Newtonsoft.Json;

namespace CommonCode
{
    public class JsonLogger : ISimpleLogger
    {
        public void Log(object anything)
        {
            string asJsonStr = JsonConvert.SerializeObject(anything, Formatting.Indented);
            Console.WriteLine(asJsonStr);
        }

        public void LogToFile(string fileFullPath, object toJsonEncode)
        {
            string asJsonStr = JsonConvert.SerializeObject(toJsonEncode, Formatting.Indented);
            File.WriteAllText(fileFullPath, asJsonStr);
        }
    }
}
