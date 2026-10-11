using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 把「防抖、取消上一次扫描、结果回到 UI 线程」这堆易错的并发细节
    /// 从界面代码里隔离出去。界面只负责给查询词、把结果画出来，不碰线程。
    /// <para>
    /// 扫描必须在后台：扫一千个文件要几百毫秒，跑在 UI 线程上会连带
    /// 全局键盘钩子一起卡住全系统键盘。
    /// </para>
    /// <para>
    /// 防抖用 Task.Delay 而不是 UI 定时器——它只在搜索期间存在，
    /// 待机时进程里没有任何计时器在跑。
    /// </para>
    /// </summary>
    internal sealed class SearchController : IDisposable
    {
        private readonly Control _ui;
        private readonly string _notesDir;
        private readonly Action<SearchResult> _onResult;
        private readonly Action _onStarted;
        private readonly int _debounceMs;
        private CancellationTokenSource _cts;

        public SearchController(Control ui, string notesDir,
            Action<SearchResult> onResult, Action onStarted, int debounceMs = 200)
        {
            _ui = ui;
            _notesDir = notesDir;
            _onResult = onResult;
            _onStarted = onStarted;
            _debounceMs = debounceMs;
        }

        public void Query(string text)
        {
            Cancel();

            var query = (text ?? "").Trim();
            var cts = new CancellationTokenSource();
            _cts = cts;
            _onStarted();

            var dir = _notesDir;
            var limit = NoteSearcher.DefaultLimit;
            // 空查询是「列最近记录」，不筛任何东西，没什么可防抖的——直接扫。
            var debounce = query.Length == 0 ? 0 : _debounceMs;

            Task.Run(async delegate
            {
                try
                {
                    if (debounce > 0) await Task.Delay(debounce, cts.Token).ConfigureAwait(false);

                    var result = query.Length == 0
                        ? NoteSearcher.ListRecent(dir, limit, cts.Token)
                        : NoteSearcher.Search(dir, query, limit, cts.Token);
                    if (cts.IsCancellationRequested) return;

                    if (_ui.IsHandleCreated && !_ui.IsDisposed)
                    {
                        _ui.BeginInvoke((Action)delegate
                        {
                            if (!cts.IsCancellationRequested) _onResult(result);
                        });
                    }
                }
                catch (OperationCanceledException) { /* 被新的查询取代，正常 */ }
                catch (Exception ex) { MuseSliceApp.Log("搜索失败：" + ex); }
            });
        }

        public void Cancel()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
        }

        public void Dispose() { Cancel(); }
    }
}
