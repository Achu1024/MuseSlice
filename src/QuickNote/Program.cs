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
        private NoteStore _store;      // 非 readonly：Task 13 改日记目录时要换掉

        /// <summary>与 _bar 共用同一套配色。绝不每次用都新建——Palette 里握着 Font，
        /// 反复新建会累积 GDI 句柄（那是有上限的资源，不是普通内存）。</summary>
        private Palette _palette;

        /// <summary>右下角的回执。第一次保存时才建，整个程序只有一个实例。</summary>
        private Toast _toast;
        private CaptureBar _bar;

        public QuickNoteApp()
        {
            // 「首次安装」的判据就是配置文件还不存在。AppConfig.Load 在文件缺失时
            // 只用默认值、**不写文件**，所以这个判据得在 Load 之前取。
            var firstRun = !File.Exists(AppConfig.DefaultConfigPath());
            _config = AppConfig.Load(AppConfig.DefaultConfigPath());
            _store = new NoteStore(_config.NotesDir);

            var showMessage = NativeMethods.RegisterWindowMessage("QuickNote.Show");
            _messageWindow = new MessageWindow(showMessage);
            _messageWindow.ShowRequested += ShowBar;   // 第二个实例被启动时由这里弹出小条

            _tray = new TrayIcon(_config.AutoStart);
            _tray.ShowRequested += ShowBar;
            _tray.OpenTodayRequested += OpenToday;
            _tray.OpenNotesFolderRequested += OpenNotesFolder;
            _tray.ExitRequested += ExitApp;
            _tray.SettingsRequested += OpenSettings;
            _tray.AutoStartToggled += ToggleAutoStart;

            _palette = Theme.Current(_config.Theme);
            _bar = new CaptureBar(_palette, _store);
            _bar.Saved += OnSaved;

            _hotkeys = new HotkeyManager(_config);
            _hotkeys.Degraded += delegate (string reason)
            {
                _tray.Balloon("QuickNote 热键降级", reason);
            };
            _hotkeys.Start(ShowBar, _messageWindow.Handle, showMessage);

            // 自启：注册表里存的是绝对路径，程序挪过位置就静默失效，每次启动自查一遍
            AutoStart.EnsurePathCurrent();
            if (_config.AutoStart) AutoStart.Enable(); else AutoStart.Disable();

            // spec 第 11 节：笔记目录不存在时启动就建好。否则托盘里的
            // 「打开今天的日记」会因目录缺失抛异常，而使用者看不到任何反馈。
            try { Directory.CreateDirectory(_config.NotesDir); }
            catch (Exception ex) { Log("创建笔记目录失败：" + ex.Message); }

            if (firstRun) PromptFirstRun();
        }

        /// <summary>
        /// 首次安装：把设置页推给使用者。默认值能用，但日记目录这种东西不该让人
        /// 自己去猜、也不该等到发现「日记不知道存哪去了」才回来找。
        /// <para>
        /// 延后一拍再弹，不在构造函数里直接 ShowDialog：那时消息循环还没跑起来，
        /// 托盘图标也还没出现，弹一个模态框会让人不知道它是谁弹的。
        /// </para>
        /// </summary>
        private void PromptFirstRun()
        {
            var once = new System.Windows.Forms.Timer { Interval = 400 };
            once.Tick += delegate
            {
                once.Stop();
                once.Dispose();

                // 先教怎么把它叫出来——这是唯一一次「教怎么用」的机会。
                // 看完再去确认设置：设置页讲的是配置，而使用者此刻还不知道
                // 这个东西平时在哪、怎么喊出来。
                using (var card = new WelcomeCard(_palette)) card.ShowDialog();
                OpenSettings();
            };
            once.Start();
        }

        /// <summary>保存成功的回执：一声「咔嚓」+ 右下角闪一下「已记录」。</summary>
        private void OnSaved()
        {
            SaveChime.Play();

            if (_toast == null) _toast = new Toast(_palette);
            _toast.Pop("已记录", _palette);
        }

        private void ShowBar()
        {
            if (_bar == null) return;
            _bar.OpenBar();
        }

        private void OpenToday()
        {
            try
            {
                Directory.CreateDirectory(_config.NotesDir);
                var path = Path.Combine(_config.NotesDir, DateFormat.FileName(DateTime.Now) + ".md");
                if (!File.Exists(path)) File.WriteAllText(path, "", new System.Text.UTF8Encoding(false));
                Open(path);
            }
            catch (Exception ex)
            {
                Log("打开今天的日记失败：" + ex);
                _tray.Balloon("QuickNote", "打不开今天的日记：" + ex.Message);
            }
        }

        private void OpenNotesFolder()
        {
            try
            {
                Directory.CreateDirectory(_config.NotesDir);
                Open(_config.NotesDir);
            }
            catch (Exception ex)
            {
                Log("打开日记目录失败：" + ex);
                _tray.Balloon("QuickNote", "打不开日记目录：" + ex.Message);
            }
        }

        private static void Open(string path)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { Log("打开失败 " + path + "：" + ex.Message); }
        }

        private void OpenSettings()
        {
            using (var dlg = new SettingsDialog(_config, _palette))
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
            }

            try
            {
                _store = new NoteStore(_config.NotesDir);
                _bar.SetStore(_store);
                _hotkeys.ApplyConfigChange();
                _palette = Theme.Current(_config.Theme);
                _bar.ApplyTheme(_palette);
                if (_toast != null) _toast.ApplyTheme(_palette);
                if (_config.AutoStart) AutoStart.Enable(); else AutoStart.Disable();
                _tray.SetAutoStartChecked(_config.AutoStart);
                _tray.Balloon("QuickNote", "设置已保存");
            }
            catch (Exception ex)
            {
                // 托盘点击处理器里逃出去的异常只会被全局处理器写进日志，
                // 使用者什么都看不到。这里必须自己说出来。
                Log("应用设置失败：" + ex);
                _tray.Balloon("QuickNote 设置未能生效", ex.Message);
            }
        }

        private void ToggleAutoStart()
        {
            _config.AutoStart = !_config.AutoStart;
            if (_config.AutoStart) AutoStart.Enable(); else AutoStart.Disable();
            _config.Save(AppConfig.DefaultConfigPath());
            _tray.SetAutoStartChecked(_config.AutoStart);
        }

        private void ExitApp()
        {
            // 直接退出会把使用者写了三行还没保存的东西丢掉，先问一句。
            var draft = _bar == null ? "" : _bar.Draft;
            if (draft.Trim().Length > 0)
            {
                var answer = MessageBox.Show(
                    "还有没保存的内容：\n\n" + draft + "\n\n要退出吗？（退出后这些内容会丢失）",
                    "QuickNote", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }
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
