using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    internal sealed class QuickNoteApp : ApplicationContext
    {
        private readonly AppConfig _config;
        private readonly TrayIcon _tray;
        private readonly MessageWindow _messageWindow;
        private HotkeyManager _hotkeys;
        private CaptureBar _bar;

        public QuickNoteApp()
        {
            _config = AppConfig.Load(AppConfig.DefaultConfigPath());

            var showMessage = NativeMethods.RegisterWindowMessage("QuickNote.Show");
            _messageWindow = new MessageWindow(showMessage);
            _messageWindow.ShowRequested += ShowBar;   // 第二个实例被启动时由这里弹出小条

            _tray = new TrayIcon(_config.AutoStart);
            _tray.ShowRequested += ShowBar;
            _tray.OpenTodayRequested += OpenToday;
            _tray.OpenNotesFolderRequested += OpenNotesFolder;
            _tray.ExitRequested += ExitApp;

            _bar = new CaptureBar(Theme.Current(_config.Theme));

            _hotkeys = new HotkeyManager(_config);
            _hotkeys.Degraded += delegate (string reason)
            {
                _tray.Balloon("QuickNote 热键降级", reason);
            };
            _hotkeys.Start(ShowBar);
        }

        private void ShowBar()
        {
            if (_bar == null) return;
            _bar.ShowBar();
        }

        private void OpenToday()
        {
            var path = Path.Combine(_config.NotesDir, DateFormat.FileName(DateTime.Now) + ".md");
            if (!File.Exists(path)) File.WriteAllText(path, "", new System.Text.UTF8Encoding(false));
            Open(path);
        }

        private void OpenNotesFolder()
        {
            Directory.CreateDirectory(_config.NotesDir);
            Open(_config.NotesDir);
        }

        private static void Open(string path)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Log("打开失败 " + path + "：" + ex.Message); }
        }

        private void ExitApp()
        {
            // Task 11 会给它加上「有未保存草稿时先询问」。
            ExitThread();
        }

        public static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(AppConfig.AppDataDir());
                File.AppendAllText(Path.Combine(AppConfig.AppDataDir(), "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                    new System.Text.UTF8Encoding(false));
            }
            catch { /* 记日志失败绝不能反过来把程序搞崩 */ }
        }

        protected override void ExitThreadCore()
        {
            if (_hotkeys != null) _hotkeys.Dispose();
            if (_tray != null) _tray.Dispose();
            if (_messageWindow != null) _messageWindow.Dispose();
            base.ExitThreadCore();
        }
    }

    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "QuickNote.SingleInstance", out createdNew);
            if (!createdNew)
            {
                // 已有实例在跑：请它弹出小条，然后自己退出。
                // 否则误双击 exe 会出现两个托盘图标和两个小条。
                var msg = NativeMethods.RegisterWindowMessage("QuickNote.Show");
                NativeMethods.PostMessage(new IntPtr(0xFFFF), msg, IntPtr.Zero, IntPtr.Zero);
                return;
            }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object s, ThreadExceptionEventArgs e)
            {
                QuickNoteApp.Log("界面线程异常：" + e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                QuickNoteApp.Log("未处理异常：" + e.ExceptionObject);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new QuickNoteApp());

            GC.KeepAlive(_mutex);
        }
    }
}
