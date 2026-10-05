using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    internal sealed class SettingsDialog : Form
    {
        private readonly AppConfig _config;
        private readonly RadioButton _doubleTap;
        private readonly RadioButton _combo;
        private readonly NumericUpDown _threshold;
        private readonly TextBox _fallback;
        private readonly TextBox _notesDir;
        private readonly ComboBox _theme;

        public SettingsDialog(AppConfig config)
        {
            _config = config;

            Text = "QuickNote 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 296);
            Font = new Font("Microsoft YaHei UI", 9f);

            _doubleTap = new RadioButton
            {
                Text = "双击左 Ctrl",
                Location = new Point(16, 16),
                Width = 200,
                Checked = config.HotkeyMode == AppConfig.ModeDoubleTap
            };
            _combo = new RadioButton
            {
                Text = "只用下面的组合键（不装全局键盘钩子）",
                Location = new Point(16, 42),
                Width = 320,
                Checked = config.HotkeyMode == AppConfig.ModeCombo
            };

            var thresholdLabel = new Label
            {
                Text = "双击判定窗口（毫秒）",
                Location = new Point(16, 78),
                Width = 170
            };
            _threshold = new NumericUpDown
            {
                Location = new Point(200, 76),
                Width = 80,
                Minimum = 50,
                Maximum = 2000,
                Increment = 10,
                Value = Math.Min(2000, Math.Max(50, config.DoubleTapMs))
            };

            var fallbackLabel = new Label
            {
                Text = "备用组合键",
                Location = new Point(16, 114),
                Width = 170
            };
            _fallback = new TextBox
            {
                Location = new Point(200, 111),
                Width = 168,
                ReadOnly = true,
                Text = config.FallbackHotkey,
                BackColor = SystemColors.Window
            };
            _fallback.KeyDown += OnFallbackKeyDown;

            var dirLabel = new Label { Text = "日记目录", Location = new Point(16, 150), Width = 170 };
            _notesDir = new TextBox
            {
                Location = new Point(200, 147),
                Width = 130,
                Text = config.NotesDir
            };
            var browse = new Button { Text = "…", Location = new Point(336, 145), Width = 32 };
            browse.Click += delegate
            {
                using (var dlg = new FolderBrowserDialog { SelectedPath = _notesDir.Text })
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK) _notesDir.Text = dlg.SelectedPath;
                }
            };

            var themeLabel = new Label { Text = "主题", Location = new Point(16, 186), Width = 80 };
            _theme = new ComboBox
            {
                Location = new Point(200, 183),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _theme.Items.AddRange(new object[] { "System", "Light", "Dark" });
            _theme.SelectedItem = config.Theme;
            if (_theme.SelectedIndex < 0) _theme.SelectedIndex = 0;

            var ok = new Button { Text = "确定", Location = new Point(240, 244), Width = 80, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", Location = new Point(328, 244), Width = 80, DialogResult = DialogResult.Cancel };

            ok.Click += delegate { Apply(); };
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[]
            {
                _doubleTap, _combo, thresholdLabel, _threshold,
                fallbackLabel, _fallback, dirLabel, _notesDir, browse,
                themeLabel, _theme, ok, cancel
            });
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
                    "QuickNote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var parts = new System.Collections.Generic.List<string>();
            if ((e.Modifiers & Keys.Control) != 0) parts.Add("Ctrl");
            if ((e.Modifiers & Keys.Alt) != 0) parts.Add("Alt");
            if ((e.Modifiers & Keys.Shift) != 0) parts.Add("Shift");
            if (parts.Count == 0)
            {
                MessageBox.Show(this, "至少要带一个修饰键（Ctrl / Alt / Shift / Win）。",
                    "QuickNote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            parts.Add(e.KeyCode.ToString());
            _fallback.Text = string.Join("+", parts.ToArray());
        }

        private void Apply()
        {
            _config.HotkeyMode = _doubleTap.Checked ? AppConfig.ModeDoubleTap : AppConfig.ModeCombo;
            _config.DoubleTapMs = (int)_threshold.Value;
            if (_fallback.Text.IndexOf('+') > 0) _config.FallbackHotkey = _fallback.Text;
            _config.NotesDir = _notesDir.Text.Trim();
            _config.Theme = (string)_theme.SelectedItem;
            _config.Save(AppConfig.DefaultConfigPath());
        }
    }
}
