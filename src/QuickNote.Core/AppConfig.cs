using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuickNote.Core
{
    /// <summary>
    /// 手写的 key=value 配置读写。不引第三方配置库——四行配置不值得多一个依赖。
    /// </summary>
    public sealed class AppConfig
    {
        public const string ModeDoubleTap = "DoubleTapCtrl";
        public const string ModeCombo = "Combo";

        public string NotesDir { get; set; }
        public string HotkeyMode { get; set; }
        public int DoubleTapMs { get; set; }
        public string FallbackHotkey { get; set; }
        public string Theme { get; set; }
        public bool AutoStart { get; set; }

        public AppConfig()
        {
            NotesDir = DefaultNotesDir();
            HotkeyMode = ModeDoubleTap;
            DoubleTapMs = 300;
            FallbackHotkey = "Ctrl+Alt+N";
            Theme = "System";
            AutoStart = true;
        }

        public static string AppDataDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "QuickNote");
        }

        public static string DefaultConfigPath()
        {
            return Path.Combine(AppDataDir(), "config.ini");
        }

        public static string DefaultNotesDir()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "QuickNote");
        }

        public static AppConfig Load(string path)
        {
            var cfg = new AppConfig();
            if (!File.Exists(path)) return cfg;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                BackupBad(path);
                return new AppConfig();
            }

            var parsed = new List<KeyValuePair<string, string>>();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    // 不是 key=value —— 整个文件视为损坏，回退默认值。
                    BackupBad(path);
                    return new AppConfig();
                }
                parsed.Add(new KeyValuePair<string, string>(
                    line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }

            foreach (var kv in parsed)
            {
                switch (kv.Key)
                {
                    case "NotesDir":
                        if (kv.Value.Length > 0) cfg.NotesDir = kv.Value;
                        break;
                    case "HotkeyMode":
                        if (kv.Value == ModeDoubleTap || kv.Value == ModeCombo) cfg.HotkeyMode = kv.Value;
                        break;
                    case "DoubleTapMs":
                        {
                            int ms;
                            if (int.TryParse(kv.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms)
                                && ms >= 50 && ms <= 2000)
                            {
                                cfg.DoubleTapMs = ms;
                            }
                            break;
                        }
                    case "FallbackHotkey":
                        if (kv.Value.Length > 0) cfg.FallbackHotkey = kv.Value;
                        break;
                    case "Theme":
                        if (kv.Value == "System" || kv.Value == "Light" || kv.Value == "Dark") cfg.Theme = kv.Value;
                        break;
                    case "AutoStart":
                        {
                            bool b;
                            if (bool.TryParse(kv.Value, out b)) cfg.AutoStart = b;
                            break;
                        }
                }
            }
            return cfg;
        }

        public void Save(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.Append("# QuickNote 配置，改完重启生效\n");
            sb.Append("NotesDir=").Append(NotesDir).Append('\n');
            sb.Append("HotkeyMode=").Append(HotkeyMode).Append('\n');
            sb.Append("DoubleTapMs=").Append(DoubleTapMs.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("FallbackHotkey=").Append(FallbackHotkey).Append('\n');
            sb.Append("Theme=").Append(Theme).Append('\n');
            sb.Append("AutoStart=").Append(AutoStart ? "true" : "false").Append('\n');

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void BackupBad(string path)
        {
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".bad", true);
            }
            catch
            {
                // 备份失败不能妨碍程序带着默认值启动
            }
        }
    }
}
