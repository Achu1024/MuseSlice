using System;
using System.IO;
using System.Text;
using MuseSlice.Core;

namespace MuseSlice.SelfCheck
{
    internal static class AppConfigChecks
    {
        public static void Run()
        {
            Check.Section("AppConfig");
            RoundTrip();
            MissingFileGivesDefaults();
            CorruptFileIsBackedUp();
            BadNumbersFallBackToDefault();
        }

        private static void RoundTrip()
        {
            using (var dir = new TempDir())
            {
                var path = Path.Combine(dir.Path, "config.ini");
                var c = new AppConfig();
                c.NotesDir = @"D:\Project\daliy_log\notes";
                c.HotkeyMode = "Combo";
                c.DoubleTapMs = 250;
                c.FallbackHotkey = "Ctrl+Alt+J";
                c.Theme = "Dark";
                c.AutoStart = false;
                c.Save(path);

                var back = AppConfig.Load(path);

                Check.Equal(back.NotesDir, @"D:\Project\daliy_log\notes", "NotesDir 往返一致");
                Check.Equal(back.HotkeyMode, "Combo", "HotkeyMode 往返一致");
                Check.Equal(back.DoubleTapMs, 250, "DoubleTapMs 往返一致");
                Check.Equal(back.FallbackHotkey, "Ctrl+Alt+J", "FallbackHotkey 往返一致");
                Check.Equal(back.Theme, "Dark", "Theme 往返一致");
                Check.Equal(back.AutoStart, false, "AutoStart 往返一致");
            }
        }

        private static void MissingFileGivesDefaults()
        {
            using (var dir = new TempDir())
            {
                var c = AppConfig.Load(Path.Combine(dir.Path, "不存在.ini"));

                Check.Equal(c.DoubleTapMs, 300, "文件不存在时用默认阈值 300ms");
                Check.Equal(c.HotkeyMode, "DoubleTapCtrl", "文件不存在时默认双击左 Ctrl");
                Check.Equal(c.FallbackHotkey, "Ctrl+Alt+N", "文件不存在时默认备用键");
                Check.Equal(c.Theme, "System", "文件不存在时默认跟随系统");
                Check.Equal(c.AutoStart, true, "文件不存在时默认开启自启");
                Check.That(c.NotesDir.Length > 0, "文件不存在时有默认笔记目录");
            }
        }

        private static void CorruptFileIsBackedUp()
        {
            using (var dir = new TempDir())
            {
                var path = Path.Combine(dir.Path, "config.ini");
                File.WriteAllText(path, "这一行根本不是配置\n还有这一行", Encoding.UTF8);

                var c = AppConfig.Load(path);

                Check.That(File.Exists(path + ".bad"), "损坏的配置被备份为 config.ini.bad");
                Check.Equal(c.DoubleTapMs, 300, "损坏后回退默认值");
            }
        }

        private static void BadNumbersFallBackToDefault()
        {
            using (var dir = new TempDir())
            {
                var path = Path.Combine(dir.Path, "config.ini");
                File.WriteAllText(path, "DoubleTapMs=abc\nTheme=Dark\n", Encoding.UTF8);

                var c = AppConfig.Load(path);

                Check.Equal(c.DoubleTapMs, 300, "非法数字回退默认值");
                Check.Equal(c.Theme, "Dark", "同一文件里合法的项仍然生效");
            }
        }
    }
}
