using System;
using System.Drawing;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
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
                Text = Brand.Name,
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

        /// <summary>
        /// 托盘图标就是 MuseSlice 标志本身，走与界面同一套矢量绘制——不为一张小图
        /// 引入资源文件，也不必维护第二份图形。
        /// <para>
        /// 颜色从当前系统主题取：任务栏是深色时，标志主形的炭黑等于隐形，必须翻成浅色。
        /// 图标只在启动时画这一次，之后改主题不会重画——托盘图标只是个入口，
        /// 不值得为它挂一套重建逻辑。
        /// </para>
        /// </summary>
        private static Icon MakeIcon()
        {
            var palette = Theme.Current("System");
            var size = SystemInformation.SmallIconSize;   // 高 DPI 下比写死 16 更对

            using (var bmp = LogoMark.RenderBitmap(size.Width, size.Height,
                       palette.Brand, palette.BrandAccent))
            {
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
        }
    }
}
