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
    internal static class ConfigStore
    {
        public static readonly string ConfigDirectory =
            Environment.GetEnvironmentVariable("WINZONE_TEST_DATA") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinZoneTrigger");

        public static readonly string ConfigPath = Path.Combine(ConfigDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    AppConfig created = AppConfig.CreateDefault();
                    Save(created);
                    return created;
                }

                string json = AtomicFile.Read(ConfigPath);
                AppConfig config = new JavaScriptSerializer().Deserialize<AppConfig>(json);
                if (config == null)
                {
                    config = AppConfig.CreateDefault();
                }

                config.Normalize();
                return config;
            }
            catch
            {
                AppConfig fallback = AppConfig.CreateDefault();
                fallback.Normalize();
                return fallback;
            }
        }

        public static void Save(AppConfig config)
        {
            using (Mutex gate = new Mutex(false, @"Local\WinZoneTrigger.ConfigWrite"))
            {
                bool acquired = false;
                try
                {
                    try { acquired = gate.WaitOne(10000); } catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("다른 화면에서 설정을 저장 중입니다.");
                    SaveCore(config);
                }
                finally { if (acquired) gate.ReleaseMutex(); }
            }
        }

        private static void SaveCore(AppConfig config)
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            config.Normalize();
            string json = new JavaScriptSerializer().Serialize(config);
            AtomicFile.Write(ConfigPath, json);
        }
    }

    internal static class AppIconProvider
    {
        public static Icon CreateApplicationIcon()
        {
            try
            {
                Icon extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    try
                    {
                        return (Icon)extracted.Clone();
                    }
                    finally
                    {
                        extracted.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("앱 아이콘 로드 실패", ex);
            }

            try
            {
                Icon fallback = SystemIcons.Application;
                return fallback == null ? null : (Icon)fallback.Clone();
            }
            catch
            {
                return null;
            }
        }
    }

}
