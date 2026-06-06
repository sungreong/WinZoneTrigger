using System;
using System.IO;
using System.Text;

namespace WinZoneTrigger
{
    internal static class DiagnosticsLog
    {
        private static readonly object SyncRoot = new object();
        private const long MaxLogBytes = 1024 * 1024;

        public static readonly string ErrorLogPath =
            Path.Combine(ConfigStore.ConfigDirectory, "errors.log");

        public static readonly string ActivityLogPath =
            Path.Combine(ConfigStore.ConfigDirectory, "activity.log");

        public static void WriteEvent(string message)
        {
            WriteRaw(ActivityLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (message ?? "") + Environment.NewLine);
        }

        public static void Write(string context, Exception exception)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + (context ?? "오류"));
                builder.AppendLine(exception == null ? "Exception information was not available." : exception.ToString());
                builder.AppendLine();

                WriteRaw(ErrorLogPath, builder.ToString());
                WriteEvent("오류 기록: " + (context ?? "오류") + " / " + (exception == null ? "세부 정보 없음" : exception.Message));
            }
            catch
            {
            }
        }

        private static void WriteRaw(string path, string text)
        {
            try
            {
                if (!Directory.Exists(ConfigStore.ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigStore.ConfigDirectory);
                }

                lock (SyncRoot)
                {
                    RotateLogIfNeeded(path);
                    File.AppendAllText(path, text ?? "", Encoding.UTF8);
                }
            }
            catch
            {
            }
        }

        private static void RotateLogIfNeeded(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                FileInfo info = new FileInfo(path);
                if (info.Length <= MaxLogBytes)
                {
                    return;
                }

                string backupPath = path + ".old";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Move(path, backupPath);
            }
            catch
            {
            }
        }
    }
}
