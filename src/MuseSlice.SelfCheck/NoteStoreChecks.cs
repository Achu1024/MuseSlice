using System;
using System.IO;
using System.Text;
using MuseSlice.Core;

namespace MuseSlice.SelfCheck
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
            EachLineBecomesItsOwnEntry();
            MarkerOnlyLinesWriteNothing();
            BlankContentWritesNothing();
            Utf8WithoutBom();
            PathWithChineseCharacters();
            LockedFileThrows();
            CountsEntriesNotListLines();
            RewritesAtomically();
            SetDoneFlipsOnlyThatLine();
            RefusesStaleLineNumber();
            SecondDeleteKeepsFirstBackup();
            RewritesBomFileAndKeepsOtherLines();
        }

        private static void RewritesBomFileAndKeepsOtherLines()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                var path = store.PathFor(Day);

                // 使用者现有的笔记里确实有带 BOM 的（早先的编辑器留下的）。
                // 删除要能照样工作，且不能把 BOM 变成一个乱码字符混进标题行。
                File.WriteAllBytes(path, new UTF8Encoding(true).GetBytes(
                    "# 2026-10-05 星期一\n\n- 20:18 下载Minimax\n\n- 21:00 另一条\n\n"));

                Check.That(store.DeleteEntry(Day, 3, "- 20:18 下载Minimax"),
                    "带 BOM 的旧文件也能按行号删除");
                Check.Equal(store.CountEntries(Day), 1, "只删掉了那一条，另一条还在");

                var text = File.ReadAllText(path, Encoding.UTF8);
                Check.Equal(text, "# 2026-10-05 星期一\n\n- 21:00 另一条\n\n",
                    "删完之后剩下的内容与预期完全一致，标题行没被 BOM 污染");

                var bytes = File.ReadAllBytes(path);
                Check.That(!(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                    "重写后的文件不带 BOM");
            }
        }

        private static void RewritesAtomically()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条", Day);
                var path = store.PathFor(Day);
                var before = File.ReadAllText(path, Encoding.UTF8);

                Check.That(store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条"), "删掉第 3 行那条记录");
                Check.Equal(store.CountEntries(Day), 0, "删完之后条数归零");
                Check.That(!File.Exists(path + ".tmp"), "临时文件不残留");
                Check.That(File.Exists(path + ".bak"), "删除前留下 .bak");
                Check.Equal(File.ReadAllText(path + ".bak", Encoding.UTF8), before,
                    ".bak 是动手之前的样子");
                Check.That(!File.Exists(Path.Combine(dir.Path, "2026-10-05.md.bak.md")),
                    ".bak 不会被当成日记文件");
            }
        }

        private static void SetDoneFlipsOnlyThatLine()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("买牛奶\n买鸡蛋", Day);
                var path = store.PathFor(Day);

                Check.That(store.SetDone(Day, 3, "- [ ] 14:30 买牛奶", true), "把第一条标成已完成");
                Check.Equal(File.ReadAllText(path, Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [x] 14:30 买牛奶\n- [ ] 14:30 买鸡蛋\n\n",
                    "只有被勾的那一行变了");

                Check.That(!store.SetDone(Day, 3, "- [x] 14:30 买牛奶", true),
                    "已经是完成态时不再写一次");

                // 旧格式的记录被勾选时要升级成新格式，而不是保持原样。
                // Append 已经只写新格式了，所以这一份旧文件得手工造。
                var legacyDay = new DateTime(2026, 10, 6);
                File.WriteAllText(store.PathFor(legacyDay),
                    "# 2026-10-06 星期二\n\n- 09:00 旧的\n\n", new UTF8Encoding(false));

                Check.That(store.SetDone(legacyDay, 3, "- 09:00 旧的", true), "勾选旧格式的记录");
                Check.Equal(File.ReadAllText(store.PathFor(legacyDay), Encoding.UTF8),
                    "# 2026-10-06 星期二\n\n- [x] 09:00 旧的\n\n",
                    "旧格式被升级成新格式，时间戳与正文不变");
            }
        }

        private static void RefusesStaleLineNumber()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条\n第二条", Day);
                var path = store.PathFor(Day);

                store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条");
                var after = File.ReadAllText(path, Encoding.UTF8);

                // 列表生成之后文件变了，旧行号已经指向另一条记录。
                // 这时宁可不写，也不能按旧行号去删/勾一条使用者没选中的记录。
                Check.That(!store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条"),
                    "行号已失效时拒绝删除");
                Check.Equal(File.ReadAllText(path, Encoding.UTF8), after, "被拒绝时文件一字未动");

                Check.That(!store.SetDone(Day, 99, "- [ ] 14:30 第二条", true), "行号越界时拒绝");
                Check.That(!store.SetDone(Day, 3, "完全不是记录的一行", true), "行内容对不上时拒绝");
                Check.That(!store.DeleteEntry(NextDay, 3, "x"), "那天的文件不存在时返回 false 而不是抛错");
            }
        }

        private static void SecondDeleteKeepsFirstBackup()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一条\n第二条\n第三条", Day);
                var path = store.PathFor(Day);
                var before = File.ReadAllText(path, Encoding.UTF8);

                store.DeleteEntry(Day, 3, "- [ ] 14:30 第一条");
                store.DeleteEntry(Day, 3, "- [ ] 14:30 第二条");

                Check.Equal(File.ReadAllText(path + ".bak", Encoding.UTF8), before,
                    "第二次删除不覆盖 .bak——保留的是今天动手之前的样子");
            }
        }

        private static void NewFileGetsHeading()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                var wrote = store.Append("跟老王确认了接口字段", Day);
                var text = File.ReadAllText(store.PathFor(Day), Encoding.UTF8);

                Check.That(wrote, "首次写入返回 true");
                Check.Equal(text, "# 2026-10-05 星期一\n\n- [ ] 14:30 跟老王确认了接口字段\n\n",
                    "新文件含标题行、勾选框、时间戳前缀与结尾空行");
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
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 第一条\n\n- [ ] 14:32 第二条\n\n",
                    "追加不重复写标题行，两条记录各自带勾选框与时间戳");
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
                    "# 2026-10-06 星期二\n\n- [ ] 09:05 第二天的\n\n", "新一天的文件有自己的标题行");
            }
        }

        private static void EachLineBecomesItsOwnEntry()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);
                store.Append("第一行\n第二行\n第三行", Day);

                Check.Equal(File.ReadAllText(store.PathFor(Day), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 第一行\n- [ ] 14:30 第二行\n- [ ] 14:30 第三行\n\n",
                    "每一次换行各成一条待办，共用保存那一刻的时间戳");
            }
        }

        private static void MarkerOnlyLinesWriteNothing()
        {
            using (var dir = new TempDir())
            {
                var store = new NoteStore(dir.Path);

                // 打了 "- " 会自动补成脸，接着直接回车——不能因此写出一条空待办
                Check.That(!store.Append(EntryLine.UndoneMark + " ", Day), "只有勾选框没有正文返回 false");
                Check.That(!store.Append("- [ ] ", Day), "只有勾选框没有正文返回 false");
                Check.That(!Directory.Exists(dir.Path) || Directory.GetFiles(dir.Path).Length == 0,
                    "只有标记的输入不创建任何文件");

                Check.That(store.Append("有内容\n" + EntryLine.UndoneMark + " \n也有内容", Day),
                    "夹着空标记行时其余内容照常写入");

                Check.Equal(File.ReadAllText(store.PathFor(Day), Encoding.UTF8),
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 有内容\n- [ ] 14:30 也有内容\n\n",
                    "空标记行被丢弃，其余照常写入");
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
                    "# 2026-10-05 星期一\n\n- [ ] 14:30 路径含中文与空格\n\n",
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

                // 新模型下每一行都是一条记录；续行只可能来自旧文件。
                store.Append("第二条\n- [ ] 待办甲\n- [ ] 待办乙",
                    new DateTime(2026, 10, 5, 14, 35, 0));
                Check.Equal(store.CountEntries(Day), 4, "每一行各成一条记录");

                store.Append("第三条", new DateTime(2026, 10, 5, 14, 40, 0));
                Check.Equal(store.CountEntries(Day), 5, "再加一条计为 5");

                store.Append("清晨的一条", new DateTime(2026, 10, 5, 9, 5, 0));
                Check.Equal(store.CountEntries(Day), 6, "个位数小时（09:05）补零后仍能识别");

                // 使用者现有的笔记是旧格式，必须照样算数
                File.WriteAllText(Path.Combine(dir.Path, "2026-10-04.md"),
                    "# 2026-10-04 星期日\n\n- 20:18 下载Minimax\n\n- 21:00 另一条\n\n",
                    new UTF8Encoding(false));
                Check.Equal(store.CountEntries(new DateTime(2026, 10, 4)), 2,
                    "旧格式（- HH:mm）的记录一样计数");
            }
        }
    }
}
