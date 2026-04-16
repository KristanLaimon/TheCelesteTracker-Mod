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
    }
}
