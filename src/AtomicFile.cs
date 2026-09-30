using System;
using System.IO;
using System.Text;
using System.Threading;

namespace WinZoneTrigger
{
    internal static class AtomicFile
    {
        public static string Read(string path)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8)) return reader.ReadToEnd();
                }
                catch (IOException) { if (attempt >= 5) throw; Thread.Sleep(40); }
            }
        }
        public static void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                        else File.Move(temporary, path);
                        break;
                    }
                    catch (IOException) { if (attempt >= 5) throw; Thread.Sleep(40); }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
