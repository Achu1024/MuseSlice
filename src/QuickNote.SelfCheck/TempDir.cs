using System;
using System.IO;

namespace QuickNote.SelfCheck
{
    /// <summary>一次性的临时目录，Dispose 时递归删除。目录名故意含中文，因为本机用户名就是中文。</summary>
    internal sealed class TempDir : IDisposable
    {
        public string Path { get; private set; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "qnote 自检-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
            catch { /* 自检清理失败不影响结论 */ }
        }
    }
}
