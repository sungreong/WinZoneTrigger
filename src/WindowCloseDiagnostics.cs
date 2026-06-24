using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal static class WindowCloseDiagnostics
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        public static string Describe(Form form)
        {
            IntPtr foreground = GetForegroundWindow();
            uint foregroundProcessId;
            GetWindowThreadProcessId(foreground, out foregroundProcessId);

            return "foreground=" + DescribeWindow(foreground, foregroundProcessId)
                + " / formHandle=" + FormatHandle(form == null ? IntPtr.Zero : form.Handle)
                + " / formVisible=" + (form != null && form.Visible)
                + " / formWindowState=" + (form == null ? "" : form.WindowState.ToString())
                + " / cursor=" + Cursor.Position.X + "," + Cursor.Position.Y;
        }

        private static string DescribeWindow(IntPtr handle, uint processId)
        {
            string processText = "pid=" + processId;
            try
            {
                using (Process process = Process.GetProcessById(unchecked((int)processId)))
                {
                    processText += ",name=" + process.ProcessName;
                }
            }
            catch
            {
            }

            return FormatHandle(handle) + " [" + processText + ",title=" + Quote(GetTitle(handle)) + "]";
        }

        private static string GetTitle(IntPtr handle)
        {
            try
            {
                StringBuilder builder = new StringBuilder(256);
                int length = GetWindowText(handle, builder, builder.Capacity);
                return length <= 0 ? "" : builder.ToString();
            }
            catch
            {
                return "";
            }
        }

        private static string FormatHandle(IntPtr handle)
        {
            return "0x" + handle.ToInt64().ToString("X");
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
