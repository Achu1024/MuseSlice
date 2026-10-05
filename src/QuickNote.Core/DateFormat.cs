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

        /// <summary>文件首行，例如「# 2026-10-05 星期日」。星期名随传入文化。</summary>
        public static string Heading(DateTime when, CultureInfo culture)
        {
            return "# " + FileName(when) + " " + when.ToString("dddd", culture);
        }
    }
}
