using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace MuseSlice.Core
{
    /// <summary>
    /// 全文搜索。不建索引——日记纯文本、体积小、增长慢，三年约一千个文件、
    /// 合计几 MB，全量扫描即可，而索引会常驻内存、直接违反内存约束。
    ///
    /// 流式读取：任何时刻内存中只有一个文件的内容加结果集。
    /// 调用方必须把它放在后台线程上跑：扫一千个文件要几百毫秒，
    /// 跑在 UI 线程上会连带全局键盘钩子一起卡住全系统键盘。
    /// </summary>
    public static class NoteSearcher
    {
        public const int DefaultLimit = 200;

        public static SearchResult Search(string notesDir, string query, int limit, CancellationToken ct)
        {
            var result = new SearchResult();

            if (string.IsNullOrEmpty(query) || query.Trim().Length == 0) return result;
            if (!Directory.Exists(notesDir)) return result;

            var needle = query.Trim();
            var files = GatherFiles(notesDir);

            foreach (var entry in files)
            {
                ct.ThrowIfCancellationRequested();
                result.ScannedFiles++;

                List<string> lines;
                try
                {
                    lines = ReadAllLines(entry.Value);
                }
                catch (Exception)
                {
                    // 单个文件坏掉不该让整个搜索失败
                    result.UnreadableFiles++;
                    continue;
                }

                for (var i = 0; i < lines.Count; i++)
                {
                    if (lines[i].IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (result.Hits.Count >= limit)
                    {
                        result.Truncated = true;
                        return result;
                    }

                    result.Hits.Add(new SearchHit
                    {
                        FilePath = entry.Value,
                        Date = entry.Key,
                        LineNumber = i + 1,
                        Line = lines[i],
                        Context = ContextFor(lines, i)
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 列出最近的记录，不筛内容。搜索框为空时用它——使用者按 Tab 进搜索
        /// 多半是想先看看最近记了什么，而不是盯着一片空白。
        /// <para>
        /// 「记录」指程序生成的记录行（时间戳前缀）：标题行、正文续行都不算。
        /// 同一文件内**倒序**遍历，因为一天里最后写的那条才是最近的。
        /// </para>
        /// </summary>
        public static SearchResult ListRecent(string notesDir, int limit, CancellationToken ct)
        {
            var result = new SearchResult();
            if (limit <= 0 || !Directory.Exists(notesDir)) return result;

            foreach (var entry in GatherFiles(notesDir))
            {
                ct.ThrowIfCancellationRequested();
                result.ScannedFiles++;

                List<string> lines;
                try
                {
                    lines = ReadAllLines(entry.Value);
                }
                catch (Exception)
                {
                    result.UnreadableFiles++;
                    continue;
                }

                for (var i = lines.Count - 1; i >= 0; i--)
                {
                    if (!DateFormat.IsEntryStart(lines[i])) continue;

                    if (result.Hits.Count >= limit)
                    {
                        result.Truncated = true;
                        return result;
                    }

                    result.Hits.Add(new SearchHit
                    {
                        FilePath = entry.Value,
                        Date = entry.Key,
                        LineNumber = i + 1,
                        Line = lines[i],
                        Context = ContextFor(lines, i)
                    });
                }
            }

            return result;
        }

        /// <summary>顶层 *.md，按日期倒序。名字不合规的文件直接忽略。</summary>
        private static List<KeyValuePair<DateTime, string>> GatherFiles(string notesDir)
        {
            var files = new List<KeyValuePair<DateTime, string>>();
            foreach (var path in Directory.GetFiles(notesDir, "*.md"))
            {
                DateTime date;
                if (TryParseDate(Path.GetFileNameWithoutExtension(path), out date))
                {
                    files.Add(new KeyValuePair<DateTime, string>(date, path));
                }
            }
            files.Sort((a, b) => b.Key.CompareTo(a.Key));
            return files;
        }

        /// <summary>
        /// 取匹配行的续行作为上下文。若下一非空行本身是一条新记录，说明这条记录
        /// 只有一行，就没有上下文可给。
        /// <para>
        /// 判断「是不是新记录」必须用 <see cref="DateFormat.IsEntryStart"/> 而不是
        /// 简单的 "- " 前缀：多行记录的续行是使用者原样写入的，他自己写一个顶层
        /// 列表项时那一行也以 "- " 开头，会被误判成下一条记录而丢掉上下文。
        /// </para>
        /// </summary>
        private static string ContextFor(List<string> lines, int index)
        {
            for (var j = index + 1; j < lines.Count; j++)
            {
                var candidate = lines[j];
                if (candidate.Trim().Length == 0) continue;
                if (DateFormat.IsEntryStart(candidate)) return "";
                return candidate;
            }
            return "";
        }

        private static List<string> ReadAllLines(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        private static bool TryParseDate(string name, out DateTime date)
        {
            return DateTime.TryParseExact(name, DateFormat.FileNamePattern,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}
