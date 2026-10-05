using System;

namespace QuickNote.Core
{
    /// <summary>
    /// 写入笔记失败。抛出这个异常而不是静默失败，是为了让界面能明确提示
    /// 「内容还在，但没写进去」——丢数据比任何崩溃都严重。
    /// </summary>
    public sealed class NoteStoreException : Exception
    {
        public NoteStoreException(string message, Exception inner) : base(message, inner) { }
    }
}
