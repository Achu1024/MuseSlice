using MuseSlice.Core;

namespace MuseSlice.SelfCheck
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

            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Space), SearchKeyAction.ToggleDone,
                "查询框为空时空格切换完成态");
            Check.Equal(InputRules.DecideSearchKey("老王", false, SearchKey.Space), SearchKeyAction.PassThrough,
                "查询框有内容时空格是正文里的空格，不能被抢走");
            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Backspace), SearchKeyAction.AskDelete,
                "查询框为空时 Backspace 进入删除确认态");
            Check.Equal(InputRules.DecideSearchKey("老", false, SearchKey.Backspace), SearchKeyAction.PassThrough,
                "查询框有内容时 Backspace 就是删字符");
            Check.Equal(InputRules.DecideSearchKey("", true, SearchKey.Enter), SearchKeyAction.ConfirmDelete,
                "确认态下 Enter 的语义被覆盖成确认删除");
            Check.Equal(InputRules.DecideSearchKey("", true, SearchKey.Escape), SearchKeyAction.CancelDelete,
                "确认态下 Esc 取消");
            Check.Equal(InputRules.DecideSearchKey("", true, SearchKey.Space), SearchKeyAction.PassThrough,
                "确认态下空格不再切换完成态");
            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Enter), SearchKeyAction.PassThrough,
                "非确认态下 Enter 交给上层打开文件");
            Check.Equal(InputRules.DecideSearchKey("", false, SearchKey.Other), SearchKeyAction.PassThrough,
                "其他键一律放行");
        }
    }
}
