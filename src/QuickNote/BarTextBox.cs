using System;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// RichTextBox 子类。两件事：
    /// 1) 跟踪输入法组合状态 —— 拼音候选框开着时的 Enter 必须被忽略（见 InputRules）。
    ///    正常情况输入法会吞掉按键、程序收不到，这里再加一道防线。
    /// 2) 暴露一个「文本变了」事件，供高亮器挂载。
    /// </summary>
    internal sealed class BarTextBox : RichTextBox
    {
        private bool _composing;
        private bool _suppress;

        public BarTextBox()
        {
            BorderStyle = BorderStyle.None;
            Multiline = true;
            WordWrap = true;
            ScrollBars = RichTextBoxScrollBars.Vertical;
            DetectUrls = false;
            AcceptsTab = false;          // Tab 由我们自己处理
            ShortcutsEnabled = true;
        }

        /// <summary>拼音/日文等输入法的组合态。组合中时 Enter 的语义是「上屏候选词」。</summary>
        public bool IsComposing { get { return _composing; } }

        /// <summary>文本确实发生变化时触发。重着色只改格式、不改文本，因此不会触发它。</summary>
        public event Action TextChangedByUser;

        protected override void WndProc(ref Message m)
        {
            // DIAG（临时，查完输入法问题就删）：组合到底有没有开始、输入框有没有中途失焦。
            if (m.Msg == NativeMethods.WM_IME_STARTCOMPOSITION)
            {
                _composing = true;
                QuickNoteApp.Log("DIAG IME start-composition");
            }
            else if (m.Msg == NativeMethods.WM_IME_ENDCOMPOSITION)
            {
                _composing = false;
                QuickNoteApp.Log("DIAG IME end-composition");
            }
            else if (m.Msg == NativeMethods.WM_KILLFOCUS)
            {
                _composing = false;
                QuickNoteApp.Log("DIAG input blur");
            }
            else if (m.Msg == NativeMethods.WM_SETFOCUS)
            {
                QuickNoteApp.Log("DIAG input focus; " + DescribeIme());
            }
            // 失焦时也必须清掉组合态。若某个输入法在切换/销毁组合时没送来
            // WM_IME_ENDCOMPOSITION，这个标志会永远为真，此后每一次 Enter 都被
            // 吞掉——使用者一个字都存不进去，而且没有任何反馈。宁可偶尔误放行
            // 一次 Enter，也不能让保存功能永久失效。
            base.WndProc(ref m);
        }

        /// <summary>
        /// DIAG（临时）：把「输入法到底有没有接上这个窗口、是不是在中文模式」写进日志。
        /// 组合消息一直不来的话，光看组合事件分不清是「输入法没接上」还是「输入法在英文模式」。
        /// </summary>
        private string DescribeIme()
        {
            var font = "字体=" + Font.Name + "/" + Font.SizeInPoints.ToString("0.#") + "pt";

            var imc = NativeMethods.ImmGetContext(Handle);
            if (imc == IntPtr.Zero) return "输入法上下文=无; " + font;

            try
            {
                int conversion, sentence;
                if (!NativeMethods.ImmGetConversionStatus(imc, out conversion, out sentence))
                    return "输入法上下文=有，转换状态读不到; " + font;

                var native = (conversion & NativeMethods.IME_CMODE_NATIVE) != 0;
                return "输入法上下文=有，转换状态=0x" + conversion.ToString("X4")
                    + (native ? "（中文模式）" : "（英文/字母数字模式）")
                    + "; " + font;
            }
            finally
            {
                NativeMethods.ImmReleaseContext(Handle, imc);
            }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            if (_suppress) return;
            var h = TextChangedByUser;
            if (h != null) h();
        }

        /// <summary>在不再触发 TextChangedByUser 的前提下替换全文（供测试与程序化填充使用）。</summary>
        public void ReplaceTextSilently(string text)
        {
            _suppress = true;
            try { Text = text; }
            finally { _suppress = false; }
        }
    }
}
