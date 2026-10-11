using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MuseSlice
{
    /// <summary>
    /// MuseSlice 标志的矢量绘制。
    /// <para>
    /// 品牌资产是 SVG（<c>icon\MuseSlice_logo.svg</c>），而 WinForms 渲染不了 SVG。
    /// 三条路：引一个 SVG 库（为一个标志加一个依赖，不值）、把这个 900KB 的展示 PNG
    /// 抠出来当位图（DPI 一变就糊，而且那张图上还带着白底和字标）、或者把路径画出来。
    /// 标志本来就只有三段直线加几条二次贝塞尔，所以走第三条：几何直接搬进代码，
    /// 零依赖，缩放不糊。
    /// </para>
    /// <para>
    /// 源坐标取自 SVG 里图标那一组，包围盒 202×241。这里按目标矩形等比缩放居中，
    /// 所以同一个标志在 16px 的托盘图标上和欢迎卡片的大图上都是同一套代码。
    /// </para>
    /// </summary>
    internal static class LogoMark
    {
        // 源坐标系（SVG 用户单位）。所有路径都写在这个盒子里，绘制时整体变换。
        // 启动动画要单独摆布那三块形状，所以这几个是 internal 而不是 private。
        internal const float SourceWidth = 202f;
        internal const float SourceHeight = 241f;

        /// <summary>宽高比。调用方要按它留位时用得上（宽 / 高）。</summary>
        public const float Aspect = SourceWidth / SourceHeight;

        /// <summary>
        /// 标志自己的两个颜色，**不随主题变**——炭黑主形 + 靛蓝切片就是品牌本身。
        /// <para>
        /// 界面里那几处小标志走的是 <see cref="Palette.Brand"/>：它们贴在深浅不同的
        /// 底色上，深色主题下炭黑会直接隐形，必须翻转。启动动画不一样——它浮在桌面
        /// 上、底下是什么颜色我们管不着，这时候翻转反而会让同一段动画在两个主题下
        /// 变成两个样子。品牌就该只有一个样子。
        /// </para>
        /// </summary>
        internal static readonly Color Ink = Color.FromArgb(0x17, 0x1B, 0x2A);
        internal static readonly Color Accent = Color.FromArgb(0x5B, 0x43, 0xF5);

        /// <summary>按目标矩形居中绘制标志。</summary>
        public static void Draw(Graphics g, Rectangle box, Color brand, Color accent)
        {
            if (box.Width <= 0 || box.Height <= 0) return;

            var scale = Math.Min(box.Width / SourceWidth, box.Height / SourceHeight);
            var ox = box.X + (box.Width - SourceWidth * scale) / 2f;
            var oy = box.Y + (box.Height - SourceHeight * scale) / 2f;

            // 直接用仿射矩阵的 (e, f) 放偏移，不叠 Translate/Scale——那两个的叠加顺序
            // 取决于 MatrixOrder，写反了就是「先平移再缩放」，位置会算错。
            using (var m = new Matrix(scale, 0, 0, scale, ox, oy))
            {
                var mode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;   // 小尺寸下没有它边缘全是锯齿

                try
                {
                    Fill(g, MainPath(), m, brand);
                    Fill(g, CutPath(), m, brand);
                    Fill(g, SlicePath(), m, accent);
                }
                finally
                {
                    g.SmoothingMode = mode;
                }
            }
        }

        /// <summary>渲染成透明底位图。托盘的 <see cref="Icon"/> 由它转。</summary>
        public static Bitmap RenderBitmap(int width, int height, Color brand, Color accent)
        {
            var bmp = new Bitmap(Math.Max(1, width), Math.Max(1, height),
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Draw(g, new Rectangle(0, 0, bmp.Width, bmp.Height), brand, accent);
            }
            return bmp;
        }

        private static void Fill(Graphics g, GraphicsPath path, Matrix m, Color color)
        {
            using (path)
            using (var brush = new SolidBrush(color))
            {
                path.Transform(m);
                g.FillPath(brush, path);
            }
        }

        /// <summary>
        /// 二次贝塞尔升阶成三次。GDI+ 只认 <c>AddBezier</c>，SVG 的 <c>Q</c> 是二次的；
        /// 升阶是精确等价的，不是近似——控制点取 2/3 处即可。
        /// </summary>
        private static void Quad(GraphicsPath path, PointF from, PointF control, PointF to)
        {
            var c1 = new PointF(from.X + 2f / 3f * (control.X - from.X),
                                from.Y + 2f / 3f * (control.Y - from.Y));
            var c2 = new PointF(to.X + 2f / 3f * (control.X - to.X),
                                to.Y + 2f / 3f * (control.Y - to.Y));
            path.AddBezier(from, c1, c2, to);
        }

        /// <summary>主体：朝右的播放/切片形，左边留一道圆滑的凹口。</summary>
        internal static GraphicsPath MainPath()
        {
            var p = new GraphicsPath();
            Quad(p, new PointF(0, 22), new PointF(0, 0), new PointF(19, 11));
            p.AddLine(new PointF(19, 11), new PointF(142, 88));
            Quad(p, new PointF(142, 88), new PointF(158, 98), new PointF(142, 108));
            p.AddLine(new PointF(142, 108), new PointF(19, 185));
            Quad(p, new PointF(19, 185), new PointF(0, 197), new PointF(0, 174));
            p.CloseFigure();
            return p;
        }

        /// <summary>右上角那一刀，与主体之间留出白色缝隙。</summary>
        internal static GraphicsPath CutPath()
        {
            var p = new GraphicsPath();
            p.AddLine(new PointF(122, 60), new PointF(178, 25));
            Quad(p, new PointF(178, 25), new PointF(194, 15), new PointF(194, 35));
            p.AddLine(new PointF(194, 35), new PointF(194, 101));
            Quad(p, new PointF(194, 101), new PointF(194, 118), new PointF(179, 109));
            p.CloseFigure();
            return p;
        }

        /// <summary>右下那片靛蓝。整块界面里唯一的强调色，就在这一片上。</summary>
        internal static GraphicsPath SlicePath()
        {
            var p = new GraphicsPath();
            p.AddLine(new PointF(54, 197), new PointF(151, 137));
            Quad(p, new PointF(151, 137), new PointF(164, 129), new PointF(176, 140));
            p.AddLine(new PointF(176, 140), new PointF(193, 157));
            Quad(p, new PointF(193, 157), new PointF(202, 166), new PointF(190, 174));
            p.AddLine(new PointF(190, 174), new PointF(94, 234));
            Quad(p, new PointF(94, 234), new PointF(82, 241), new PointF(70, 232));
            p.AddLine(new PointF(70, 232), new PointF(53, 216));
            Quad(p, new PointF(53, 216), new PointF(43, 207), new PointF(54, 197));
            p.CloseFigure();
            return p;
        }
    }
}
