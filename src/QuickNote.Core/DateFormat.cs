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

        /// <summary>记录行的前缀，例如「- 14:03 」。写入与识别共用，避免两边格式漂移。</summary>
        public static string EntryPrefix(DateTime when)
        {
            return "- " + Stamp(when) + " ";
        }

        /// <summary>
        /// 判断一行是否是程序生成的记录起始行。
        /// <para>
        /// 不能只看 "- "：多行记录的续行是使用者原样写入的，如果他自己写了一个
        /// 顶层列表项，那一行也会以 "- " 开头。唯一可靠的标志是程序生成的
        /// 时间戳前缀 —— 短横、空格、两位小时、冒号、两位分钟、空格。
        /// </para>
        /// </summary>
        public static bool IsEntryStart(string line)
        {
            if (line == null || line.Length < 8) return false;
            if (line[0] != '-' || line[1] != ' ') return false;
            if (!IsDigit(line[2]) || !IsDigit(line[3])) return false;
            if (line[4] != ':') return false;
            if (!IsDigit(line[5]) || !IsDigit(line[6])) return false;
            return line[7] == ' ';
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
