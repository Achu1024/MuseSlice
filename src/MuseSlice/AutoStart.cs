using System;
using System.Reflection;
using Microsoft.Win32;

namespace MuseSlice
{
    /// <summary>
    /// 注册表 Run 键自启。不需要管理员权限，而且会出现在任务管理器的
    /// 「启动」选项卡里——使用者能自己看到、自己禁用，这是优点不是缺点。
    /// </summary>
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MuseSlice";

        /// <summary>
        /// 自启命令里附带的参数。程序靠它区分「开机自动跑」与「使用者双击」——
        /// 前者不该弹启动动画。注册表里旧的那条没有这个参数，但
        /// <see cref="EnsurePathCurrent"/> 每次启动都会比对整串命令，下次就顺手补上了。
        /// </summary>
        public const string Flag = "--autostart";

        private static string ExePath()
        {
            return Assembly.GetEntryAssembly().Location;
        }

        private static string Quoted() { return "\"" + ExePath() + "\" " + Flag; }

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                // CreateSubKey 而不是 OpenSubKey(..., true)：Run 键万一不存在也不会静默失效
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    key.SetValue(ValueName, Quoted());
                }
            }
            catch (Exception ex) { MuseSliceApp.Log("写入自启项失败：" + ex.Message); }
        }

        public static void Disable()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { MuseSliceApp.Log("删除自启项失败：" + ex.Message); }
        }

        /// <summary>
        /// 注册表里存的是 exe 的绝对路径。程序挪了位置后自启会静默失效，
        /// 所以每次启动都自查一遍，不一致就改回来。
        /// 返回 true 表示做了修正。
        /// </summary>
        public static bool EnsurePathCurrent()
        {
            try
            {
                if (!IsEnabled()) return false;

                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return false;
                    var stored = key.GetValue(ValueName) as string;
                    if (string.Equals(stored, Quoted(), StringComparison.OrdinalIgnoreCase)) return false;

                    key.SetValue(ValueName, Quoted());
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
