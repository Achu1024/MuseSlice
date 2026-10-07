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

        /// <summary>DIAG（临时）：组合字体只记一次，免得每次弹出小条都写一行日志。</summary>
        private bool _imeFontLogged;

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

        /// <summary>
        /// 输入法组合结束。组合期间调用方会跳过一切改格式的操作（那段时间文本归 IME 管），
        /// 所以结束后要补一次高亮。
        /// </summary>
        public event Action CompositionEnded;

        protected override void WndProc(ref Message m)
        {
            // 组合结束时，上屏的文本变更**先于** WM_IME_ENDCOMPOSITION 到达
            // （实测日志里就是这个顺序），所以那一次的 TextChangedByUser 会被
            // 「组合中，跳过」挡掉。得在这里再补一次，否则刚打完的中文没有高亮。
            var endedComposition = m.Msg == NativeMethods.WM_IME_ENDCOMPOSITION;

            if (m.Msg == NativeMethods.WM_IME_STARTCOMPOSITION)
            {
                _composing = true;

                // 每次组合开始前再压一遍组合字体：输入法在某些情况下会把它重置回默认值，
                // 而默认值的行高与正文不同——那正是「文字抖动下沉」的来源。
                NativeMethods.SyncCompositionFont(Handle, Font);
            }
            else if (endedComposition) _composing = false;
            else if (m.Msg == NativeMethods.WM_KILLFOCUS) _composing = false;
            else if (m.Msg == NativeMethods.WM_SETFOCUS)
            {
                // DIAG（临时）：第一次拿到焦点时，把输入法**当前**的组合字体记下来。
                // 如果它和正文字体不一样，就证实了「组合期间行高变了」这条推断；
                // 如果本来就一样，那真凶另有其人，别在这条路上继续修。
                if (!_imeFontLogged)
                {
                    _imeFontLogged = true;
                    QuickNoteApp.Log("DIAG 组合字体同步: " + NativeMethods.SyncCompositionFont(Handle, Font)
                        + "；正文=" + Font.Name + " 高" + Font.Height);
                }
                else
                {
                    NativeMethods.SyncCompositionFont(Handle, Font);
                }
            }
            // 失焦时也必须清掉组合态。若某个输入法在切换/销毁组合时没送来
            // WM_IME_ENDCOMPOSITION，这个标志会永远为真，此后每一次 Enter 都被
            // 吞掉——使用者一个字都存不进去，而且没有任何反馈。宁可偶尔误放行
            // 一次 Enter，也不能让保存功能永久失效。
            base.WndProc(ref m);

            if (endedComposition)
            {
                var h = CompositionEnded;
                if (h != null) h();
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
