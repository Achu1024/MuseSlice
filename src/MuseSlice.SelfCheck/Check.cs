using System;
using System.Collections.Generic;

namespace MuseSlice.SelfCheck
{
    internal static class Check
    {
        private static readonly List<string> Failures = new List<string>();
        private static string _section = "(未分组)";

        public static void Section(string name)
        {
            _section = name;
            Console.WriteLine();
            Console.WriteLine("== " + name + " ==");
        }

        public static void That(bool condition, string what)
        {
            if (condition)
            {
                Console.WriteLine("  PASS  " + what);
                return;
            }
            Console.WriteLine("  FAIL  " + what);
            Failures.Add(_section + " / " + what);
        }

        public static void Equal<T>(T actual, T expected, string what)
        {
            if (EqualityComparer<T>.Default.Equals(actual, expected))
            {
                Console.WriteLine("  PASS  " + what);
                return;
            }
            Console.WriteLine("  FAIL  " + what);
            Console.WriteLine("        期望: " + Show(expected));
            Console.WriteLine("        实际: " + Show(actual));
            Failures.Add(_section + " / " + what);
        }

        private static string Show(object v)
        {
            if (v == null) return "<null>";
            return "\"" + v.ToString()
                .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
        }

        public static int Report()
        {
            Console.WriteLine();
            if (Failures.Count == 0)
            {
                Console.WriteLine("全部通过");
                return 0;
            }
            Console.WriteLine(Failures.Count + " 项失败：");
            foreach (var f in Failures) Console.WriteLine("  - " + f);
            return 1;
        }
    }
}
