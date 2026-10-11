using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MuseSlice.Core
{
    /// <summary>
    /// 按天把内容追加进 markdown 文件。整个过程是纯追加，写入前不读取文件内容。
    /// </summary>
    public sealed class NoteStore
    {
        public NoteStore(string notesDir)
        {
            if (string.IsNullOrWhiteSpace(notesDir))
                throw new ArgumentException("笔记目录不能为空", "notesDir");
            NotesDir = notesDir;
        }

        public string NotesDir { get; private set; }

        public string PathFor(DateTime when)
        {
            return Path.Combine(NotesDir, DateFormat.FileName(when) + ".md");
        }

        /// <summary>
        /// 追加一笔。<paramref name="content"/> 里的**每一行各成一条待办**（定案 §10.1）：
        /// 使用者的真实用法是「一口气列几件要做的事」，不是写一段带结构的长文。
        /// 每行开头若已有勾选标记（界面上画的脸，或他手打的 - [ ]），沿用它的状态。
        /// 全部行都是空白（连标记都没有正文）时什么都不做，返回 false。
        /// </summary>
        public bool Append(string content, DateTime now)
        {
            if (content == null) return false;

            var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.Trim().Length == 0) return false;

            var lines = normalized.TrimEnd('\n').Split('\n');

            var entries = new StringBuilder();
            foreach (var raw in lines)
            {
                bool done;
                var body = EntryLine.StripMarker(raw, out done);
                if (body.Length == 0) continue;      // 空行、或只有标记没有正文
                entries.Append(EntryLine.Prefix(now, done)).Append(body).Append('\n');
            }
            if (entries.Length == 0) return false;   // 别创建空文件

            var sb = new StringBuilder();
            var path = PathFor(now);
            if (!File.Exists(path))
            {
                sb.Append(DateFormat.Heading(now)).Append('\n').Append('\n');
            }
            sb.Append(entries).Append('\n');

            try
            {
                Directory.CreateDirectory(NotesDir);
                // FileShare.ReadWrite：使用者正用 Typora 打开当天文件时仍能写入。
                using (var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
                {
                    w.Write(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("写入 " + path + " 失败：" + ex.Message, ex);
            }

            return true;
        }

        /// <summary>
        /// 把某一天的某一行的勾选状态改成 done。行内容与 <paramref name="expectedLine"/>
        /// 不一致时拒绝写入——那是「列表生成之后文件被外部改过」的信号，
        /// 按旧行号动手会改错一条。
        /// </summary>
        public bool SetDone(DateTime day, int lineNumber, string expectedLine, bool done)
        {
            return Rewrite(day, lineNumber, expectedLine, delegate(List<string> lines, int index)
            {
                var updated = EntryLine.SetDone(lines[index], done);
                if (updated == null || updated == lines[index]) return false;   // 已经是这个状态
                lines[index] = updated;
                return true;
            });
        }

        /// <summary>
        /// 删掉某一天的第 lineNumber 行那条记录。旧格式的续行跟着一起删，
        /// 不留孤儿行（定案 §10.2）。
        /// </summary>
        public bool DeleteEntry(DateTime day, int lineNumber, string expectedLine)
        {
            return Rewrite(day, lineNumber, expectedLine, delegate(List<string> lines, int index)
            {
                lines.RemoveRange(index, EntryLine.DeleteCount(lines, index));
                return true;
            });
        }

        /// <summary>
        /// 删除与勾选共用的重写路径。返回 false 表示「什么都没做」——
        /// 文件不在、行号越界、行内容对不上、或改完没有变化。
        /// </summary>
        private bool Rewrite(DateTime day, int lineNumber, string expectedLine,
                             Func<List<string>, int, bool> mutate)
        {
            var path = PathFor(day);
            if (!File.Exists(path)) return false;

            List<string> lines;
            try
            {
                lines = ReadLines(path);
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("读取 " + path + " 失败：" + ex.Message, ex);
            }

            var index = lineNumber - 1;
            if (index < 0 || index >= lines.Count) return false;
            if (lines[index] != expectedLine) return false;
            if (!mutate(lines, index)) return false;

            BackupOnce(path);
            WriteAtomic(path, lines);
            return true;
        }

        private static List<string> ReadLines(string path)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var r = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = r.ReadLine()) != null) lines.Add(line);
            }
            return lines;
        }

        /// <summary>
        /// 当天首次改写之前留一份原文，之后不再覆盖。
        /// 保留的是「今天动手之前」的样子，那才是使用者真正想找回来的东西；
        /// 每次改写都覆盖的话，连删几条之后就只剩最后一次之前的状态了。
        /// </summary>
        private static void BackupOnce(string path)
        {
            var bak = path + ".bak";
            if (File.Exists(bak)) return;
            try
            {
                File.Copy(path, bak);
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("备份 " + path + " 失败：" + ex.Message, ex);
            }
        }

        /// <summary>
        /// 写临时文件再原子替换。追加写入靠「不碰前面的内容」规避了写坏一半的风险，
        /// 重写就没有这层保护了——中途失败会留下半个文件，所以必须走
        /// File.Replace（NTFS 上要么旧文件、要么新文件）。
        /// <para>
        /// File.Replace 需要目标可写：Typora 之类以独占方式打开着当天文件时会失败。
        /// 那时原文件一字未动，抛出去让界面提示即可。
        /// </para>
        /// </summary>
        private static void WriteAtomic(string path, List<string> lines)
        {
            var tmp = path + ".tmp";
            var sb = new StringBuilder();
            foreach (var line in lines) sb.Append(line).Append('\n');

            try
            {
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                File.Replace(tmp, path, null);
            }
            catch (Exception ex)
            {
                TryDelete(tmp);
                throw new NoteStoreException("重写 " + path + " 失败：" + ex.Message, ex);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* 清不掉临时文件不该盖住真正的错误 */ }
        }

        /// <summary>今天已有的记录条数，用于小条顶部的「今天已记 N 条」。</summary>
        public int CountEntries(DateTime day)
        {
            var path = PathFor(day);
            if (!File.Exists(path)) return 0;

            var count = 0;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var r = new StreamReader(fs, Encoding.UTF8))
                {
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        if (DateFormat.IsEntryStart(line)) count++;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new NoteStoreException("读取 " + path + " 失败：" + ex.Message, ex);
            }
            return count;
        }
    }
}
