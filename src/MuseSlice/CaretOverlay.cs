using System;
using System.Drawing;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 自绘光标。RichEdit 自带的是 1px 硬闪的黑条；这里把它 HideCaret 掉，
    /// 自己画一根 2px 的短竖线，颜色用 ink，1 秒一循环地柔和明暗（定案 §5）。
    /// <para>
    /// 做成独立子控件，而不是往 RichEdit 的 DC 上画：后者每次 RichEdit 重绘
    /// （打字、滚动、改选）都会被抹掉，只能靠高频重画去补，看起来就是闪。
    /// 子控件是独立的 HWND，RichEdit 永远碰不到它。
    /// </para>
    /// <para>
    /// 明暗用改背景色实现，不用透明度：小条背景是纯色，线性插出来的中间色
    /// 与真正的 alpha 混合结果完全一致，而子控件就不必去碰分层窗口。
    /// </para>
    /// <para>
    /// 计时器只在输入框拿到焦点时跑。小条收起、或焦点跑到另一个框上时它停掉——
    /// 待机时进程里没有任何计时器。
    /// </para>
    /// <para>
    /// 填写页的正文框和搜索页的查询框**共用这一个类**。原先查询框走的是系统光标，
    /// 它和自绘这根既不同高也不同位——同一个界面里两根竖线长得不一样，切换时看得出来。
    /// 查询框接上来之后，两边的度量都由 <see cref="CaretBox"/> 算，位置自然一致。
    /// </para>
    /// </summary>
    internal sealed class CaretOverlay : Control
    {
        private const int StepMs = 40;
        private const int LogicalWidth = 2;

        private readonly Timer _timer;
        private TextBoxBase _box;
        private Control _host;
        private float _scale = 1f;
        private Color _paper = Color.White;
        private Color _ink = Color.Black;
        private int _elapsed;
        private int _diagCounter;

        public CaretOverlay()
        {
            SetStyle(ControlStyles.Selectable, false);   // 绝不抢焦点
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = false;
            Visible = false;

            _timer = new Timer { Interval = StepMs };
            _timer.Tick += delegate
            {
                _elapsed += StepMs;
                Follow();
            };
        }

        public void Attach(TextBoxBase box, Control host, float scale)
        {
            _box = box;
            _host = host;
            _scale = scale;
            Follow();
        }

        public void SetColors(Palette p)
        {
            _paper = p.Paper;
            _ink = p.Ink;
            Follow();
        }

        /// <summary>把光标贴到插入点。输入框没有焦点、或小条已收起时就藏起来并停表。</summary>
        public void Follow()
        {
            // 自绘光标已禁用，使用系统光标
            if (_box == null || _host == null || !_box.IsHandleCreated || !_box.Visible ||
                !_host.Visible || !_box.Focused)
            {
                Stop();
                return;
            }

            // 输入法组合期间整个让开。组合串与候选框是 IME 在画，而且 IME 要靠插入符
            // 定位它们——我们在这里反复 HideCaret 会把 IME 的锚点抹掉。组合结束后
            // 那次上屏会再触发一次文本变更，Follow 自然恢复。
            // 只有正文框跟输入法打交道；查询框是单行文本框，没有这一层。
            var input = _box as BarTextBox;
            if (input != null && input.IsComposing)
            {
                Stop();
                return;
            }

            // 系统光标必须反复藏。RichEdit 在按键、滚动、改选之后会自己把它显示回来，
            // 藏一次是不够的——每个节拍补一次，代价是一次 user32 调用。
            // 临时注释：测试时使用系统光标
            // NativeMethods.HideCaret(_box.Handle);

            var caret = _box.SelectionStart;
            var isRichTextBox = _box is RichTextBox;
            Point at;

            // 尝试多种方法获取光标位置，优先使用最可靠的方式
            if (isRichTextBox)
            {
                // 方法1: 对于 RichEdit，先尝试 EM_POSFROMCHAR 消息
                var result = NativeMethods.SendMessage(_box.Handle, NativeMethods.EM_POSFROMCHAR,
                    new IntPtr(caret), IntPtr.Zero);
                var x = unchecked((short)(result.ToInt32() & 0xFFFF));
                var y = unchecked((short)((result.ToInt32() >> 16) & 0xFFFF));

                // 如果返回值有效（不是 -1），使用它
                if (x >= 0 && y >= 0)
                {
                    at = new Point(x, y);
                }
                else
                {
                    // 后备：使用 GetPositionFromCharIndex
                    at = _box.GetPositionFromCharIndex(caret);
                }
            }
            else
            {
                // 对于普通 TextBox，先尝试 GetCaretPos（获取系统光标的实际位置）
                NativeMethods.POINT caretPos;
                if (NativeMethods.GetCaretPos(out caretPos) && (caretPos.x > 0 || caretPos.y > 0 || caret == 0))
                {
                    // GetCaretPos 返回的是相对于控件客户区的坐标，直接使用
                    at = new Point(caretPos.x, caretPos.y);
                }
                else
                {
                    // 后备：使用 GetPositionFromCharIndex
                    at = _box.GetPositionFromCharIndex(caret);
                }
            }

            // 转换坐标：从控件内坐标 -> 屏幕坐标 -> host 内坐标
            var screen = _box.PointToScreen(at);
            var local = _host.PointToClient(screen);

            int top, height;
            Measure(_box.Font, out top, out height);

            // DIAG: 每 20 次记录一次，避免刷屏
            if (_diagCounter++ % 20 == 0)
            {
                var boxType = isRichTextBox ? "RichTextBox" : "TextBox";
                MuseSliceApp.Log($"DIAG 光标定位 [{boxType}]: caret={caret}/{_box.TextLength}, " +
                    $"at=({at.X},{at.Y}), screen=({screen.X},{screen.Y}), local=({local.X},{local.Y}), " +
                    $"final=({local.X},{local.Y + top}), top={top}, height={height}");
            }

            Location = new Point(local.X, local.Y + top);
            Width = Math.Max(1, (int)Math.Round(LogicalWidth * _scale));
            Height = height;

            if (!Visible)
            {
                Visible = true;
                _elapsed = 0;   // 光标刚跳过去时正好落在全暗那一瞬，会显得它没出现
            }
            Apply();
            if (!_timer.Enabled) _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();

            // 无条件设 false，**不要**写成 if (Visible) Visible = false。
            // Visible 的取值返回的是「有效可见性」：父控件不可见时它恒为 false，
            // 于是那句赋值会被跳过，而控件内部的可见标志留成 true。之后父控件一旦
            // 重新可见（查询行就是这么用的），这根光标就自己冒出来，停在原地不动——
            // 同时 Follow 不再运行、HideCaret 也没人调，系统那根原生光标跟着露出来，
            // 屏幕上就是两根光标。
            Visible = false;
        }

        private void Apply()
        {
            BackColor = Blend(_paper, _ink, CaretPulse.LevelAt(_elapsed));
        }

        /// <summary>
        /// 从字体度量里取出「行距」与「字身」，交给 <see cref="CaretBox.Range"/> 算上下范围。
        /// <para>
        /// <c>Font.Height</c> 是行距，不是字身：CJK 字体（Noto Sans SC）的行距约 1.45em，
        /// 而字身只有 1.16em 上下。直接拿它当光标高度，那根线会比文字高出一大截、顶到
        /// 上面的分隔线。字身由 <c>FontFamily</c> 的升部 + 降部按行距等比换算而来。
        /// </para>
        /// </summary>
        private static void Measure(Font font, out int top, out int height)
        {
            if (font == null)
            {
                CaretBox.Range(0, 0, 0, out top, out height);
                return;
            }

            var family = font.FontFamily;
            var style = font.Style;
            var lineSpacing = font.Height;
            var designLineSpacing = family.GetLineSpacing(style);

            if (designLineSpacing <= 0)
            {
                CaretBox.Range(lineSpacing, 0, 0, out top, out height);
                return;
            }

            var ascent = (int)Math.Round(
                (double)lineSpacing * family.GetCellAscent(style) / designLineSpacing);
            var descent = (int)Math.Round(
                (double)lineSpacing * family.GetCellDescent(style) / designLineSpacing);

            CaretBox.Range(lineSpacing, ascent, descent, out top, out height);
        }

        private static Color Blend(Color paper, Color ink, double level)
        {
            return Color.FromArgb(
                (int)Math.Round(paper.R + (ink.R - paper.R) * level),
                (int)Math.Round(paper.G + (ink.G - paper.G) * level),
                (int)Math.Round(paper.B + (ink.B - paper.B) * level));
        }
    }
}
