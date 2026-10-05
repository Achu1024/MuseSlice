using System;
using System.IO;
using System.Text;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class NoteStoreChecks
    {
        private static readonly DateTime Day = new DateTime(2026, 10, 5, 14, 30, 0);
        private static readonly DateTime NextDay = new DateTime(2026, 10, 6, 9, 5, 0);

        public static void Run()
        {
            Check.Section("NoteStore");

            NewFileGetsHeading();
            AppendsToExistingFile();
            RollsOverAtMidnight();
            MultiLineOnlyFirstLineGetsStamp();
            BlankContentWritesNothing();
            Utf8WithoutBom();
            PathWithChineseCharacters();
            LockedFileThrows();
            CountsEntriesNotListLines();
        }

        private static void NewFileGetsHeading()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                var wrote = store.Append("跟老王确认了接口字段", Day);
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.That(wrote, "首次写入返回 true");
                Check.Equal(text, "# 2026-10-05 星期一\n\n- 14:30 跟老王确认了接口字段\n\n",
                    "新文件含标题行、时间戳前缀与结尾空行");
            }
        }

        private static void AppendsToExistingFile()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条", Day);
                store.Append("第二条", new DateTime(2026, 10, 5, 14, 32, 0));
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.Equal(text,
                    "# 2026-10-05 星期一\n\n- 14:30 第一条\n\n- 14:32 第二条\n\n",
                    "追加不重复写标题行，两条记录各自带时间戳");
            }
        }

        private static void RollsOverAtMidnight()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("今天的", Day);
                store.Append("第二天的", NextDay);

                Check.That(File.Exists(store.PathFor(Day)), "前一天文件仍存在");
                Check.Equal(store.PathFor(NextDay),
                    Path.Combine(dir.Path, "2026-10-06.md"), "跨天写入新文件");
                Check.Equal(File.ReadAllText(store.PathFor(NextDay), Encoding.UTF8),
                    "# 2026-10-06 星期二\n\n- 09:05 第二天的\n\n", "新一天的文件有自己的标题行");
            }
        }

        private static void MultiLineOnlyFirstLineGetsStamp()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一行\n## 下午计划\n- [ ] 把 spec 写完", Day);
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.Equal(text,
                    "# 2026-10-05 星期一\n\n- 14:30 第一行\n## 下午计划\n- [ ] 把 spec 写完\n\n",
                    "多行内容仅首行带时间戳，后续行原样不缩进");
            }
        }

        private static void BlankContentWritesNothing()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);

                Check.That(!store.Append("", Day), "空字符串返回 false");
                Check.That(!store.Append("   ", Day), "纯空格返回 false");
                Check.That(!store.Append("\n\n", Day), "纯换行返回 false");
                Check.That(!store.Append(" \t \r\n  ", Day), "空格制表符回车混合返回 false");
                Check.That(!store.Append(null, Day), "null 返回 false");

                Check.That(!Directory.Exists(dir.Path) || Directory.GetFiles(dir.Path).Length == 0,
                    "空白输入不创建任何文件");
            }
        }

        private static void Utf8WithoutBom()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("中文内容", Day);
                var bytes = File.ReadAllBytes(store.PathFor(Day));

                Check.That(bytes.Length >= 3 &&
                           !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                    "文件不以 UTF-8 BOM 开头");
            }
        }

        private static void PathWithChineseCharacters()
        {
            using (var dir = new TempDir())
            {
                var cn = Path.Combine(dir.Path, "我的 笔记");
                var store = new NoteStore(cn);
                store.Append("路径含中文与空格", Day);

                Check.That(File.Exists(Path.Combine(cn, "2026-10-05.md")),
                    "目录名含中文与空格时正常创建并写入");
                Check.Equal(File.ReadAllText(Path.Combine(cn, "2026-10-05.md"), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- 14:30 路径含中文与空格\n\n",
                    "中文路径下内容正确");
            }
        }

        private static void LockedFileThrows()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("先写一条", Day);

                using (var _ = new FileStream(store.PathFor(Day), FileMode.Open,
                                              FileAccess.Read, FileShare.None))
                {
                    var threw = false;
                    try { store.Append("被锁住时写入", Day); }
                    catch (NoteStoreException) { threw = true; }

                    Check.That(threw, "文件被独占锁定时抛 NoteStoreException 而不是静默失败");
                }
            }
        }

        private static void CountsEntriesNotListLines()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);

                Check.Equal(store.CountEntries(Day), 0, "文件还不存在时条数为 0");

                store.Append("第一条", Day);
                Check.Equal(store.CountEntries(Day), 1, "一条单行记录计为 1");

                // 多行记录：续行是使用者自己写的顶层列表，不能被当成新记录。
                // 程序生成的时间戳前缀是唯一可靠的记录标志，光看 "- " 会把它们算进去。
                store.Append("第二条\n- [ ] 待办甲\n- [ ] 待办乙",
                    new DateTime(2026, 10, 5, 14, 35, 0));
                Check.Equal(store.CountEntries(Day), 2, "多行记录里的列表项不算作新记录");

                store.Append("第三条", new DateTime(2026, 10, 5, 14, 40, 0));
                Check.Equal(store.CountEntries(Day), 3, "再加一条计为 3");

                store.Append("清晨的一条", new DateTime(2026, 10, 5, 9, 5, 0));
                Check.Equal(store.CountEntries(Day), 4, "个位数小时（09:05）补零后仍能识别");
            }
        }
    }
}
