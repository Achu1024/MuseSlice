using System;
using System.Drawing;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 设置。首次安装时会自动弹出来。
    /// <para>
    /// 版面只有两条左边缘：一列标签、一列控件。原先每个控件都是手写的绝对坐标，
    /// 结果标签有的宽 170 有的宽 80，控件起点却固定在 200——短的标签后面拖着一段
    /// 空档，长的又差点撞上控件。这里改成由一个行光标顺序排下来，位置再也对不齐不了。
    /// </para>
    /// </summary>
    internal sealed class SettingsDialog : Form
    {
        private const int PadX = 22;          // 左右内边距

        /// <summary>
        /// 标签列宽。要容得下最长的那条标签——「双击判定窗口」在 125% 下量出来 126px，
        /// 原先的 104 逻辑像素（含内缩 8）只给 120px，最后一个字会被切掉。
        /// </summary>
        private const int LabelColumn = 118;
        private const int RowGap = 10;
        private const int LogicalWidth = 452;

        private readonly AppConfig _config;
        private readonly Palette _p;
        private readonly float _scale;

        private readonly RadioButton _doubleTap;
        private readonly RadioButton _combo;
        private readonly NumericUpDown _threshold;
        private readonly TextBox _fallback;
        private readonly TextBox _notesDir;
        private readonly ComboBox _theme;

        /// <summary>行光标：下一个控件放在哪。顺序排下来，不用手写 y。</summary>
        private int _y;

        /// <summary>顶部品牌标志的位置，由 <see cref="BrandHeader"/> 算、由 OnPaint 画。</summary>
        private Rectangle _markRect;

        public SettingsDialog(AppConfig config, Palette palette)
        {
            _config = config;
            _p = palette;
            _scale = DetectScale();

            Text = Brand.Name + " 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = _p.Paper;
            Font = _p.Body;                   // 子控件都继承这个

            _doubleTap = Radio("双击左 Ctrl", config.HotkeyMode == AppConfig.ModeDoubleTap);
            _combo = Radio("只用组合键（不装全局键盘钩子）", config.HotkeyMode == AppConfig.ModeCombo);

            _threshold = new NumericUpDown
            {
                Minimum = 50,
                Maximum = 2000,
                Increment = 10,
                Value = Math.Min(2000, Math.Max(50, config.DoubleTapMs))
            };

            _fallback = new TextBox
            {
                ReadOnly = true,
                Text = config.FallbackHotkey,
                BackColor = _p.Paper,
                ForeColor = _p.Ink
            };
            _fallback.KeyDown += OnFallbackKeyDown;

            _notesDir = new TextBox { Text = config.NotesDir, BackColor = _p.Paper, ForeColor = _p.Ink };

            _theme = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            _theme.Items.AddRange(new object[] { "跟随系统", "浅色", "深色" });
            _theme.SelectedIndex = ThemeIndex(config.Theme);

            Build();
        }

        // ---- 版面 ----

        private int S(int logical) { return (int)Math.Round(logical * _scale); }

        /// <summary>
        /// 构造时就把真实 DPI 问出来。不靠 AutoScaleMode：对纯代码构造的窗体，
        /// 它拿到的 AutoScaleDimensions 恒等于当前值，缩放系数永远是 1，等于不缩。
        /// </summary>
        private float DetectScale()
        {
            try
            {
                using (var g = CreateGraphics()) return g.DpiX / 96f;
            }
            catch { return 1f; }
        }

        private void Build()
        {
            _y = S(18);

            BrandHeader();
            _y += S(4);
            Rule();

            Section("唤起方式");
            FullRow(_doubleTap);
            FullRow(_combo);
            Field("双击判定窗口", _threshold, S(64), "毫秒");
            Field("备用组合键", _fallback, ControlWidth() - S(60));
            Hint("按一下就录制。Ctrl+Space 留给输入法，不可占用。");

            _y += S(6);
            Rule();
            Section("日记与外观");
            FieldWithButton("日记目录", _notesDir, "…", BrowseForFolder);
            Field("主题", _theme, S(120));

            _y += S(6);
            Rule();
            Buttons();

            ClientSize = new Size(S(LogicalWidth), _y + S(18));
        }

        private int ControlLeft { get { return S(PadX + LabelColumn); } }
        private int ControlWidth() { return S(LogicalWidth - PadX) - ControlLeft; }

        /// <summary>
        /// 顶部的品牌行：标志 + 产品名。整页里唯一一处品牌，往下全是设置项。
        /// <para>
        /// 标志交给 <see cref="OnPaint"/> 画，文字仍是一个普通 Label、只是让开左边——
        /// 这样文字排版还是 WinForms 自己那套，不必手写居中。
        /// </para>
        /// </summary>
        private void BrandHeader()
        {
            var rowHeight = S(30);
            var markHeight = S(24);
            var markWidth = (int)Math.Round(markHeight * LogoMark.Aspect);

            _markRect = new Rectangle(S(PadX), _y + (rowHeight - markHeight) / 2,
                markWidth, markHeight);

            Controls.Add(NewLabel(Brand.Name, _p.Heading, _p.Ink,
                new Point(S(PadX) + markWidth + S(10), _y + (rowHeight - _p.Heading.Height) / 2), 0));

            _y += rowHeight;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_markRect.Width > 0)
                LogoMark.Draw(e.Graphics, _markRect, _p.Brand, _p.BrandAccent);
        }

        /// <summary>一条 1px 分隔线。和正文区一样，结构靠线，不靠空档。</summary>
        private void Rule()
        {
            Controls.Add(new Panel
            {
                Location = new Point(S(PadX), _y),
                Size = new Size(S(LogicalWidth - PadX * 2), 1),
                BackColor = _p.Hairline
            });
            _y += S(14);
        }

        private void Section(string text)
        {
            Controls.Add(NewLabel(text, _p.Chrome, _p.Muted, new Point(S(PadX), _y), 0));
            _y += _p.Chrome.Height + S(10);
        }

        /// <summary>
        /// 说明文字。比正文轻一档，不抢控件的注意力。
        /// <para>
        /// 宽度不够时让它换行占两行，**不截断**——说明被切掉一半比没有还糟，
        /// 而使用者看到的会是一句读不通的话。
        /// </para>
        /// </summary>
        private void Hint(string text)
        {
            var label = NewLabel(text, _p.Chrome, _p.Muted, new Point(ControlLeft, _y), ControlWidth());

            var lines = TextRenderer.MeasureText(text, _p.Chrome).Width > ControlWidth() ? 2 : 1;
            label.Height = _p.Chrome.Height * lines + S(4);

            Controls.Add(label);
            _y += label.Height + S(6);
        }

        /// <summary>占满一行、没有标签的控件（单选钮）。</summary>
        private void FullRow(Control c)
        {
            Themed(c);
            c.Location = new Point(S(PadX + 4), _y);
            c.Width = S(LogicalWidth - PadX * 2);
            Controls.Add(c);
            _y += c.Height + S(6);
        }

        /// <summary>
        /// 给控件套上当前配色。
        /// <para>
        /// **必须一个个显式设**：这块界面是纯代码构造的，WinForms 不会因为窗体
        /// 的 BackColor 换了就把子控件的底色和字色一起换掉——它们默认取系统色，
        /// 于是深色主题下就是「深底 + 一排浅色系统控件 + 黑字」，又刺眼又看不清。
        /// </para>
        /// <para>
        /// 单选钮和下拉框还要压成 Flat：圆点和下拉箭头是系统画的，用默认那套
        /// （3D/Standard）不吃我们给的 ForeColor，圆点在深底上会直接看不见。
        /// </para>
        /// </summary>
        private T Themed<T>(T control) where T : Control
        {
            control.BackColor = _p.Paper;
            control.ForeColor = _p.Ink;

            var button = control as ButtonBase;      // 按钮与单选钮的共同基类
            if (button != null) button.FlatStyle = FlatStyle.Flat;

            var combo = control as ComboBox;
            if (combo != null) combo.FlatStyle = FlatStyle.Flat;

            var flat = control as Button;
            if (flat != null) flat.FlatAppearance.BorderColor = _p.Hairline;

            return control;
        }

        /// <summary>一列标签 + 一列控件。两个左边缘就只有这两个。</summary>
        private void Field(string label, Control c, int width, string unit = null)
        {
            var at = _y;

            var lab = NewLabel(label, _p.Body, _p.Ink, new Point(S(PadX), at), S(LabelColumn - 8));
            lab.Height = c.Height;
            lab.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lab);

            Themed(c);
            c.Location = new Point(ControlLeft, at);
            c.Width = width;
            Controls.Add(c);

            if (unit != null)
            {
                var u = NewLabel(unit, _p.Chrome, _p.Muted, new Point(ControlLeft + width + S(8), at), 0);
                u.Height = c.Height;
                u.TextAlign = ContentAlignment.MiddleLeft;
                Controls.Add(u);
            }

            _y += c.Height + RowGap;
        }

        private void FieldWithButton(string label, TextBox box, string buttonText, Action onClick)
        {
            var at = _y;
            var buttonWidth = S(34);

            var lab = NewLabel(label, _p.Body, _p.Ink, new Point(S(PadX), at), S(LabelColumn - 8));
            lab.Height = box.Height;
            lab.TextAlign = ContentAlignment.MiddleLeft;
            Controls.Add(lab);

            Themed(box);
            box.Location = new Point(ControlLeft, at);
            box.Width = ControlWidth() - buttonWidth - S(6);
            Controls.Add(box);

            var button = Themed(new Button { Text = buttonText, Location = new Point(ControlLeft + box.Width + S(6), at - S(1)) });
            button.Size = new Size(buttonWidth, box.Height + S(2));
            button.Click += delegate { onClick(); };
            Controls.Add(button);

            _y += box.Height + RowGap;
        }

        private void Buttons()
        {
            var width = S(84);
            var height = S(30);
            var right = S(LogicalWidth - PadX);

            // 确定在右、取消在左。中文界面里主操作放右下角，与系统的对话框一致。
            var ok = Themed(new Button { Text = "确定", DialogResult = DialogResult.OK });
            var cancel = Themed(new Button { Text = "取消", DialogResult = DialogResult.Cancel });

            ok.Size = cancel.Size = new Size(width, height);
            cancel.Location = new Point(right - width - S(8) - width, _y);
            ok.Location = new Point(right - width, _y);

            ok.Click += delegate { Apply(); };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(cancel);
            Controls.Add(ok);

            _y += height;
        }

        private Label NewLabel(string text, Font font, Color color, Point at, int width)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                BackColor = _p.Paper,
                Location = at,
                Width = width > 0 ? width : 0,
                AutoSize = width <= 0
            };
        }

        private RadioButton Radio(string text, bool selected)
        {
            return Themed(new RadioButton { Text = text, Checked = selected, AutoSize = true });
        }

        private void BrowseForFolder()
        {
            using (var dlg = new FolderBrowserDialog { SelectedPath = _notesDir.Text })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) _notesDir.Text = dlg.SelectedPath;
            }
        }

        private static int ThemeIndex(string theme)
        {
            if (theme == "Light") return 1;
            if (theme == "Dark") return 2;
            return 0;
        }

        private static string ThemeName(int index)
        {
            if (index == 1) return "Light";
            if (index == 2) return "Dark";
            return "System";
        }

        /// <summary>
        /// 按下组合键即录制。必须避开 Ctrl+Space——那是中文输入法切换全半角的热键，
        /// 抢占会导致输入法行为错乱。
        /// <para>
        /// Ctrl+Shift 不需要单独拦：组合键必须带一个非修饰主键，而 Ctrl+Shift
        /// 单独按下根本不会产生 KeyDown 事件，构造不出来。
        /// </para>
        /// </summary>
        private void OnFallbackKeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.ShiftKey)
                return;   // 只按了修饰键，继续等主键

            if (e.KeyCode == Keys.Space)
            {
                MessageBox.Show(this, "Ctrl+Space 是中文输入法的切换热键，不能占用。",
                    Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            if ((e.Modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((e.Modifiers & Keys.Alt) != 0) parts.Add("Alt");
            if ((e.Modifiers & Keys.Shift) != 0) parts.Add("Shift");
            if (parts.Count == 0)
            {
                MessageBox.Show(this, "至少要带一个修饰键（Ctrl / Alt / Shift / Win）。",
                    Brand.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            parts.Add(e.KeyCode.ToString());
            _fallback.Text = string.Join("+", parts.ToArray());
        }

        private void Apply()
        {
            // 空目录会让 NoteStore 构造抛异常，而异常逃到全局处理器只写日志——
            // 使用者看到的是「点了确定，什么都没发生」。宁可不关对话框也要说明白。
            var problem = AppConfig.ValidateNotesDir(_notesDir.Text);
            if (problem != null)
            {
                MessageBox.Show(this, problem + "。", Brand.Name,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;   // 阻止关闭
                return;
            }

            _config.HotkeyMode = _doubleTap.Checked ? AppConfig.ModeDoubleTap : AppConfig.ModeCombo;
            _config.DoubleTapMs = (int)_threshold.Value;
            if (_fallback.Text.IndexOf('+') > 0) _config.FallbackHotkey = _fallback.Text;
            _config.NotesDir = _notesDir.Text.Trim();
            _config.Theme = ThemeName(_theme.SelectedIndex);
            _config.Save(AppConfig.DefaultConfigPath());
        }
    }
}
