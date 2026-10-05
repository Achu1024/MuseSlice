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
        }
    }
}
