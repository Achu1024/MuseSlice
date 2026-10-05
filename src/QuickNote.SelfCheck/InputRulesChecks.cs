using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class InputRulesChecks
    {
        public static void Run()
        {
            Check.Section("InputRules");

            Check.Equal(InputRules.DecideTab(BarMode.Record, true), TabAction.SwitchToSearch,
                "记录模式 + 输入框为空 + Tab → 切到搜索");
            Check.Equal(InputRules.DecideTab(BarMode.Record, false), TabAction.Indent,
                "记录模式 + 有内容 + Tab → 缩进");
            Check.Equal(InputRules.DecideTab(BarMode.Search, true), TabAction.SwitchToRecord,
                "搜索模式 + 空查询 + Tab → 切回记录");
            Check.Equal(InputRules.DecideTab(BarMode.Search, false), TabAction.SwitchToRecord,
                "搜索模式 + 有查询 + Tab → 仍切回记录");

            Check.Equal(InputRules.IndentWidth, 2, "缩进宽度为 2 个空格");

            Check.Equal(InputRules.DecideEnter(false, false), EnterAction.Save,
                "普通 Enter → 保存");
            Check.Equal(InputRules.DecideEnter(false, true), EnterAction.Newline,
                "Shift+Enter → 换行");
            Check.Equal(InputRules.DecideEnter(true, false), EnterAction.Ignore,
                "中文输入法组合态按 Enter → 忽略，不保存不关闭");
            Check.Equal(InputRules.DecideEnter(true, true), EnterAction.Ignore,
                "中文输入法组合态按 Shift+Enter → 也忽略，交给输入法处理");
        }
    }
}
