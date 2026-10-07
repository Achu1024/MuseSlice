using System;
using System.Collections.Generic;
using System.IO;

namespace QuickNote.Core
{
    /// <summary>
    /// 「叮咚」提示音的合成。
    /// <para>
    /// 自己合成而不是用系统提示音：SystemSounds 那几种（Asterisk / Beep / Exclamation…）
    /// 里没有「叮咚」，而引一个音频库或带一个 wav 文件都违反零依赖。
    /// 合成还有个好处——波形完全可控，也能用断言把 WAV 头钉死（头写错会静默不响，
    /// 那是肉眼和耳朵都很难归因的失败）。
    /// </para>
    /// <para>
    /// 两声下行（先高后低），各带指数衰减与 3 毫秒起音淡入。淡入是为了不「啪」——
    /// 第一个采样直接从 0 跳到峰值就是一个咔哒声。
    /// </para>
    /// </summary>
    public static class Chime
    {
        public const int SampleRate = 44100;

        /// <summary>第一声（高）。E6。</summary>
        public const double FirstHz = 1318.51;

        /// <summary>第二声（低）。C6——从 E6 下行一个大三度，是门铃那个「叮咚」。</summary>
        public const double SecondHz = 1046.50;

        private const double FirstSeconds = 0.12;
        private const double SecondSeconds = 0.20;
        private const double Amplitude = 0.30;
        private const double AttackSeconds = 0.003;

        /// <summary>合成好的 16 位单声道 PCM WAV。每次调用都重新生成——只有几十 KB，且保存时才用。</summary>
        public static byte[] Wav()
        {
            var samples = new List<short>();
            AppendNote(samples, FirstHz, FirstSeconds, 0.055);
            AppendNote(samples, SecondHz, SecondSeconds, 0.085);
            return Pack(samples);
        }

        private static void AppendNote(List<short> into, double hz, double seconds, double decayTau)
        {
            var count = (int)Math.Round(seconds * SampleRate);

            for (var i = 0; i < count; i++)
            {
                var t = (double)i / SampleRate;

                var envelope = Math.Exp(-t / decayTau);
                if (t < AttackSeconds) envelope *= t / AttackSeconds;   // 起音淡入，防咔哒

                // 加一点二次谐波，让它听起来像小铃铛而不是纯电子音。
                // 除以 1.15 是把谐波算进去之后仍不越界。
                var wave = (Math.Sin(2 * Math.PI * hz * t)
                            + 0.15 * Math.Sin(4 * Math.PI * hz * t)) / 1.15;

                into.Add((short)Math.Round(Amplitude * envelope * wave * short.MaxValue));
            }
        }

        private static byte[] Pack(List<short> samples)
        {
            var dataBytes = samples.Count * 2;

            using (var ms = new MemoryStream(44 + dataBytes))
            using (var w = new BinaryWriter(ms))
            {
                WriteTag(w, "RIFF");
                w.Write(36 + dataBytes);          // 之后的全部字节数
                WriteTag(w, "WAVE");

                WriteTag(w, "fmt ");
                w.Write(16);                       // PCM 的 fmt 块固定 16
                w.Write((short)1);                 // PCM
                w.Write((short)1);                 // 单声道
                w.Write(SampleRate);
                w.Write(SampleRate * 2);           // 字节率 = 采样率 × 声道 × 位深/8
                w.Write((short)2);                 // 块对齐
                w.Write((short)16);                // 位深

                WriteTag(w, "data");
                w.Write(dataBytes);
                foreach (var s in samples) w.Write(s);

                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>四个 ASCII 字节的块标识。不用 Write(string)——那会走编码，长度不可控。</summary>
        private static void WriteTag(BinaryWriter w, string tag)
        {
            foreach (var c in tag) w.Write((byte)c);
        }
    }
}
