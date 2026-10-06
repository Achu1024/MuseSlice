using System;
using System.Collections.Generic;

namespace QuickNote.Core
{
    /// <summary>一条记录行的内容。</summary>
    public sealed class EntryInfo
    {
        public bool IsDone { get; set; }
        public string Stamp { get; set; }
        public string Content { get; set; }

        /// <summary>界面上代表这个状态的勾选框。文件里存的是 "- [ ]" / "- [x]"。</summary>
        public string Mark { get { return IsDone ? EntryLine.DoneMark : EntryLine.UndoneMark; } }
    }

    /// <summary>
    /// 记录行的解析与生成。
    /// <para>
    /// 写入格式（2026-10-05 定案 §6）：<c>- [ ] HH:mm 内容</c>，已完成的写 <c>- [x]</c>。
    /// 读取必须同时认旧格式 <c>- HH:mm 内容</c>——使用者已有的记录全是旧格式，
    /// 只认新格式会让条数归零、上下文错乱、搜索列不出来。
    /// </para>
    /// </summary>
    public static class EntryLine
    {
        /// <summary>
        /// 填写页的未勾选框；文件里存 "- [ ]"。
        /// <para>
        /// 用方块字形而不是 emoji：原先那两个脸（😶‍🌫️ / 🥳）在本机渲染成了黑白，
        /// 而且 😶‍🌫️ 是 ZWJ 序列（U+1F636 + ZWJ + U+1F32B + VS16），比单个 emoji
        /// 更容易退化成两截。U+2610 / U+2611 是普通 BMP 字形，Noto Sans SC 自带
        /// （已量过宽度），不走字体回退，天生就是单色——正好合「界面近乎无彩」。
        /// </para>
        /// </summary>
        public const string UndoneMark = "☐";

        /// <summary>填写页的已勾选框（U+2611）；文件里存 "- [x]"。</summary>
        public const string DoneMark = "☑";

        public const string UndoneBox = "- [ ] ";
        public const string DoneBox = "- [x] ";

        /// <summary>新建一条记录的行首前缀，例如「- [ ] 14:03 」。</summary>
        public static string Prefix(DateTime when, bool done)
        {
            return (done ? DoneBox : UndoneBox) + DateFormat.Stamp(when) + " ";
        }

        /// <summary>
        /// 解析一行记录。使用者手打的 "- [ ] 待办" 与真正的记录行的区别在于时间戳：
        /// 后者有「- HH:mm 」或「- [ ] HH:mm 」，前者没有，因此不会被误认成记录。
        /// </summary>
        public static bool TryParse(string line, out EntryInfo info)
        {
            info = null;
            if (line == null) return false;

            int at;
            var done = false;

            if (line.Length >= 6 && line[0] == '-' && line[1] == ' ' && line[2] == '[' &&
                line[4] == ']' && line[5] == ' ' &&
                (line[3] == ' ' || line[3] == 'x' || line[3] == 'X'))
            {
                done = line[3] != ' ';
                at = 6;
            }
            else if (line.Length >= 2 && line[0] == '-' && line[1] == ' ')
            {
                at = 2;
            }
            else return false;

            if (line.Length < at + 6) return false;
            if (!IsDigit(line[at]) || !IsDigit(line[at + 1])) return false;
            if (line[at + 2] != ':') return false;
            if (!IsDigit(line[at + 3]) || !IsDigit(line[at + 4])) return false;
            if (line[at + 5] != ' ') return false;

            info = new EntryInfo
            {
                IsDone = done,
                Stamp = line.Substring(at, 5),
                Content = line.Substring(at + 6)
            };
            return true;
        }

        public static bool IsEntryStart(string line)
        {
            EntryInfo ignored;
            return TryParse(line, out ignored);
        }

        /// <summary>把一条记录行改写成指定完成态，时间戳与正文原样保留。不是记录行则返回 null。</summary>
        public static string SetDone(string line, bool done)
        {
            EntryInfo info;
            if (!TryParse(line, out info)) return null;
            return (done ? DoneBox : UndoneBox) + info.Stamp + " " + info.Content;
        }

        /// <summary>
        /// 去掉填写页那一行开头的勾选标记（界面上画的脸，或使用者手打的 - [ ] / - [x]），
        /// 返回正文并给出完成态。没有标记时原样返回、视为未完成。
        /// 结果已 Trim——保存时每一行都要变成一条干净的待办正文。
        /// </summary>
        public static string StripMarker(string line, out bool done)
        {
            done = false;
            if (line == null) return "";

            if (StartsWith(line, DoneBox)) { done = true; return line.Substring(DoneBox.Length).Trim(); }
            if (StartsWith(line, UndoneBox)) return line.Substring(UndoneBox.Length).Trim();
            if (StartsWith(line, DoneMark)) { done = true; return line.Substring(DoneMark.Length).Trim(); }
            if (StartsWith(line, UndoneMark)) return line.Substring(UndoneMark.Length).Trim();

            return line.Trim();
        }

        /// <summary>
        /// 删除第 index 行时要一并删掉多少行：这条记录本身 + 它的续行 + 紧随的一个空行。
        /// <para>
        /// 续行只可能来自旧格式——新格式每一行都是独立记录。遇到空行或下一条记录就停，
        /// 否则会把下一条记录一起吃掉。
        /// </para>
        /// </summary>
        public static int DeleteCount(IList<string> lines, int index)
        {
            var end = index + 1;
            while (end < lines.Count)
            {
                if (lines[end].Trim().Length == 0) break;
                if (IsEntryStart(lines[end])) break;
                end++;
            }
            if (end < lines.Count && lines[end].Trim().Length == 0) end++;
            return end - index;
        }

        private static bool StartsWith(string line, string prefix)
        {
            return line.Length >= prefix.Length &&
                   string.CompareOrdinal(line, 0, prefix, 0, prefix.Length) == 0;
        }

        private static bool IsDigit(char c) { return c >= '0' && c <= '9'; }
    }
}
