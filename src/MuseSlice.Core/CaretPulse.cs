namespace MuseSlice.Core
{
    /// <summary>
    /// 自绘光标的明暗曲线。1 秒一循环的三角波，不是硬开关的闪烁。
    /// <para>
    /// 纯函数，方便离线钉住曲线形状——这东西好不好看只能靠眼睛，
    /// 至少把「什么时候最亮」钉死在断言里。
    /// </para>
    /// </summary>
    public static class CaretPulse
    {
        public const int PeriodMs = 1000;

        /// <summary>最暗时的不透明度。不落到 0：光标整个消失看起来像丢了焦点。</summary>
        public const double MinLevel = 0.35;

        /// <summary>把经过的毫秒映射成 0.35–1.0 的亮度。</summary>
        public static double LevelAt(int elapsedMs)
        {
            var t = elapsedMs % PeriodMs;
            if (t < 0) t += PeriodMs;

            var half = PeriodMs / 2.0;
            var up = t < half ? t / half : (PeriodMs - t) / half;
            return MinLevel + (1.0 - MinLevel) * up;
        }
    }
}
