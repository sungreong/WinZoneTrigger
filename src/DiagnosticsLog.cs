using System;
using System.IO;
using System.Text;

namespace WinZoneTrigger
{
    internal static class DiagnosticsLog
    {
        private static readonly object SyncRoot = new object();

        public static readonly string ErrorLogPath =
            Path.Combine(ConfigStore.ConfigDirectory, "errors.log");

        public static void Write(string context, Exception exception)
        {
            try
            {
                if (!Directory.Exists(ConfigStore.ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigStore.ConfigDirectory);
                }

                StringBuilder builder = new StringBuilder();
                builder.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (context ?? "오류"));
                builder.AppendLine(exception == null ? "Exception information was not available." : exception.ToString());
                builder.AppendLine();

                lock (SyncRoot)
                {
                    File.AppendAllText(ErrorLogPath, builder.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
