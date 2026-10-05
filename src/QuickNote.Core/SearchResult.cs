using System;
using System.Collections.Generic;

namespace QuickNote.Core
{
    public sealed class SearchHit
    {
        public string FilePath { get; set; }
        public DateTime Date { get; set; }
        public int LineNumber { get; set; }
        public string Line { get; set; }

        /// <summary>紧随匹配行的续行；若下一非空行是另一条记录（时间戳前缀）则为空串。</summary>
        public string Context { get; set; }
    }

    public sealed class SearchResult
    {
        public IList<SearchHit> Hits { get; set; }
        public bool Truncated { get; set; }
        public int ScannedFiles { get; set; }
        public int UnreadableFiles { get; set; }

        public SearchResult()
        {
            Hits = new List<SearchHit>();
        }

        public static SearchResult Empty()
        {
            return new SearchResult();
        }
    }
}
