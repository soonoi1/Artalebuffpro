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
        public int exp_crop_x { get; set; }
        public int exp_crop_y { get; set; }
        public int exp_crop_w { get; set; }
        public int exp_crop_h { get; set; }
        public List<BuffCardViewModel> cards { get; set; }
        public List<PatrolGroupViewModel> patrol_groups { get; set; }
        public bool patrol_pause_others { get; set; }
        public string patrol_fluct { get; set; }
    }

    public class ChannelClickStep : ViewModelBase
    {
        private string _name = "步骤";
        private int _x = 0;
        private int _y = 0;
        private double _delaySeconds = 0.5;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public int X
        {
            get => _x;
            set => SetField(ref _x, value);
        }

        public int Y
        {
            get => _y;
            set => SetField(ref _y, value);
        }

        public double DelaySeconds
        {
            get => _delaySeconds;
            set => SetField(ref _delaySeconds, value);
        }
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
        public int exp_crop_x { get; set; } = 0;
        public int exp_crop_y { get; set; } = 0;
        public int exp_crop_w { get; set; } = 0;
        public int exp_crop_h { get; set; } = 0;
        public List<BuffCardViewModel> cards { get; set; } = new List<BuffCardViewModel>();
        public List<PatrolGroupViewModel> patrol_groups { get; set; } = new List<PatrolGroupViewModel>();
        public bool patrol_pause_others { get; set; } = true;
        public string patrol_fluct { get; set; } = "10";
        public Dictionary<string, PresetData> presets { get; set; } = new Dictionary<string, PresetData>();
        public Dictionary<string, double> boss_hunt_map_exp { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, List<double>> boss_hunt_map_exps { get; set; } = new Dictionary<string, List<double>>();
        public bool channel_macro_enabled { get; set; } = false;
        public bool channel_macro_alt { get; set; } = true;
        public bool channel_macro_ctrl { get; set; } = false;
        public bool channel_macro_shift { get; set; } = false;
        public string channel_macro_key { get; set; } = "F12";
        public int channel_click1_x { get; set; } = 965;
        public int channel_click1_y { get; set; } = 105;
        public int channel_click2_x { get; set; } = 285;
        public int channel_click2_y { get; set; } = 540;
        public int channel_click3_x { get; set; } = 435;
        public int channel_click3_y { get; set; } = 405;
        public List<ChannelClickStep> channel_click_steps { get; set; } = new List<ChannelClickStep>();
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
