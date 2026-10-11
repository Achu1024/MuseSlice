using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MuseSlice
{
    /// <summary>
    /// 启动动画：标志从 94% 放大淡入，靛蓝切片晚一步从主体左下角滑上来就位。
    /// <para>
    /// 走的是**分层窗口**（<see cref="NativeMethods.UpdateLayeredWindow"/>）而不是色键：
    /// 标志全是曲线，色键下抗锯齿的边会和键色混出一圈紫红描边，只能画硬边——那正是
    /// 上一版看着糙的原因。分层窗口把整幅带每像素 alpha 的位图交给系统合成，圆角、
    /// 柔边、半透明才都成立。
    /// </para>
    /// <para>
    /// **绝不抢焦点、也绝不吃鼠标**：除了 WS_EX_NOACTIVATE，命中测试一律答成
    /// HTTRANSPARENT。动画只有一秒多，但那一秒多里点什么都点不动是不能接受的。
    /// </para>
    /// <para>
    /// 窗口只开标志那么大（加一圈余量），不铺满屏幕：每帧都要把整幅位图做一次预乘
    /// 并推给系统，窗口越大这趟白工越贵，而它并不需要用到那些空白。
    /// </para>
    /// </summary>
    internal sealed class SplashOverlay : Form
    {
        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        private const int StepMs = 16;          // ~60fps

        // ---- 时间线（毫秒）----
        private const int MainFadeMs = 620;     // 主体淡入并放大到位
        private const int SliceDelayMs = 150;   // 切片晚这么多起步
        private const int SliceFadeMs = 560;
        private const int HoldUntilMs = 1420;   // 到这儿开始收
        private const int FadeOutMs = 400;
        private const int TotalMs = HoldUntilMs + FadeOutMs;

        /// <summary>标志聚成后有多高（逻辑像素）。</summary>
        private const int LogicalLogoHeight = 160;

        /// <summary>
        /// 窗口比标志大一圈。切片起点要往左下探出去一截，不留余量就会被窗口边缘切掉；
        /// 缩放是从 94% 往上长，只会更小，不占余量。
        /// </summary>
        private const int LogicalMargin = 28;

        /// <summary>缩放起点，终点是 1.0。幅度再大就不像「浮现」而像「弹出来」了。</summary>
        private const double StartScale = 0.94;

        private readonly Timer _clock;
        private readonly ImageAttributes _fade = new ImageAttributes();

        private Bitmap _canvas;      // 每帧清清重画、推给系统的那一幅
        private Bitmap _art;         // 只画标志本身；切片就位后就不再重画
        private byte[] _pixels;      // 预乘用的中转缓冲，复用以免每帧分配

        private IntPtr _memDc, _dib, _dibBits, _oldDib;
        private int _canvasWidth, _canvasHeight;

        private double _sliceProgress = -1;   // 上次画 _art 时切片的进度
        private float _scale = 1f;
        private int _logoHeight;
        private int _elapsed;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                            | NativeMethods.WS_EX_NOACTIVATE
                            | NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        /// <summary>
        /// 不需要配色：标志用的是它自己那两个固定颜色（见 <see cref="LogoMark.Ink"/>），
        /// 不跟主题走。
        /// </summary>
        public SplashOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;

            _clock = new Timer { Interval = StepMs };
            _clock.Tick += delegate { Step(); };
        }

        /// <summary>放一遍。自己负责收尾：播完关闭并释放，调用方不用管。</summary>
        public void Play()
        {
            // 句柄先定下来，之后 DPI 才可查
            var unused = Handle;

            try
            {
                var dpi = NativeMethods.GetDpiForWindow(Handle);
                if (dpi >= 96) _scale = dpi / 96f;
            }
            catch { /* 老系统上没有这个 API，保持 1.0 */ }

            _logoHeight = S(LogicalLogoHeight);

            var margin = S(LogicalMargin);
            _canvasWidth = (int)Math.Round(_logoHeight * LogoMark.Aspect) + margin * 2;
            _canvasHeight = _logoHeight + margin * 2;

            ClientSize = new Size(_canvasWidth, _canvasHeight);

            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Left = wa.Left + (wa.Width - _canvasWidth) / 2;
            Top = wa.Top + (wa.Height - _canvasHeight) / 2;

            _canvas = new Bitmap(_canvasWidth, _canvasHeight, PixelFormat.Format32bppArgb);
            _art = new Bitmap(_canvasWidth, _canvasHeight, PixelFormat.Format32bppArgb);
            _pixels = new byte[_canvasWidth * _canvasHeight * 4];

            CreateDib();

            _elapsed = 0;
            Show();
            _clock.Start();
        }

        private int S(int logical) { return (int)Math.Round(logical * _scale); }

        private void Step()
        {
            _elapsed += StepMs;

            if (_elapsed >= TotalMs)
            {
                Render();       // 补一帧全透明的，免得最后停在半透明上被硬切掉
                _clock.Stop();
                Close();
                return;
            }

            Render();
        }

        private void Render()
        {
            var mainProgress = Clamp01(_elapsed / (double)MainFadeMs);
            var sliceProgress = Clamp01((_elapsed - SliceDelayMs) / (double)SliceFadeMs);
            var outProgress = _elapsed > HoldUntilMs
                ? Clamp01((_elapsed - HoldUntilMs) / (double)FadeOutMs)
                : 0;

            var alpha = (1 - outProgress) * mainProgress;

            // 标志本身只在切片还在动的时候需要重画；它一旦到位就是张不动的图。
            if (Math.Abs(sliceProgress - _sliceProgress) > 0.0005)
            {
                PaintArt(sliceProgress);
                _sliceProgress = sliceProgress;
            }

            using (var g = Graphics.FromImage(_canvas))
            {
                g.Clear(Color.Transparent);

                if (alpha > 0)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    var scale = StartScale + (1 - StartScale) * EaseOutCubic(mainProgress);
                    var w = (int)Math.Round(_canvasWidth * scale);
                    var h = (int)Math.Round(_canvasHeight * scale);

                    // 缩放围绕画布中心，于是标志是在原地长出来的，不位移
                    var dest = new Rectangle((_canvasWidth - w) / 2, (_canvasHeight - h) / 2, w, h);

                    _fade.SetColorMatrix(new ColorMatrix { Matrix33 = (float)alpha });
                    g.DrawImage(_art, dest, 0, 0, _canvasWidth, _canvasHeight,
                        GraphicsUnit.Pixel, _fade);
                }
            }

            Push();
        }

        /// <summary>
        /// 把标志整组画进 <see cref="_art"/>，不透明、不带缩放——缩放与淡入交给
        /// 合成那一步。这样分是因为片与片之间是**压盖**关系：如果带着半透明逐片画，
        /// 主体压住切片的那块地方会混成一片紫糊。
        /// </summary>
        private void PaintArt(double sliceProgress)
        {
            using (var g = Graphics.FromImage(_art))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 把标志的源坐标（202×241）摆到画布正中
                var k = _logoHeight / LogoMark.SourceHeight;
                g.TranslateTransform((_canvasWidth - LogoMark.SourceWidth * k) / 2f,
                                     (_canvasHeight - LogoMark.SourceHeight * k) / 2f);
                g.ScaleTransform(k, k);

                // 靛蓝切片在前，从主体的左下方滑上来
                var slide = EaseOutCubic(sliceProgress);
                var state = g.Save();
                g.TranslateTransform((float)((1 - slide) * -16), (float)((1 - slide) * 22));
                using (var brush = new SolidBrush(LogoMark.Accent))
                using (var slice = LogoMark.SlicePath())
                {
                    g.FillPath(brush, slice);
                }
                g.Restore(state);

                // 主体盖在切片上面——「从底下滑出来」才成立
                using (var brush = new SolidBrush(LogoMark.Ink))
                using (var main = LogoMark.MainPath())
                using (var cut = LogoMark.CutPath())
                {
                    g.FillPath(brush, main);
                    g.FillPath(brush, cut);
                }
            }
        }

        /// <summary>
        /// 建一块 32bpp 的自上而下 DIB，内容是空的，往后的帧都往上头写。
        /// <para>
        /// 不用 <c>Bitmap.GetHbitmap</c>：那条路会不会保住 alpha 通道，说法不一，
        /// 而这里要的是**确定**——DIB 的内存是我们自己拿着的，写什么就是什么。
        /// </para>
        /// </summary>
        private void CreateDib()
        {
            var screenDc = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                _memDc = NativeMethods.CreateCompatibleDC(screenDc);

                var info = new NativeMethods.BITMAPINFO
                {
                    bmiHeader = new NativeMethods.BITMAPINFOHEADER
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER)),
                        biWidth = _canvasWidth,
                        biHeight = -_canvasHeight,   // 负值 = 自上而下
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = 0            // BI_RGB
                    }
                };

                _dib = NativeMethods.CreateDIBSection(screenDc, ref info, 0, out _dibBits,
                    IntPtr.Zero, 0);
                _oldDib = NativeMethods.SelectObject(_memDc, _dib);
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        /// <summary>
        /// 把画好的位图搬进 DIB，顺手做预乘。
        /// <para>
        /// UpdateLayeredWindow 要的是**预乘**过的 ARGB——RGB 已经乘过 alpha。而 GDI+ 交
        /// 出来的是未预乘的。直接推上去，凡是不完全透明的地方都会偏亮：淡入那几百毫秒
        /// 里标志会发白，到不透明才落回本色。反正这一趟本来就要拷，乘法捎带做掉。
        /// </para>
        /// </summary>
        private void Push()
        {
            var rect = new Rectangle(0, 0, _canvasWidth, _canvasHeight);
            var data = _canvas.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                // 32bpp 的行已经是 4 字节对齐，Stride 必然等于 宽×4，所以可以整块拷。
                Marshal.Copy(data.Scan0, _pixels, 0, _pixels.Length);

                for (var i = 0; i < _pixels.Length; i += 4)
                {
                    var a = _pixels[i + 3];
                    if (a == 255) continue;

                    if (a == 0)
                    {
                        _pixels[i] = 0;
                        _pixels[i + 1] = 0;
                        _pixels[i + 2] = 0;
                        continue;
                    }

                    _pixels[i] = (byte)(_pixels[i] * a / 255);
                    _pixels[i + 1] = (byte)(_pixels[i + 1] * a / 255);
                    _pixels[i + 2] = (byte)(_pixels[i + 2] * a / 255);
                }

                Marshal.Copy(_pixels, 0, _dibBits, _pixels.Length);
            }
            finally
            {
                _canvas.UnlockBits(data);
            }

            var screenDc = NativeMethods.GetDC(IntPtr.Zero);
            try
            {
                var destination = new NativeMethods.POINT { x = Left, y = Top };
                var size = new NativeMethods.SIZE { cx = _canvasWidth, cy = _canvasHeight };
                var source = new NativeMethods.POINT { x = 0, y = 0 };
                var blend = new NativeMethods.BLENDFUNCTION
                {
                    BlendOp = NativeMethods.AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,               // 每像素的 alpha 说了算
                    AlphaFormat = NativeMethods.AC_SRC_ALPHA
                };

                NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref destination, ref size,
                    _memDc, ref source, 0, ref blend, NativeMethods.ULW_ALPHA);
            }
            finally
            {
                NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private static double Clamp01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }

        private static double EaseOutCubic(double x) { return 1 - Math.Pow(1 - x, 3); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                // 动画这一秒多里，点击必须穿过去落到下面的窗口上。
                m.Result = new IntPtr(HTTRANSPARENT);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_clock != null) { _clock.Stop(); _clock.Dispose(); }

                if (_memDc != IntPtr.Zero)
                {
                    if (_dib != IntPtr.Zero) NativeMethods.SelectObject(_memDc, _oldDib);
                    if (_dib != IntPtr.Zero) NativeMethods.DeleteObject(_dib);
                    NativeMethods.DeleteDC(_memDc);
                    _memDc = IntPtr.Zero;
                    _dib = IntPtr.Zero;
                }

                if (_fade != null) _fade.Dispose();
                if (_canvas != null) _canvas.Dispose();
                if (_art != null) _art.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
