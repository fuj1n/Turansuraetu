using System.IO;
using Newtonsoft.Json;

namespace Turansuraetu
{
    public class ConfigFile
    {
        public const string Location = "config.json";

        public static ConfigFile Instance => _instance ??= Load();
        private static ConfigFile _instance;

        public Project project;
        public RpgMakerTrans rpgMakerTrans;

        private ConfigFile()
        {
        }

        private static ConfigFile Load()
        {
            ConfigFile config = new ConfigFile();
            if(File.Exists(Location))
                JsonConvert.PopulateObject(File.ReadAllText(Location), config);

            return config;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Location)));
            File.WriteAllText(Location, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public struct Project
        {
            public string lastOpenProjectPath;
        }

        public struct RpgMakerTrans
        {
            public string path;
        }
    }
}
