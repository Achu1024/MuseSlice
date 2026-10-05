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
        /// 追加一笔。内容全为空白时什么都不做（连文件都不创建），返回 false。
        /// </summary>
        public bool Append(string content, DateTime now)
        {
            if (content == null) return false;

            var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            if (normalized.Trim().Length == 0) return false;

            var text = normalized.TrimEnd('\n');
            var lines = text.Split('\n');

            var sb = new StringBuilder();
            var path = PathFor(now);
            var isNew = !File.Exists(path);

            if (isNew)
            {
                sb.Append(DateFormat.Heading(now)).Append('\n').Append('\n');
            }

            // 只有首行带时间戳前缀；后续行原样写入、不缩进，
            // 这样使用者自己写的 ## 标题与列表能正常渲染。
            sb.Append(DateFormat.EntryPrefix(now)).Append(lines[0]).Append('\n');
            for (var i = 1; i < lines.Length; i++)
            {
                sb.Append(lines[i]).Append('\n');
            }
            sb.Append('\n');

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
