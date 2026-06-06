using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinZoneTrigger
{
    internal sealed class ProcessParentInfo
    {
        public int ProcessId { get; set; }
        public int ParentProcessId { get; set; }
        public string ProcessName { get; set; }
    }

    internal static class ProcessDiagnostics
    {
        private const uint TH32CS_SNAPPROCESS = 0x00000002;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        public static List<ProcessParentInfo> GetParentChain(int processId, int maxDepth)
        {
            Dictionary<int, ProcessParentInfo> processes = SnapshotProcesses();
            List<ProcessParentInfo> parents = new List<ProcessParentInfo>();
            int currentProcessId = processId;

            for (int depth = 0; depth < Math.Max(1, maxDepth); depth++)
            {
                ProcessParentInfo current;
                if (!processes.TryGetValue(currentProcessId, out current) || current.ParentProcessId <= 0)
                {
                    break;
                }

                ProcessParentInfo parent;
                if (!processes.TryGetValue(current.ParentProcessId, out parent))
                {
                    parents.Add(new ProcessParentInfo
                    {
                        ProcessId = current.ParentProcessId,
                        ParentProcessId = 0,
                        ProcessName = "unknown"
                    });
                    break;
                }

                parents.Add(parent);
                currentProcessId = parent.ProcessId;
            }

            return parents;
        }

        public static string FormatParentChain(IEnumerable<ProcessParentInfo> parents)
        {
            List<string> parts = new List<string>();
            foreach (ProcessParentInfo parent in parents ?? new List<ProcessParentInfo>())
            {
                parts.Add((parent.ProcessName ?? "unknown") + "#" + parent.ProcessId);
            }

            return parts.Count == 0 ? "none" : string.Join(" <- ", parts.ToArray());
        }

        private static Dictionary<int, ProcessParentInfo> SnapshotProcesses()
        {
            Dictionary<int, ProcessParentInfo> processes = new Dictionary<int, ProcessParentInfo>();
            IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snapshot == IntPtr.Zero || snapshot == InvalidHandleValue)
            {
                return processes;
            }

            try
            {
                ProcessEntry32 entry = new ProcessEntry32();
                entry.dwSize = (uint)Marshal.SizeOf(typeof(ProcessEntry32));
                if (!Process32First(snapshot, ref entry))
                {
                    return processes;
                }

                do
                {
                    int processId = unchecked((int)entry.th32ProcessID);
                    processes[processId] = new ProcessParentInfo
                    {
                        ProcessId = processId,
                        ParentProcessId = unchecked((int)entry.th32ParentProcessID),
                        ProcessName = entry.szExeFile ?? ""
                    };
                }
                while (Process32Next(snapshot, ref entry));
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return processes;
        }
    }
}
