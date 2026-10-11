using System;
using System.Runtime.InteropServices;

namespace MuseSlice
{
    internal static class NativeMethods
    {
        /// <summary>自定义消息：另一个实例要求本实例弹出小条。</summary>
        internal const int WM_APP_SHOW = 0x8000 + 1;
        internal const int WM_APP_EXIT = 0x8000 + 2;

        internal const int WM_SETREDRAW = 0x000B;
        internal const int WM_KEYDOWN = 0x0100;
        internal const int WM_KEYUP = 0x0101;
        internal const int WM_SYSKEYDOWN = 0x0104;
        internal const int WM_SYSKEYUP = 0x0105;

        internal const int WM_IME_STARTCOMPOSITION = 0x010D;
        internal const int WM_IME_ENDCOMPOSITION = 0x010E;
        internal const int WM_SETFOCUS = 0x0007;
        internal const int WM_KILLFOCUS = 0x0008;

        // ---- 输入法组合字体 ----
        // 中文打字时文字会抖、往下沉：IME 用**自己默认的组合字体**画组合串，那个字体的
        // 行高与正文字体不同，于是行盒高度在组合前后变化，整行文字就跟着上下跳。
        // 对策是在拿到焦点、以及每次组合开始前，把组合字体同步成正文字体。

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct LOGFONT
        {
            public int lfHeight;
            public int lfWidth;
            public int lfEscapement;
            public int lfOrientation;
            public int lfWeight;
            public byte lfItalic;
            public byte lfUnderline;
            public byte lfStrikeOut;
            public byte lfCharSet;
            public byte lfOutPrecision;
            public byte lfClipPrecision;
            public byte lfQuality;
            public byte lfPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string lfFaceName;
        }

        [DllImport("imm32.dll")]
        internal static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmGetCompositionFont(IntPtr hIMC, ref LOGFONT lf);

        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImmSetCompositionFont(IntPtr hIMC, ref LOGFONT lf);

        /// <summary>把输入法的组合字体同步成控件的字体。返回同步前它的样子（供诊断）。</summary>
        internal static string SyncCompositionFont(IntPtr hWnd, System.Drawing.Font font)
        {
            if (font == null) return "(没有字体)";

            var imc = ImmGetContext(hWnd);
            if (imc == IntPtr.Zero) return "(没有输入法上下文)";

            try
            {
                var before = new LOGFONT();
                var was = ImmGetCompositionFont(imc, ref before)
                    ? before.lfFaceName + " 高" + before.lfHeight
                    : "(读不到)";

                // 手工填，不用 Font.ToLogFont(object)：那个方法靠反射往传入对象上写字段，
                // 而结构体会被装箱成一份副本——方法填的是箱子，本地这个字段一个都不会变
                // （实测就是这样：日志里打出 `→ 高0`，字体名是空的）。
                //
                // lfHeight 的关键：
                // - 正值 = 字符单元格高度（包含行间距），对应 Font.Height
                // - 负值 = 字符高度（不含行间距），以逻辑单位表示
                //
                // 原先用 Font.Height（行距），导致组合期间行高变大，文字下沉。
                // 改用负值表示字符高度：-font.SizeInPoints 转换为设备单位。
                // 公式：点数 * DPI / 72，但 LOGFONT 会自动处理，直接用点数即可。
                var want = new LOGFONT
                {
                    lfHeight = -(int)Math.Round(font.SizeInPoints),
                    lfWeight = font.Bold ? 700 : 400,
                    lfItalic = (byte)(font.Italic ? 1 : 0),
                    lfUnderline = 0,
                    lfStrikeOut = 0,
                    lfCharSet = font.GdiCharSet,
                    lfOutPrecision = 0,
                    lfClipPrecision = 0,
                    lfQuality = 5,          // CLEARTYPE_QUALITY，与正文的渲染方式一致
                    lfPitchAndFamily = 0,
                    lfFaceName = font.Name
                };

                ImmSetCompositionFont(imc, ref want);

                return was + "  →  " + want.lfFaceName + " 高" + want.lfHeight;
            }
            catch
            {
                return "(同步时出错)";
            }
            finally
            {
                ImmReleaseContext(hWnd, imc);
            }
        }

        /// <summary>藏掉系统画的插入符。自绘光标要用它把 RichEdit 那根 1px 黑条顶掉。</summary>
        [DllImport("user32.dll")]
        internal static extern bool HideCaret(IntPtr hWnd);

        /// <summary>获取系统插入符的位置（相对于窗口客户区）。</summary>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCaretPos(out POINT lpPoint);

        /// <summary>
        /// EM_POSFROMCHAR：从字符索引获取位置。对 RichEdit 比 GetPositionFromCharIndex 更可靠。
        /// wParam = 字符索引，返回值的 LOWORD 是 X，HIWORD 是 Y（相对客户区）。
        /// </summary>
        internal const int EM_POSFROMCHAR = 0x00D6;

        /// <summary>EM_SETPARAFORMAT：设置段落格式。</summary>
        internal const int EM_SETPARAFORMAT = 0x0447;

        /// <summary>段落格式标志：行间距类型。</summary>
        internal const int PFM_LINESPACING = 0x00000100;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PARAFORMAT2
        {
            public int cbSize;
            public uint dwMask;
            public short wNumbering;
            public short wReserved;
            public int dxStartIndent;
            public int dxRightIndent;
            public int dxOffset;
            public short wAlignment;
            public short cTabCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] rgxTabs;
            public int dySpaceBefore;
            public int dySpaceAfter;
            public int dyLineSpacing;
            public short sStyle;
            public byte bLineSpacingRule;
            public byte bOutlineLevel;
            public short wShadingWeight;
            public short wShadingStyle;
            public short wNumberingStart;
            public short wNumberingStyle;
            public short wNumberingTab;
            public short wBorderSpace;
            public short wBorderWidth;
            public short wBorders;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref PARAFORMAT2 lParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        // ---- 抢前台（Task 15）----
        // 后台进程直接被 SetForegroundWindow 拒绝是 Windows 的设计，
        // 绕法是临时把本线程的输入队列附加到当前前台线程上。

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BringWindowToTop(IntPtr hWnd);

        /// <summary>
        /// 把窗口抢到前台并让它真正接收键盘。后台进程直接调 SetForegroundWindow
        /// 会被系统拒绝，于是「弹出来了但打字打到了别的窗口」——必须先把本线程的
        /// 输入队列附加到当前前台线程上，抢完再摘掉。
        /// </summary>
        internal static void ForceForeground(IntPtr hWnd)
        {
            var foreground = GetForegroundWindow();
            var foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
            var myThread = GetCurrentThreadId();

            if (foregroundThread == myThread || foregroundThread == 0)
            {
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
                return;
            }

            // 后台进程直接 SetForegroundWindow 会被前台锁拒绝，表现为
            // 「小条画出来了但打字打到别的窗口」。把本线程的输入队列临时附加到
            // 当前前台线程上，抢完再摘掉——实测 attach=True set=True err=0，
            // 前台确实变成了小条。
            AttachThreadInput(myThread, foregroundThread, true);
            try
            {
                BringWindowToTop(hWnd);
                SetForegroundWindow(hWnd);
            }
            finally
            {
                AttachThreadInput(myThread, foregroundThread, false);
            }
        }

        // ---- 窗口外观（Task 8）----

        /// <summary>让回执窗口只被画出来、不进前台，也不出现在 Alt+Tab 里。</summary>
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;

        internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        internal const int DWMWCP_ROUND = 2;
        internal const int CS_DROPSHADOW = 0x00020000;

        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        /// <summary>窗口所在显示器的实际 DPI。比 Control.DeviceDpi 可靠。</summary>
        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hwnd);

        // ---- 全局键盘钩子与热键（Task 10）----

        internal const int WH_KEYBOARD_LL = 13;
        internal const int VK_LCONTROL = 0xA2;
        internal const int WM_HOTKEY = 0x0312;

        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_WIN = 0x0008;
        internal const uint MOD_NOREPEAT = 0x4000;

        internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct KBDLLHOOKSTRUCT
        {
            internal uint vkCode;
            internal uint scanCode;
            internal uint flags;
            internal uint time;
            internal IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // ---- 分层窗口：每像素透明 ----
        // 色键（TransparencyKey）只能做硬边——抗锯齿的边会和键色混出一圈紫红。
        // 要画圆角、柔边、半透明，就得走 UpdateLayeredWindow：整幅位图连同每像素的
        // alpha 一起推给系统，由它去合成。代价是每帧一次全幅内存拷贝，所以窗口开得
        // 越小越好。

        internal const int WS_EX_LAYERED = 0x00080000;
        internal const int ULW_ALPHA = 0x00000002;
        internal const byte AC_SRC_OVER = 0x00;
        internal const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;      // 负值 = 自上而下，与 GDI+ 位图的行序一致
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;    // 32bpp BI_RGB 不用调色板，留一个占位即可
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        internal static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi,
            uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);
    }
}
