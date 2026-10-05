using System;
using System.Globalization;

namespace QuickNote.Core
{
    /// <summary>笔记文件与时间戳的格式化。数字部分一律用不变文化，避免受系统区域设置影响。</summary>
    public static class DateFormat
    {
        public const string FileNamePattern = "yyyy-MM-dd";
        public const string StampPattern = "HH:mm";

        public static string FileName(DateTime when)
        {
            return when.ToString(FileNamePattern, CultureInfo.InvariantCulture);
        }

        public static string Stamp(DateTime when)
        {
            return when.ToString(StampPattern, CultureInfo.InvariantCulture);
        }

        public static string Heading(DateTime when)
        {
            return Heading(when, CultureInfo.CurrentCulture);
        }

        /// <summary>文件首行，例如「# 2026-10-05 星期一」。星期名随传入文化。</summary>
        public static string Heading(DateTime when, CultureInfo culture)
        {
            return "# " + FileName(when) + " " + when.ToString("dddd", culture);
        }

        /// <summary>顶部 chrome 用的短标题，例如「10-05 星期一」。不带年份。</summary>
        public static string ShortHeading(DateTime when)
        {
            return when.ToString("MM-dd", CultureInfo.InvariantCulture)
                + " " + when.ToString("dddd", CultureInfo.CurrentCulture);
        }

        /// <summary>判断一行是否是程序生成的记录起始行。格式细节见 <see cref="EntryLine"/>。</summary>
        public static bool IsEntryStart(string line)
        {
            return EntryLine.IsEntryStart(line);
        }
    }
}
