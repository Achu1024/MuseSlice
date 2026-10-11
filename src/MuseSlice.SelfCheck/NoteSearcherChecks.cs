using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MuseSlice.Core;

namespace MuseSlice.SelfCheck
{
    internal static class NoteSearcherChecks
    {
        public static void Run()
        {
            Check.Section("NoteSearcher");
            EmptyQueryReturnsNothing();
            CaseInsensitiveMatch();
            ChineseSubstringMatch();
            NewestFirst();
            ContextSkipsBlankAndNextEntry();
            ContextIsUserListLineNotNextEntry();
            TruncatesAtLimit();
            IgnoresNonMarkdownAndSubdirectories();
            UnreadableFileIsSkipped();
            CancellationStopsEarly();
            ListRecentReturnsEntriesNewestFirst();
            ListRecentHonoursLimit();
            NewFormatEntryHasNoContext();
        }

        private static void NewFormatEntryHasNoContext()
        {
            using (var dir = new TempDir())
            {
                File.WriteAllText(Path.Combine(dir.Path, "2026-10-05.md"),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 跟老王确认了字段\n- [x] 14:32 把 spec 写完\n\n",
                    new UTF8Encoding(false));

                var result = NoteSearcher.Search(dir.Path, "老王", 200, CancellationToken.None);
                Check.Equal(result.Hits.Count, 1, "新格式的记录能被搜到");
                Check.Equal(result.Hits[0].Context, "",
                    "新格式的下一条记录不会被当成上一条的上下文");

                var recent = NoteSearcher.ListRecent(dir.Path, 200, CancellationToken.None);
                Check.Equal(recent.Hits.Count, 2, "新格式的记录会被 ListRecent 列出来");
            }
        }

        private static void Write(string dir, string name, params string[] lines)
        {
            File.WriteAllText(Path.Combine(dir, name), string.Join("\n", lines) + "\n",
                new UTF8Encoding(false));
        }

        private static void EmptyQueryReturnsNothing()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "# 2026-10-05 星期一", "", "- 14:30 老王");

                var a = NoteSearcher.Search(dir.Path, "", NoteSearcher.DefaultLimit, CancellationToken.None);
                var b = NoteSearcher.Search(dir.Path, "   ", NoteSearcher.DefaultLimit, CancellationToken.None);
                var c = NoteSearcher.Search(dir.Path, null, NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(a.Hits.Count, 0, "空查询不返回结果（而不是匹配所有行）");
                Check.Equal(a.ScannedFiles, 0, "空查询不扫描任何文件");
                Check.Equal(b.Hits.Count, 0, "纯空白查询不返回结果");
                Check.Equal(c.Hits.Count, 0, "null 查询不返回结果");
            }
        }

        private static void CaseInsensitiveMatch()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "- 14:30 字段名改成 Snake_Case 了");
                var r = NoteSearcher.Search(dir.Path, "snake", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "小写查询命中大写内容");
                Check.Equal(r.Hits[0].Line, "- 14:30 字段名改成 Snake_Case 了", "命中行内容正确");
            }
        }

        private static void ChineseSubstringMatch()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "- 14:30 跟老王确认了接口字段");
                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "中文子串命中");
            }
        }

        private static void NewestFirst()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-08-01.md", "- 09:00 老王今天来");
                Write(dir.Path, "2026-10-05.md", "- 09:00 老王今天来");
                Write(dir.Path, "2026-09-10.md", "- 09:00 老王今天来");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 3, "三个文件都命中");
                Check.Equal(r.Hits[0].Date, new DateTime(2026, 10, 5), "最近的在最前");
                Check.Equal(r.Hits[1].Date, new DateTime(2026, 9, 10), "其次");
                Check.Equal(r.Hits[2].Date, new DateTime(2026, 8, 1), "最旧的在最后");
            }
        }

        private static void ContextSkipsBlankAndNextEntry()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md",
                    "# 2026-10-05 星期一",
                    "",
                    "- 14:30 跟老王确认了接口字段",
                    "他那边说字段名要改成 snake_case",
                    "",
                    "- 14:32 老王说要推迟",
                    "",
                    "- 14:40 老王说结尾这条没有下文");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 3, "命中三条");
                Check.Equal(r.Hits[0].Context, "他那边说字段名要改成 snake_case",
                    "多行记录：上下文取紧随的续行");
                Check.Equal(r.Hits[1].Context, "",
                    "单行记录：下一非空行是另一条记录的时间戳，不留上下文");
                Check.Equal(r.Hits[2].Context, "", "文件末尾的记录不越界");
            }
        }

        /// <summary>
        /// 续行是使用者原样写入的，他可能写一个顶层列表项，那一行也以 "- " 开头。
        /// 光看 "- " 会把它误判成「下一条记录」而丢掉上下文；唯一可靠的标志是
        /// 程序生成的时间戳前缀。
        /// </summary>
        private static void ContextIsUserListLineNotNextEntry()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md",
                    "- 14:30 老王这条后面跟的是使用者自己的列表",
                    "- [ ] 待办甲",
                    "- [ ] 待办乙");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "只命中一条");
                Check.Equal(r.Hits[0].Context, "- [ ] 待办甲",
                    "续行是使用者的顶层列表项，应作为上下文，而不是被当成新记录");
            }
        }

        private static void TruncatesAtLimit()
        {
            using (var dir = new TempDir())
            {
                var lines = new List<string>();
                for (var i = 0; i < 20; i++) lines.Add("- 14:30 老王第 " + i + " 次");
                Write(dir.Path, "2026-10-05.md", lines.ToArray());

                var r = NoteSearcher.Search(dir.Path, "老王", 5, CancellationToken.None);

                Check.Equal(r.Hits.Count, 5, "结果被截断到 limit");
                Check.That(r.Truncated, "标记为已截断");
            }
        }

        private static void IgnoresNonMarkdownAndSubdirectories()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-05.md", "- 14:30 老王在笔记里");
                Write(dir.Path, "2026-10-05.txt", "- 14:30 老王在文本文件里");
                Write(dir.Path, "随便一个名字.md", "- 14:30 老王在取名不规范的文件里");

                var sub = Path.Combine(dir.Path, "子目录");
                Directory.CreateDirectory(sub);
                Write(sub, "2026-10-04.md", "- 14:30 老王在子目录里");

                var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 1, "只扫顶层、只认 yyyy-MM-dd.md");
            }
        }

        private static void UnreadableFileIsSkipped()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-10-04.md", "- 09:00 老王在好文件里");
                Write(dir.Path, "2026-10-05.md", "- 14:30 老王在被锁文件里");

                var locked = Path.Combine(dir.Path, "2026-10-05.md");
                using (var _ = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    var r = NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, CancellationToken.None);

                    Check.Equal(r.Hits.Count, 1, "锁住的文件被跳过，其余文件仍正常出结果");
                    Check.Equal(r.UnreadableFiles, 1, "坏文件被计数");
                }
            }
        }

        private static void CancellationStopsEarly()
        {
            using (var dir = new TempDir())
            {
                for (var d = 1; d <= 30; d++)
                {
                    Write(dir.Path, string.Format("2026-09-{0:D2}.md", d), "- 09:00 老王");
                }

                var cts = new CancellationTokenSource();
                cts.Cancel();

                var threw = false;
                try { NoteSearcher.Search(dir.Path, "老王", NoteSearcher.DefaultLimit, cts.Token); }
                catch (OperationCanceledException) { threw = true; }

                Check.That(threw, "已取消的令牌立刻抛出 OperationCanceledException");
            }
        }

        /// <summary>
        /// 搜索框为空时列出全部记录。注意「记录」指的是程序生成的记录行
        /// （时间戳前缀），不是文件里的每一行——标题行和续行都不算。
        /// </summary>
        private static void ListRecentReturnsEntriesNewestFirst()
        {
            using (var dir = new TempDir())
            {
                Write(dir.Path, "2026-09-01.md",
                    "# 2026-09-01 星期二",
                    "",
                    "- 09:00 第一条",
                    "这条是续行，不算独立记录",
                    "",
                    "- 14:32 第二条");

                Write(dir.Path, "2026-10-05.md",
                    "# 2026-10-05 星期一",
                    "",
                    "- 08:15 最新的一条");

                var r = NoteSearcher.ListRecent(dir.Path, NoteSearcher.DefaultLimit, CancellationToken.None);

                Check.Equal(r.Hits.Count, 3, "只数列记录行：标题行与续行都不算");
                Check.Equal(r.Hits[0].Line, "- 08:15 最新的一条", "最近的在最前");
                Check.Equal(r.Hits[1].Line, "- 14:32 第二条", "同一天内按文件内顺序");
                Check.Equal(r.Hits[2].Line, "- 09:00 第一条", "最旧的在最后");
                Check.Equal(r.Hits[2].Context, "这条是续行，不算独立记录",
                    "多行记录的续行作为上下文带出来");
                Check.Equal(r.ScannedFiles, 2, "两个文件都扫了");

                var none = NoteSearcher.ListRecent(Path.Combine(dir.Path, "不存在"),
                    NoteSearcher.DefaultLimit, CancellationToken.None);
                Check.Equal(none.Hits.Count, 0, "目录不存在时返回空而不是抛异常");
            }
        }

        private static void ListRecentHonoursLimit()
        {
            using (var dir = new TempDir())
            {
                var lines = new List<string>();
                for (var i = 1; i <= 20; i++) lines.Add("- 14:30 第 " + i + " 条");
                Write(dir.Path, "2026-10-05.md", lines.ToArray());

                var r = NoteSearcher.ListRecent(dir.Path, 5, CancellationToken.None);

                Check.Equal(r.Hits.Count, 5, "最多返回 limit 条");
                Check.That(r.Truncated, "被截断时标记出来，免得使用者以为只有这些");
            }
        }
    }
}
