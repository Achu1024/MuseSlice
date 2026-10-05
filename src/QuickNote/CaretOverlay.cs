using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
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
    /// 计时器只在输入框拿到焦点时跑。小条收起、或焦点跑到查询框上时它停掉——
    /// 待机时进程里没有任何计时器。
    /// </para>
    /// </summary>
    internal sealed class CaretOverlay : Control
    {
        private const int StepMs = 40;
        private const int LogicalWidth = 2;

        private readonly Timer _timer;
        private RichTextBox _box;
        private Control _host;
        private float _scale = 1f;
        private Color _paper = Color.White;
        private Color _ink = Color.Black;
        private int _elapsed;

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

        public void Attach(RichTextBox box, Control host, float scale)
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
            if (_box == null || _host == null || !_box.IsHandleCreated || !_box.Visible ||
                !_host.Visible || !_box.Focused)
            {
                Stop();
                return;
            }

            // 系统光标必须反复藏。RichEdit 在按键、滚动、改选之后会自己把它显示回来，
            // 藏一次是不够的——每个节拍补一次，代价是一次 user32 调用。
            NativeMethods.HideCaret(_box.Handle);

            var at = _box.GetPositionFromCharIndex(_box.SelectionStart);
            Location = _host.PointToClient(_box.PointToScreen(at));
            Width = Math.Max(1, (int)Math.Round(LogicalWidth * _scale));
            Height = _box.Font.Height;

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
            if (Visible) Visible = false;
        }

        private void Apply()
        {
            BackColor = Blend(_paper, _ink, CaretPulse.LevelAt(_elapsed));
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
