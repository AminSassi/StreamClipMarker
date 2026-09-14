using System;
using System.IO;
using System.Text;

namespace StreamClipMarker.Core
{
    public class AppConfig
    {
        public string HotkeyKey { get; set; }
        public bool HotkeyCtrl { get; set; }
        public bool HotkeyAlt { get; set; }
        public bool HotkeyShift { get; set; }

        public int RecordingOffsetSeconds { get; set; }

        public bool EnableCountdown { get; set; }
        public int CountdownSeconds { get; set; }

        public string OutputFolder { get; set; }
        public bool EnableSound { get; set; }
        public bool EnableToast { get; set; }

        // Auto-detection features
        public bool AutoDetectRecording { get; set; }
        public bool AutoEndRecording { get; set; }
        public string WatchedRecordingFolder { get; set; }
        public bool MinimizeToTrayOnClose { get; set; }

        public AppConfig()
        {
            // Default configuration
            HotkeyKey = "F8";
            HotkeyCtrl = false;
            HotkeyAlt = false;
            HotkeyShift = false;

            RecordingOffsetSeconds = 0;

            EnableCountdown = true;
            CountdownSeconds = 3;

            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            OutputFolder = Path.Combine(myDocs, "StreamClipMarker");

            EnableSound = true;
            EnableToast = true;

            AutoDetectRecording = true;
            AutoEndRecording = true;
            WatchedRecordingFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            MinimizeToTrayOnClose = true;
        }

        public string GetHotkeyDisplayString()
        {
            StringBuilder sb = new StringBuilder();
            if (HotkeyCtrl) sb.Append("Ctrl + ");
            if (HotkeyAlt) sb.Append("Alt + ");
            if (HotkeyShift) sb.Append("Shift + ");
            sb.Append(HotkeyKey);
            return sb.ToString();
        }

        public static string GetConfigPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "StreamClipMarker");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "config.json");
        }

        public static AppConfig Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                AppConfig defaultCfg = new AppConfig();
                defaultCfg.Save();
                return defaultCfg;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                AppConfig cfg = new AppConfig();

                cfg.HotkeyKey = JsonHelper.ExtractString(json, "HotkeyKey", "F8");
                cfg.HotkeyCtrl = JsonHelper.ExtractBool(json, "HotkeyCtrl", false);
                cfg.HotkeyAlt = JsonHelper.ExtractBool(json, "HotkeyAlt", false);
                cfg.HotkeyShift = JsonHelper.ExtractBool(json, "HotkeyShift", false);

                cfg.RecordingOffsetSeconds = JsonHelper.ExtractInt(json, "RecordingOffsetSeconds", 0);

                cfg.EnableCountdown = JsonHelper.ExtractBool(json, "EnableCountdown", true);
                cfg.CountdownSeconds = JsonHelper.ExtractInt(json, "CountdownSeconds", 3);

                string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string defaultOutput = Path.Combine(myDocs, "StreamClipMarker");
                cfg.OutputFolder = JsonHelper.ExtractString(json, "OutputFolder", defaultOutput);

                cfg.EnableSound = JsonHelper.ExtractBool(json, "EnableSound", true);
                cfg.EnableToast = JsonHelper.ExtractBool(json, "EnableToast", true);

                cfg.AutoDetectRecording = JsonHelper.ExtractBool(json, "AutoDetectRecording", true);
                cfg.AutoEndRecording = JsonHelper.ExtractBool(json, "AutoEndRecording", true);
                string defaultVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                cfg.WatchedRecordingFolder = JsonHelper.ExtractString(json, "WatchedRecordingFolder", defaultVideos);
                cfg.MinimizeToTrayOnClose = JsonHelper.ExtractBool(json, "MinimizeToTrayOnClose", true);

                return cfg;
            }
            catch
            {
                return new AppConfig();
            }
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine(string.Format("  \"HotkeyKey\": \"{0}\",", JsonHelper.Escape(HotkeyKey)));
                sb.AppendLine(string.Format("  \"HotkeyCtrl\": {0},", HotkeyCtrl ? "true" : "false"));
                sb.AppendLine(string.Format("  \"HotkeyAlt\": {0},", HotkeyAlt ? "true" : "false"));
                sb.AppendLine(string.Format("  \"HotkeyShift\": {0},", HotkeyShift ? "true" : "false"));
                sb.AppendLine(string.Format("  \"RecordingOffsetSeconds\": {0},", RecordingOffsetSeconds));
                sb.AppendLine(string.Format("  \"EnableCountdown\": {0},", EnableCountdown ? "true" : "false"));
                sb.AppendLine(string.Format("  \"CountdownSeconds\": {0},", CountdownSeconds));
                sb.AppendLine(string.Format("  \"OutputFolder\": \"{0}\",", JsonHelper.Escape(OutputFolder)));
                sb.AppendLine(string.Format("  \"EnableSound\": {0},", EnableSound ? "true" : "false"));
                sb.AppendLine(string.Format("  \"EnableToast\": {0},", EnableToast ? "true" : "false"));
                sb.AppendLine(string.Format("  \"AutoDetectRecording\": {0},", AutoDetectRecording ? "true" : "false"));
                sb.AppendLine(string.Format("  \"AutoEndRecording\": {0},", AutoEndRecording ? "true" : "false"));
                sb.AppendLine(string.Format("  \"WatchedRecordingFolder\": \"{0}\",", JsonHelper.Escape(WatchedRecordingFolder)));
                sb.AppendLine(string.Format("  \"MinimizeToTrayOnClose\": {0}", MinimizeToTrayOnClose ? "true" : "false"));
                sb.AppendLine("}");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
                // Fallback
            }
        }
    }
}
