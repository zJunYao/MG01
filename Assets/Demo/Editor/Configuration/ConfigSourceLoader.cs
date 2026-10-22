using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MG01.Demo.Configuration;

namespace MG01.Demo.Editor
{
    public static class ConfigSourceLoader
    {
        public const string SourceDirectory = "Assets/Demo/ConfigSource";
        public static DemoConfigCatalog Load(string directory = SourceDirectory)
        {
            return new DemoConfigCatalog(ConfigSchema.All.Select(schema =>
                new KeyValuePair<string, IReadOnlyList<ConfigRecord>>(schema.Name,
                    CsvConfigReader.Read(schema, File.ReadAllText(Path.Combine(directory, schema.Name + ".csv"), new UTF8Encoding(false, true))))));
        }
    }
}
