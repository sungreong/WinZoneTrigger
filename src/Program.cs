using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                DiagnosticsLog.Write("UI 스레드 예외", e == null ? null : e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                DiagnosticsLog.Write("처리되지 않은 앱 예외", e == null ? null : e.ExceptionObject as Exception);
            };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
            {
                DiagnosticsLog.Write("관찰되지 않은 작업 예외", e == null ? null : e.Exception);
                if (e != null)
                {
                    e.SetObserved();
                }
            };
            Application.ApplicationExit += delegate
            {
                DiagnosticsLog.WriteEvent("앱 종료: ApplicationExit");
            };

            List<ProcessParentInfo> parentChain = ProcessDiagnostics.GetParentChain(Process.GetCurrentProcess().Id, 8);
            DiagnosticsLog.WriteEvent("앱 시작: pid=" + Process.GetCurrentProcess().Id
                + " / exe=" + Application.ExecutablePath
                + " / args=" + FormatArguments(args)
                + " / parents=" + ProcessDiagnostics.FormatParentChain(parentChain));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool startMinimized = args != null && args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
            bool startedFromWindowsStartup = args != null && args.Any(a => string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(startMinimized, startedFromWindowsStartup));
        }

        private static string FormatArguments(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "none";
            }

            return string.Join(" ", args.Select(QuoteArgument).ToArray());
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
