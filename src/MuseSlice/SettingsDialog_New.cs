using System;
using System.Drawing;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 设置对话框 - 优化版：更好的字体、间距和 Notion 风格。
    /// <para>
    /// 改进：
    /// - 更合理的字体层次（标题/正文/说明）
    /// - 更宽松的间距，视觉呼吸感更好
    /// - 细线边框，轻量简洁
    /// - 新增保存回执方式选项（效果音/通知）
    /// </para>
    /// </summary>
    internal sealed class SettingsDialogNew : Form
    {
        private const int LogicalWidth = 540;
        private const int PadX = 32;
        private const int LabelColumn = 120;

        private readonly AppConfig _config;
        private readonly Palette _p;
        private readonly float _scale;
        private readonly Font _sectionFont;
        private readonly Font _labelFont;
        private readonly Font _hintFont;

        private readonly RadioButton _doubleTap;
        private readonly RadioButton _combo;
        private readonly NumericUpDown _threshold;
        private readonly TextBox _fallback;
        private readonly TextBox _notesDir;
        private readonly ComboBox _theme;
        private readonly RadioButton _feedbackSound;
        private readonly RadioButton _feedbackNotification;

        private Rectangle _markRect;
        private int _y;

        internal SettingsDialogNew(AppConfig config, Palette palette)
        {
            _config = config;
            _p = palette;
            _scale = DetectScale();

            // 字体层次优化
            _sectionFont = new Font(_p.Body.FontFamily, 9f, FontStyle.Bold);     // 分组标题
            _labelFont = new Font(_p.Body.FontFamily, 9.5f, FontStyle.Regular); // 字段标签
            _hintFont = new Font(_p.Body.FontFamily, 8.5f, FontStyle.Regular);  // 说明文字

            FormBorderStyle = FormBorderStyle.FixedDialog;
            Text = Brand.Name + " 设置";
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = _p.Paper;
            Font = _p.Body;

            // 唤起方式
            _doubleTap = new RadioButton
            {
                Text = "双击左 Ctrl",
                Checked = config.HotkeyMode == AppConfig.ModeDoubleTap,
                AutoSize = true
            };
            _combo = new RadioButton
            {
                Text = "只用组合键（不装全局键盘钩子）",
                Checked = config.HotkeyMode == AppConfig.ModeCombo,
                AutoSize = true
            };
            _threshold = new NumericUpDown
            {
                Minimum = 50,
                Maximum = 2000,
                Increment = 10,
                Value = config.DoubleTapMs
            };
            _fallback = new TextBox { Text = config.FallbackHotkey };
            _fallback.KeyDown += OnFallbackKeyDown;

            // 日记与外观
            _notesDir = new TextBox { Text = config.NotesDir };
            _theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _theme.Items.AddRange(new object[] { "跟随系统", "浅色", "深色" });
            _theme.SelectedIndex = ThemeIndex(config.Theme);

            // 保存回执方式
            _feedbackSound = new RadioButton
            {
                Text = "播放效果音",
                Checked = config.SaveFeedback == AppConfig.FeedbackSound,
                AutoSize = true
            };
            _feedbackNotification = new RadioButton
            {
                Text = "显示通知",
                Checked = config.SaveFeedback == AppConfig.FeedbackNotification,
                AutoSize = true
            };

            Build();
        }

        private int S(int logical) => (int)Math.Round(logical * _scale);

        private float DetectScale()
        {
            try
            {
                using (var g = CreateGraphics())
                    return g.DpiX / 96f;
            }
            catch { return 1f; }
        }

        private void Build()
        {
            _y = S(20);

            BrandHeader();
            _y += S(6);
            Rule();

            // 唤起方式（使用 Panel 分组）
            Section("唤起方式");
            var hotkeyPanel = new Panel
            {
                Location = new Point(S(PadX), _y),
                Size = new Size(S(LogicalWidth - PadX * 2), S(60)),
                BackColor = _p.Paper
            };
            _doubleTap.Location = new Point(6, 0);
            _combo.Location = new Point(6, S(26));
            hotkeyPanel.Controls.Add(_doubleTap);
            hotkeyPanel.Controls.Add(_combo);
            Controls.Add(hotkeyPanel);
            _y += hotkeyPanel.Height + S(8);

            Field("双击判定窗口", _threshold, S(80), "毫秒");
            Field("备用组合键", _fallback, ControlWidth() - S(70));
            Hint("按一下就录制。Ctrl+Space 留给输入法，不可占用。");

            _y += S(10);
            Rule();

            // 日记与外观
            Section("日记与外观");
            FieldWithButton("日记目录", _notesDir, "…", BrowseForFolder);
            Field("主题", _theme, S(140));

            _y += S(10);
            Rule();

            // 保存回执（使用单独的 Panel 分组）
            Section("保存回执");
            var feedbackPanel = new Panel
            {
                Location = new Point(S(PadX), _y),
                Size = new Size(S(LogicalWidth - PadX * 2), S(52)),
                BackColor = _p.Paper
            };
            _feedbackSound.Location = new Point(6, 0);
            _feedbackNotification.Location = new Point(6, S(26));
            feedbackPanel.Controls.Add(_feedbackSound);
            feedbackPanel.Controls.Add(_feedbackNotification);
            Controls.Add(feedbackPanel);
            _y += feedbackPanel.Height + S(8);

            Hint("选择记录保存后的提示方式：效果音或系统通知。");

            _y += S(10);
            Rule();
            Buttons();

            ClientSize = new Size(S(LogicalWidth), _y + S(20));
        }

        private int ControlLeft => S(PadX + LabelColumn);
        private int ControlWidth() => S(LogicalWidth - PadX) - ControlLeft;

        private void BrandHeader()
        {
            var rowHeight = S(32);
            var markHeight = S(24);
            var markWidth = (int)Math.Round(markHeight * LogoMark.Aspect);

            _markRect = new Rectangle(S(PadX), _y + (rowHeight - markHeight) / 2, markWidth, markHeight);

            var titleLabel = NewLabel(Brand.Name, new Font(_p.Body.FontFamily, 11f, FontStyle.Bold),
                _p.Ink, new Point(S(PadX) + markWidth + S(12), _y + (rowHeight - S(20)) / 2), 0);
            Controls.Add(titleLabel);

            _y += rowHeight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // 细边框
            using (var pen = new Pen(_p.Hairline, 1))
            {
                g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            if (_markRect.Width > 0)
                LogoMark.Draw(g, _markRect, _p.Brand, _p.BrandAccent);
        }

        private void Rule()
        {
            Controls.Add(new Panel
            {
                Location = new Point(S(PadX), _y),
                Size = new Size(S(LogicalWidth - PadX * 2), 1),
                BackColor = _p.Hairline
            });
            _y += S(16);
        }

        private void Section(string text)
        {
            var label = NewLabel(text, _sectionFont, _p.Muted, new Point(S(PadX), _y), 0);
            Controls.Add(label);
            _y += label.Height + S(12);
        }

        private void Hint(string text)
        {
            var label = NewLabel(text, _hintFont, _p.Muted, new Point(ControlLeft, _y), ControlWidth());
            var lines = TextRenderer.MeasureText(text, _hintFont).Width > ControlWidth() ? 2 : 1;
            label.Height = _hintFont.Height * lines + S(4);
            Controls.Add(label);
            _y += label.Height + S(8);
        }

        private void FullRow(Control c)
        {
            Themed(c);
            c.Location = new Point(S(PadX + 6), _y);
            c.Width = S(LogicalWidth - PadX * 2);
            Controls.Add(c);
            _y += c.Height + S(8);
        }

        private T Themed<T>(T control) where T : Control
        {
            control.BackColor = _p.Paper;
            control.ForeColor = _p.Ink;

            if (control is ButtonBase button)
                button.FlatStyle = FlatStyle.Flat;

            if (control is ComboBox combo)
                combo.FlatStyle = FlatStyle.Flat;

            if (control is Button flat)
                flat.FlatAppearance.BorderColor = _p.Hairline;

            return control;
        }

        private void Field(string label, Control c, int width, string unit = null)
        {
            var at = _y;

            var lab = NewLabel(label, _labelFont, _p.Ink, new Point(S(PadX), at), S(LabelColumn - 8));
            lab.Height = c.Height;
            lab.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lab);

            Themed(c);
            c.Location = new Point(ControlLeft, at);
            c.Width = width;
            Controls.Add(c);

            if (unit != null)
            {
                var unitLabel = NewLabel(unit, _hintFont, _p.Muted,
                    new Point(ControlLeft + width + S(8), at + (c.Height - _hintFont.Height) / 2), 0);
                Controls.Add(unitLabel);
            }

            _y += c.Height + S(10);
        }

        private void FieldWithButton(string label, Control c, string btnText, EventHandler btnClick)
        {
            var at = _y;

            var lab = NewLabel(label, _labelFont, _p.Ink, new Point(S(PadX), at), S(LabelColumn - 8));
            lab.Height = c.Height;
            lab.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lab);

            var btnWidth = S(40);
            Themed(c);
            c.Location = new Point(ControlLeft, at);
            c.Width = ControlWidth() - btnWidth - S(6);
            Controls.Add(c);

            var btn = new Button { Text = btnText, Size = new Size(btnWidth, c.Height) };
            Themed(btn);
            btn.Location = new Point(ControlLeft + c.Width + S(6), at);
            btn.Click += btnClick;
            Controls.Add(btn);

            _y += c.Height + S(10);
        }

        private void Buttons()
        {
            var btnWidth = S(90);
            var btnHeight = S(32);
            var gap = S(10);

            var cancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(btnWidth, btnHeight),
                Location = new Point(S(LogicalWidth) - S(PadX) - btnWidth * 2 - gap, _y)
            };
            Themed(cancel);
            Controls.Add(cancel);
            CancelButton = cancel;

            var ok = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Size = new Size(btnWidth, btnHeight),
                Location = new Point(S(LogicalWidth) - S(PadX) - btnWidth, _y),
                BackColor = _p.BrandAccent,
                ForeColor = Color.White
            };
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.Click += OnOk;
            Controls.Add(ok);
            AcceptButton = ok;

            _y += btnHeight;
        }

        private Label NewLabel(string text, Font font, Color color, Point location, int width)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                Location = location,
                Width = width,
                AutoSize = width == 0
            };
        }

        private int ThemeIndex(string theme)
        {
            if (theme == "Light") return 1;
            if (theme == "Dark") return 2;
            return 0;
        }

        private string ThemeName(int index)
        {
            if (index == 1) return "Light";
            if (index == 2) return "Dark";
            return "System";
        }

        private void OnFallbackKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            var parts = new System.Collections.Generic.List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Alt) parts.Add("Alt");
            if (e.Shift) parts.Add("Shift");
            if (e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.Menu && e.KeyCode != Keys.ShiftKey)
                parts.Add(e.KeyCode.ToString());

            var combo = string.Join("+", parts);
            _fallback.Text = combo;

            // 实时检测快捷键是否被占用
            if (parts.Count >= 2) // 至少需要修饰键+主键
            {
                CheckHotkeyAvailability(combo);
            }
        }

        private void CheckHotkeyAvailability(string combo)
        {
            // 临时注册测试快捷键是否可用
            using (var tester = new FallbackHotkey(() => { }))
            {
                string error;
                if (!tester.TryRegister(combo, out error))
                {
                    _fallback.BackColor = Color.FromArgb(255, 240, 240); // 淡红色背景提示冲突
                    MessageBox.Show(this, error + "，请选择其他组合键。", Brand.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    _fallback.BackColor = _p.Paper; // 恢复正常背景
                }
            }
        }

        private void BrowseForFolder(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog { SelectedPath = _notesDir.Text })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _notesDir.Text = dlg.SelectedPath;
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            var problem = AppConfig.ValidateNotesDir(_notesDir.Text);
            if (problem != null)
            {
                MessageBox.Show(this, problem + "。", Brand.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            _config.HotkeyMode = _doubleTap.Checked ? AppConfig.ModeDoubleTap : AppConfig.ModeCombo;
            _config.DoubleTapMs = (int)_threshold.Value;
            if (_fallback.Text.IndexOf('+') > 0) _config.FallbackHotkey = _fallback.Text;
            _config.NotesDir = _notesDir.Text.Trim();
            _config.Theme = ThemeName(_theme.SelectedIndex);
            _config.SaveFeedback = _feedbackSound.Checked ? AppConfig.FeedbackSound : AppConfig.FeedbackNotification;
            _config.Save(AppConfig.DefaultConfigPath());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sectionFont?.Dispose();
                _labelFont?.Dispose();
                _hintFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
