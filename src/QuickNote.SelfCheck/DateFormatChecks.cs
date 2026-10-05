using System;
using System.Globalization;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class DateFormatChecks
    {
        public static void Run()
        {
            Check.Section("DateFormat");

            var t = new DateTime(2026, 10, 5, 14, 3, 0);

            Check.Equal(DateFormat.FileName(t), "2026-10-05", "文件名用 ISO 日期");
            Check.Equal(DateFormat.Stamp(t), "14:03", "时间戳 24 小时制、分钟补零");

            var zh = CultureInfo.GetCultureInfo("zh-CN");
            Check.Equal(DateFormat.Heading(t, zh), "# 2026-10-05 星期一", "标题行含中文星期");

            Check.Equal(DateFormat.Stamp(new DateTime(2026, 1, 1, 0, 0, 0)), "00:00",
                "零点显示为 00:00 而不是 24:00");
            Check.Equal(DateFormat.FileName(new DateTime(2026, 1, 9, 0, 0, 0)), "2026-01-09",
                "月与日补零");

            Check.Equal(DateFormat.EntryPrefix(t), "- 14:03 ", "记录行前缀格式");

            Check.That(DateFormat.IsEntryStart("- 14:03 跟老王确认了接口字段"),
                "识别程序生成的记录行");
            Check.That(DateFormat.IsEntryStart("- 00:00 零点"),
                "零点整的记录行也能识别");
            Check.That(!DateFormat.IsEntryStart("- [ ] 待办"),
                "顶层待办列表项不是记录行");
            Check.That(!DateFormat.IsEntryStart("- 一条普通列表"),
                "普通列表项不是记录行");
            Check.That(!DateFormat.IsEntryStart("- 9:05 小时没补零"),
                "小时未补零不算记录行");
            Check.That(!DateFormat.IsEntryStart("14:03 缺了短横"),
                "没有短横开头不算记录行");
            Check.That(!DateFormat.IsEntryStart("- 14:03"),
                "只有时间戳没有尾随空格不算记录行");
            Check.That(!DateFormat.IsEntryStart(""), "空行不是记录行");
            Check.That(!DateFormat.IsEntryStart(null), "null 不是记录行");
        }
    }
}
