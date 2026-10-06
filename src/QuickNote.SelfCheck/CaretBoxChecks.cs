using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class CaretBoxChecks
    {
        public static void Run()
        {
            Check.Section("CaretBox");

            int top, height;

            // Noto Sans SC 在 125% 下的近似值：行距 27、字身 22（升 19 + 降 3）。
            // 光标要贴着文字、不是顶着行盒——原先拿行距当高度，那根线比文字高出一截，
            // 上头顶到了分隔线。
            CaretBox.Range(27, 19, 3, out top, out height);
            Check.Equal(height, 22, "高度取字身（升 + 降），不是行距");
            Check.Equal(top, 2, "在行盒里居中：上下各留 2px");
            Check.That(top + height <= 27, "光标不越出自己那一行");

            // 字身正好等于行距时没有余量，只能顶格
            CaretBox.Range(22, 19, 3, out top, out height);
            Check.Equal(height, 22, "字身等于行距时高度不变");
            Check.Equal(top, 0, "没有余量时上边距为 0");

            // 字身比行距还大（某些字体的降部很夸张）时不能越界
            CaretBox.Range(20, 25, 5, out top, out height);
            Check.Equal(height, 20, "字身超过行距时被钳到行距");
            Check.Equal(top, 0, "被钳之后上边距为 0");

            // 拿不到字身（字体度量 API 失败）时退回行距，总比没有好
            CaretBox.Range(30, 0, 0, out top, out height);
            Check.Equal(height, 30, "字身拿不到时退回行距");
            Check.Equal(top, 0, "退回行距时上边距为 0");

            // 拿不到行距
            CaretBox.Range(0, 19, 3, out top, out height);
            Check.Equal(height, 22, "行距拿不到时仍用字身，且不除零");
            Check.Equal(top, 0, "行距拿不到时上边距为 0");

            CaretBox.Range(-5, 19, 3, out top, out height);
            Check.Equal(height, 22, "负行距也不除零");
            Check.Equal(top, 0, "负行距时上边距为 0");

            // 奇数余量：整数除法向下取，上面少 1px 比下面少 1px 好看
            CaretBox.Range(23, 19, 3, out top, out height);
            Check.Equal(height, 22, "奇数余量时高度不变");
            Check.Equal(top, 0, "余 1px 时上边距为 0，余量留给下面");

            CaretBox.Range(25, 19, 3, out top, out height);
            Check.Equal(top, 1, "余 3px 时上边距取 1（向下取整）");

            // 真正要防的那件事：光标顶端不能贴着行盒顶（也就是贴着上面的分隔线）。
            // 1px 余量时上边距为 0 是有意的——1px 的偏移看不出来，所以门槛设在 ≥2px。
            var offenders = 0;
            for (var lineSpacing = 20; lineSpacing <= 40; lineSpacing++)
            {
                CaretBox.Range(lineSpacing, 16, 4, out top, out height);   // 字身 20
                if (lineSpacing - height >= 2 && top < 1) offenders++;
            }
            Check.Equal(offenders, 0, "行盒富余 ≥2px 时，光标一定有上边距，不会顶到分隔线");
        }
    }
}
