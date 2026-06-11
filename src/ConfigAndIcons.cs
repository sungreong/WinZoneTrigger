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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinZoneTrigger");

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

                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
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
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            config.Normalize();
            string json = new JavaScriptSerializer().Serialize(config);
            File.WriteAllText(ConfigPath, json, Encoding.UTF8);
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
