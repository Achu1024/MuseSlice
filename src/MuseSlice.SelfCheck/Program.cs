using System;
using System.Text;

namespace MuseSlice.SelfCheck
{
    internal static class Program
    {
        private static int Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            try
            {
                DateFormatChecks.Run();
                EntryLineChecks.Run();
                CaretPulseChecks.Run();
                CaretBoxChecks.Run();
                ChimeChecks.Run();
                NoteStoreChecks.Run();
                AppConfigChecks.Run();
                InputRulesChecks.Run();
                DoubleTapDetectorChecks.Run();
                NoteSearcherChecks.Run();
                BarGeometryChecks.Run();
                ReviewFixChecks.Run();
                // 后续任务在此处依次追加
            }
            catch (Exception ex)
            {
                Console.WriteLine("自检自身崩溃：" + ex);
                return 2;
            }
            return Check.Report();
        }
    }
}
