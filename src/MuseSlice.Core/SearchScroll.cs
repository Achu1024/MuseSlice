using System;

namespace MuseSlice.Core
{
    /// <summary>
    /// 结果列表的滚动位置。抽成纯函数是因为这里的边界最容易写错，
    /// 而错了的表现是「回车打开了一条看不见的记录」——很难靠肉眼发现。
    /// </summary>
    public static class SearchScroll
    {
        /// <summary>保证 selected 落在 [first, first+visibleRows-1] 内，并钳制在结果总数之内。</summary>
        public static int FirstVisible(int selected, int visibleRows, int total, int currentFirst)
        {
            if (visibleRows <= 0 || total <= 0) return 0;

            var maxFirst = total - visibleRows;
            if (maxFirst < 0) maxFirst = 0;

            var first = currentFirst;
            if (first < 0) first = 0;
            if (first > maxFirst) first = maxFirst;

            if (selected < first) first = selected;
            else if (selected >= first + visibleRows) first = selected - visibleRows + 1;

            if (first < 0) first = 0;
            if (first > maxFirst) first = maxFirst;
            return first;
        }
    }
}
