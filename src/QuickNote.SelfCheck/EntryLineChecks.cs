using System;
using System.Collections.Generic;
using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class EntryLineChecks
    {
        private static readonly DateTime When = new DateTime(2026, 10, 5, 14, 3, 0);

        public static void Run()
        {
            Check.Section("EntryLine");
            NewFormatRoundTrip();
            ReadsLegacyFormat();
            RejectsUserWrittenLists();
            StripsMarkers();
            TogglesDone();
            DeleteRange();
        }

        private static void NewFormatRoundTrip()
        {
            Check.Equal(EntryLine.Prefix(When, false), "- [ ] 14:03 ", "新格式前缀：未完成");
            Check.Equal(EntryLine.Prefix(When, true), "- [x] 14:03 ", "新格式前缀：已完成");

            EntryInfo info;
            Check.That(EntryLine.TryParse("- [ ] 14:03 跟老王确认了接口字段", out info), "识别新格式的未完成行");
            Check.Equal(info.IsDone, false, "未完成行的完成态为 false");
            Check.Equal(info.Stamp, "14:03", "取到时间戳");
            Check.Equal(info.Content, "跟老王确认了接口字段", "取到正文");
            Check.Equal(info.Face, EntryLine.UndoneFace, "未完成行给未完成的脸");

            Check.That(EntryLine.TryParse("- [x] 14:03 把 spec 写完", out info), "识别新格式的已完成行");
            Check.Equal(info.IsDone, true, "已完成行的完成态为 true");
            Check.Equal(info.Face, EntryLine.DoneFace, "已完成行给完成的脸");

            Check.That(EntryLine.TryParse("- [X] 14:03 大写 X", out info), "大写 X 也认");
            Check.Equal(info.IsDone, true, "大写 X 视为已完成");
        }

        private static void ReadsLegacyFormat()
        {
            // 使用者现存的文件全是这个格式。认不出来就等于所有旧记录消失。
            EntryInfo info;
            Check.That(EntryLine.TryParse("- 20:18 下载Minimax", out info), "旧格式（无勾选框）仍被识别为记录");
            Check.Equal(info.IsDone, false, "旧格式一律视为未完成");
            Check.Equal(info.Stamp, "20:18", "旧格式也能取到时间戳");
            Check.Equal(info.Content, "下载Minimax", "旧格式也能取到正文");
            Check.Equal(info.Face, EntryLine.UndoneFace, "旧格式的记录显示未完成的脸");

            Check.That(DateFormat.IsEntryStart("- 20:18 下载Minimax"), "DateFormat.IsEntryStart 仍认旧格式");
            Check.That(DateFormat.IsEntryStart("- [ ] 14:03 新格式"), "DateFormat.IsEntryStart 也认新格式");
            Check.That(DateFormat.IsEntryStart("- [x] 14:03 新格式"), "DateFormat.IsEntryStart 认已完成的新格式");
        }

        private static void RejectsUserWrittenLists()
        {
            EntryInfo info;
            Check.That(!EntryLine.TryParse("- [ ] 待办甲", out info), "使用者手打的待办列表项不是记录行（没有时间戳）");
            Check.That(!EntryLine.TryParse("- 一条普通列表", out info), "普通列表项不是记录行");
            Check.That(!EntryLine.TryParse("- 9:05 小时没补零", out info), "小时没补零不算记录行");
            Check.That(!EntryLine.TryParse("- [ ] 9:05 新格式也要两位小时", out info), "新格式同样要求两位小时");
            Check.That(!EntryLine.TryParse("- [ ]14:03 方括号后没有空格", out info), "方括号后缺空格不算记录行");
            Check.That(!EntryLine.TryParse("- 20:18", out info), "只有时间戳没有尾随空格不算");
            Check.That(!EntryLine.TryParse("", out info), "空行不是记录行");
            Check.That(!EntryLine.TryParse(null, out info), "null 不是记录行");
        }

        private static void StripsMarkers()
        {
            bool done;
            Check.Equal(EntryLine.StripMarker(EntryLine.UndoneFace + " 买牛奶", out done), "买牛奶",
                "界面的未完成脸被去掉");
            Check.Equal(done, false, "未完成脸给出未完成态");

            Check.Equal(EntryLine.StripMarker(EntryLine.DoneFace + " 买牛奶", out done), "买牛奶",
                "界面的完成脸被去掉");
            Check.Equal(done, true, "完成脸给出完成态");

            Check.Equal(EntryLine.StripMarker("- [ ] 买牛奶", out done), "买牛奶", "手打的 - [ ] 被去掉");
            Check.Equal(done, false, "手打的 - [ ] 视为未完成");
            Check.Equal(EntryLine.StripMarker("- [x] 买牛奶", out done), "买牛奶", "手打的 - [x] 被去掉");
            Check.Equal(done, true, "手打的 - [x] 视为已完成");

            Check.Equal(EntryLine.StripMarker("买牛奶", out done), "买牛奶", "没有标记的普通行原样返回");
            Check.Equal(done, false, "没有标记视为未完成");
            Check.Equal(EntryLine.StripMarker(EntryLine.UndoneFace, out done), "",
                "只有脸没有正文时返回空串（保存时会被跳过）");
            Check.Equal(EntryLine.StripMarker("- 5 分钟后开会", out done), "- 5 分钟后开会",
                "行首不是勾选标记的短横不被吃掉");
        }

        private static void TogglesDone()
        {
            Check.Equal(EntryLine.SetDone("- [ ] 14:03 买牛奶", true), "- [x] 14:03 买牛奶", "未完成切到已完成");
            Check.Equal(EntryLine.SetDone("- [x] 14:03 买牛奶", false), "- [ ] 14:03 买牛奶", "已完成切回未完成");
            Check.Equal(EntryLine.SetDone("- 20:18 下载Minimax", true), "- [x] 20:18 下载Minimax",
                "勾选旧格式的记录会把它升级成新格式，时间戳与正文不变");
            Check.Equal(EntryLine.SetDone("- [ ] 14:03 ", true), "- [x] 14:03 ", "正文为空的记录也能切换");
            Check.Equal(EntryLine.SetDone("普通人", true), null, "不是记录行时返回 null");
            Check.Equal(EntryLine.SetDone("- [ ] 待办甲", true), null, "使用者手打的列表项不能被当成记录行改写");
        }

        private static void DeleteRange()
        {
            var lines = new List<string>
            {
                "# 2026-10-05 星期一",              // 0
                "",                                   // 1
                "- [ ] 14:30 第一条",                 // 2
                "- [ ] 14:32 第二条",                 // 3
                "",                                   // 4
                "- 20:18 旧的单行",                   // 5
                "",                                   // 6
                "- 21:00 旧的带续行",                 // 7
                "他那边说字段名要改成 snake_case",    // 8  旧格式的续行
                "",                                   // 9
                "- [ ] 21:05 第三条",                 // 10
                ""                                    // 11
            };

            Check.Equal(EntryLine.DeleteCount(lines, 2), 1, "下一条记录紧随其后时不连坐");
            Check.Equal(EntryLine.DeleteCount(lines, 3), 2, "连着自己的分隔空行一起删");
            Check.Equal(EntryLine.DeleteCount(lines, 5), 2, "旧格式的单行记录");
            Check.Equal(EntryLine.DeleteCount(lines, 7), 3, "旧格式的续行跟着一起删，但停在空行");
            Check.Equal(EntryLine.DeleteCount(lines, 10), 2, "删最后一条记录");

            var noTrailingBlank = new List<string> { "- [ ] 14:30 只有一条" };
            Check.Equal(EntryLine.DeleteCount(noTrailingBlank, 0), 1, "文件末尾没有空行时不越界");

            var glued = new List<string> { "- [ ] 14:30 第一条", "- [ ] 14:31 第二条" };
            Check.Equal(EntryLine.DeleteCount(glued, 0), 1, "两条记录紧挨着时只删第一条");

            // 已知的有意取舍：空行是唯一的「这条记录到此为止」标志。手工编辑时把标题行
            // 直接贴在记录下面（中间没空行），它会被当成续行一起删。程序自己写的文件
            // 不会长这样——每条记录后面必定跟着一个空行；而旧格式的续行本来就可能是
            // 一个 "## 标题"，不认它会留下孤儿行。
            var handEdited = new List<string> { "- [ ] 14:30 最后一条", "## 下午计划" };
            Check.Equal(EntryLine.DeleteCount(handEdited, 0), 2,
                "手改过的文件里紧贴着记录的标题行会被当成续行（空行才是边界）");
        }
    }
}
