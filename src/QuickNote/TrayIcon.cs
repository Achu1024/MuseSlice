using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ToolStripMenuItem _autoStartItem;

        public event Action ShowRequested;
        public event Action OpenTodayRequested;
        public event Action OpenNotesFolderRequested;
        public event Action SettingsRequested;
        public event Action AutoStartToggled;
        public event Action ExitRequested;

        public TrayIcon(bool autoStartChecked)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("记一笔", null, delegate { Raise(ShowRequested); });
            menu.Items.Add("打开今天的日记", null, delegate { Raise(OpenTodayRequested); });
            menu.Items.Add("打开日记目录", null, delegate { Raise(OpenNotesFolderRequested); });
            menu.Items.Add(new ToolStripSeparator());

            _autoStartItem = new ToolStripMenuItem("开机自启") { Checked = autoStartChecked };
            _autoStartItem.Click += delegate { Raise(AutoStartToggled); };
            menu.Items.Add(_autoStartItem);

            menu.Items.Add("设置…", null, delegate { Raise(SettingsRequested); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate { Raise(ExitRequested); });

            _icon = new NotifyIcon
            {
                Icon = MakeIcon(),
                Text = "QuickNote",
                Visible = true,
                ContextMenuStrip = menu
            };
            _icon.MouseClick += delegate (object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Raise(ShowRequested);
            };
        }

        public void SetAutoStartChecked(bool value) { _autoStartItem.Checked = value; }

        public void Balloon(string title, string text)
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = text;
            _icon.BalloonTipIcon = ToolTipIcon.Info;
            _icon.ShowBalloonTip(5000);
        }

        private static void Raise(Action a) { if (a != null) a(); }

        /// <summary>运行时画一个 16×16 的图标，避免为一张小图引入资源文件。</summary>
        private static Icon MakeIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (var pen = new Pen(Color.FromArgb(110, 168, 254), 1.6f))
                using (var brush = new SolidBrush(Color.FromArgb(110, 168, 254)))
                {
                    g.DrawRectangle(pen, 2.5f, 1.5f, 11f, 13f);
                    g.FillRectangle(brush, 4.5f, 4.5f, 7f, 1.4f);
                    g.FillRectangle(brush, 4.5f, 7.5f, 7f, 1.4f);
                    g.FillRectangle(brush, 4.5f, 10.5f, 4.5f, 1.4f);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
