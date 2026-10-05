namespace QuickNote.Core
{
    public enum TabAction
    {
        SwitchToSearch,
        SwitchToRecord,
        Indent
    }

    public enum EnterAction
    {
        Save,
        Newline,
        Ignore
    }

    /// <summary>
    /// 按键判定规则。故意做成纯函数：这几个分支是最容易出错、
    /// 又最难靠手动敲键盘验证的地方（尤其是输入法组合态）。
    /// </summary>
    public static class InputRules
    {
        public const int IndentWidth = 2;

        /// <summary>
        /// Tab 在文本框里本来是缩进键，直接占用会与写嵌套列表冲突。
        /// 所以记录模式下仅在输入框为空时切换模式——而按热键弹出小条时
        /// 输入框必然是空的，那正是唯一会想到「我要搜个东西」的时刻。
        /// </summary>
        public static TabAction DecideTab(BarMode mode, bool inputIsEmpty)
        {
            if (mode == BarMode.Search) return TabAction.SwitchToRecord;
            return inputIsEmpty ? TabAction.SwitchToSearch : TabAction.Indent;
        }

        /// <summary>
        /// 拼音候选框开着时 Enter 的语义是「上屏候选词」而非保存。
        /// 若被当作保存处理，使用者每选一次词就会保存并关闭小条一次，软件完全不可用。
        /// 正常情况下输入法会在组合状态吞掉按键、程序收不到；
        /// 这里再做一道防线，只认「非组合态」的 Enter。
        /// </summary>
        public static EnterAction DecideEnter(bool isComposing, bool shiftHeld)
        {
            if (isComposing) return EnterAction.Ignore;
            return shiftHeld ? EnterAction.Newline : EnterAction.Save;
        }
    }
}
