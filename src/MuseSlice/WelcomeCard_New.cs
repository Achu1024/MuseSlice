using System;
using System.Drawing;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 首次运行欢迎卡片：Notion 风格，简洁轻量。
    /// <para>
    /// 新设计采用居中对齐的卡片布局：
    /// - Logo 居中展示（120x120）
    /// - 应用标题 + 副标题
    /// - 快捷键提示："按 Ctrl ×2 即可唤起"（Ctrl 带下划线强调）
    /// - Tab 键提示："唤起后按 Tab 即可查看记录"
    /// - 知道了按钮（靛蓝色）
    /// </para>
    /// <para>
    /// 细线边框（1px），小圆角（3px），品牌色点缀，整体轻量简洁。
    /// </para>
    /// </summary>
    internal sealed class WelcomeCardNew : Form
    {
        private const int LogicalWidth = 480;

        private readonly Palette _p;
        private readonly Font _titleFont;
        private readonly Font _subtitleFont;
        private readonly Font _hintFont;
        private readonly Font _keyFont;
        private readonly Font _tabKeyFont;
        private readonly Button _ok;

        private Rectangle _logoRect;
        private Rectangle _titleRect;
        private Rectangle _subtitleRect;
        private Rectangle _keyHintRect;
        private Rectangle _tabHintRect;

        internal WelcomeCardNew(Palette palette)
        {
            _p = palette;

            // 字体
            _titleFont = new Font(_p.Body.FontFamily, 15f, FontStyle.Bold);
            _subtitleFont = new Font(_p.Body.FontFamily, 9f, FontStyle.Regular);
            _hintFont = new Font(_p.Body.FontFamily, 10f, FontStyle.Regular);
            _keyFont = new Font("Consolas", 13f, FontStyle.Bold);
            _tabKeyFont = new Font("Consolas", 8.5f, FontStyle.Regular);

            FormBorderStyle = FormBorderStyle.None;
            BackColor = _p.Paper;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = false;

            var scale = DeviceDpi / 96f;
            int S(int logical) => (int)Math.Round(logical * scale);

            var pad = S(60);
            var innerWidth = S(LogicalWidth) - pad * 2;
            var y = pad;

            // Logo 区域 (120x120 逻辑像素)
            var logoSize = S(120);
            _logoRect = new Rectangle((S(LogicalWidth) - logoSize) / 2, y, logoSize, logoSize);
            y += logoSize + S(32);

            // 应用标题 "MuseSlice"
            using (var g = CreateGraphics())
            {
                var titleSize = TextRenderer.MeasureText(g, Brand.Name, _titleFont);
                _titleRect = new Rectangle((S(LogicalWidth) - titleSize.Width) / 2, y,
                    titleSize.Width, titleSize.Height);
            }
            y += _titleRect.Height + S(12);

            // 副标题
            var subtitle = "快速记录灵感的便签工具";
            using (var g = CreateGraphics())
            {
                var subtitleSize = TextRenderer.MeasureText(g, subtitle, _subtitleFont);
                _subtitleRect = new Rectangle((S(LogicalWidth) - subtitleSize.Width) / 2, y,
                    subtitleSize.Width, subtitleSize.Height);
            }
            y += _subtitleRect.Height + S(36);

            // 快捷键提示区域 "按 Ctrl ×2 即可唤起"
            _keyHintRect = new Rectangle(pad, y, innerWidth, S(30));
            y += S(30) + S(20);

            // Tab 键提示 "唤起后按 Tab 即可查看记录"
            _tabHintRect = new Rectangle(pad, y, innerWidth, S(24));
            y += S(24) + S(32);

            // 知道了按钮（靛蓝色）
            _ok = new Button
            {
                Text = "知道了",
                Font = _hintFont,
                Size = new Size(S(120), S(36)),
                Location = new Point((S(LogicalWidth) - S(120)) / 2, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = _p.BrandAccent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _ok.FlatAppearance.BorderSize = 0;
            _ok.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(_ok);
            AcceptButton = _ok;
            y += _ok.Height + pad;

            ClientSize = new Size(S(LogicalWidth), y);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                // Win11 圆角
                var pref = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref pref, sizeof(int));
            }
            catch { /* 非 Win11 忽略 */ }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 背景
            g.Clear(_p.Paper);

            // 细边框（1px）
            using (var pen = new Pen(_p.Hairline, 1))
            {
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            // 绘制 Logo
            LogoMark.Draw(g, _logoRect, _p.Brand, _p.BrandAccent);

            // 应用标题
            TextRenderer.DrawText(g, Brand.Name, _titleFont, _titleRect, _p.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 副标题
            TextRenderer.DrawText(g, "快速记录灵感的便签工具", _subtitleFont, _subtitleRect,
                _p.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 快捷键提示 "按 Ctrl ×2 即可唤起"
            DrawKeyHint(g, _keyHintRect);

            // Tab 键提示 "唤起后按 Tab 即可查看记录"
            DrawTabHint(g, _tabHintRect);
        }

        private void DrawKeyHint(Graphics g, Rectangle rect)
        {
            // "按 Ctrl ×2 即可唤起" 居中显示
            var text1 = "按";
            var ctrlText = "Ctrl";
            var text2 = "×2";
            var text3 = "即可唤起";

            var size1 = TextRenderer.MeasureText(g, text1, _hintFont);
            var sizeCtrl = TextRenderer.MeasureText(g, ctrlText, _keyFont);
            var size2 = TextRenderer.MeasureText(g, text2, _hintFont);
            var size3 = TextRenderer.MeasureText(g, text3, _hintFont);

            var totalWidth = size1.Width + sizeCtrl.Width + size2.Width + size3.Width + 20; // 间距
            var x = rect.X + (rect.Width - totalWidth) / 2;
            var y = rect.Y + (rect.Height - size1.Height) / 2;

            // "按"
            TextRenderer.DrawText(g, text1, _hintFont, new Point(x, y), _p.InkSoft);
            x += size1.Width + 6;

            // "Ctrl" 带下划线
            var ctrlY = y + 2;
            TextRenderer.DrawText(g, ctrlText, _keyFont, new Point(x, ctrlY), _p.Ink);
            using (var pen = new Pen(_p.BrandAccent, 2))
            {
                g.DrawLine(pen, x, ctrlY + sizeCtrl.Height + 2, x + sizeCtrl.Width, ctrlY + sizeCtrl.Height + 2);
            }
            x += sizeCtrl.Width + 6;

            // "×2"
            TextRenderer.DrawText(g, text2, _hintFont, new Point(x, y), _p.Muted);
            x += size2.Width + 6;

            // "即可唤起"
            TextRenderer.DrawText(g, text3, _hintFont, new Point(x, y), _p.InkSoft);
        }

        private void DrawTabHint(Graphics g, Rectangle rect)
        {
            // "唤起后按 Tab 即可查看记录" 居中显示
            var text1 = "唤起后按";
            var tabText = "Tab";
            var text2 = "即可查看记录";

            var size1 = TextRenderer.MeasureText(g, text1, _subtitleFont);
            var sizeTab = TextRenderer.MeasureText(g, tabText, _tabKeyFont);
            var size2 = TextRenderer.MeasureText(g, text2, _subtitleFont);

            var totalWidth = size1.Width + sizeTab.Width + size2.Width + 18; // Tab 左右各留一点空间
            var x = rect.X + (rect.Width - totalWidth) / 2;
            var y = rect.Y + (rect.Height - size1.Height) / 2;

            // "唤起后按"
            TextRenderer.DrawText(g, text1, _subtitleFont, new Point(x, y), _p.Muted);
            x += size1.Width + 4;

            // "Tab" 键（带背景和圆角）
            var tabPadding = 4;
            var tabRect = new Rectangle(x - tabPadding, y - 2, sizeTab.Width + tabPadding * 2, sizeTab.Height + 4);
            using (var brush = new SolidBrush(Color.FromArgb(240, 240, 240)))
            {
                g.FillRectangle(brush, tabRect);
            }
            TextRenderer.DrawText(g, tabText, _tabKeyFont,
                new Point(x, y), _p.BrandAccent);
            x += sizeTab.Width + tabPadding * 2 + 4;

            // "即可查看记录"
            TextRenderer.DrawText(g, text2, _subtitleFont, new Point(x, y), _p.Muted);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!IsDisposed) Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont?.Dispose();
                _subtitleFont?.Dispose();
                _hintFont?.Dispose();
                _keyFont?.Dispose();
                _tabKeyFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
