using System;
using System.IO;
using System.Text;

namespace QuickNote.Core
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
