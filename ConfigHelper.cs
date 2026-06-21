using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ArtaleProBuff
{
    public class PresetData
    {
        public string global_delay { get; set; }
        public bool bg_enabled { get; set; }
        public string bg_title { get; set; }
        public bool exp_enabled { get; set; }
        public bool exp_close_game { get; set; }
        public string exp_time { get; set; }
        public List<BuffCardViewModel> cards { get; set; }
        public List<PatrolGroupViewModel> patrol_groups { get; set; }
        public bool patrol_pause_others { get; set; }
        public string patrol_fluct { get; set; }
    }

    public class AppConfig
    {
        public string global_delay { get; set; } = "0";
        public bool bg_enabled { get; set; } = false;
        public string bg_title { get; set; } = "MapleStory Worlds";
        public string theme { get; set; } = "dark";
        public bool exp_enabled { get; set; } = false;
        public bool exp_close_game { get; set; } = false;
        public string exp_time { get; set; } = "15";
        public List<BuffCardViewModel> cards { get; set; } = new List<BuffCardViewModel>();
        public List<PatrolGroupViewModel> patrol_groups { get; set; } = new List<PatrolGroupViewModel>();
        public bool patrol_pause_others { get; set; } = true;
        public string patrol_fluct { get; set; } = "10";
        public Dictionary<string, PresetData> presets { get; set; } = new Dictionary<string, PresetData>();
    }

    public static class ConfigHelper
    {
        public static string GetConfigPath()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(exeDir, "config.json");
        }

        public static AppConfig Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                return new AppConfig();
            }
            try
            {
                string json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(GetConfigPath(), json);
            }
            catch { }
        }
    }
}
