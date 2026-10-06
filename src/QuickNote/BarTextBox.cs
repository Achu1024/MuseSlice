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
            if (m.Msg == NativeMethods.WM_IME_STARTCOMPOSITION) _composing = true;
            else if (m.Msg == NativeMethods.WM_IME_ENDCOMPOSITION) _composing = false;
            else if (m.Msg == NativeMethods.WM_KILLFOCUS) _composing = false;
            // 失焦时也必须清掉组合态。若某个输入法在切换/销毁组合时没送来
            // WM_IME_ENDCOMPOSITION，这个标志会永远为真，此后每一次 Enter 都被
            // 吞掉——使用者一个字都存不进去，而且没有任何反馈。宁可偶尔误放行
            // 一次 Enter，也不能让保存功能永久失效。
            base.WndProc(ref m);
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
