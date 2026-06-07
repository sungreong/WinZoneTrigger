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

    internal static class AppIcons
    {
        public static Icon GetAppIcon()
        {
            try
            {
                return (Icon)SystemIcons.Application.Clone();
            }
            catch
            {
                return null;
            }
        }

        public static void DisposeIcon(Form form)
        {
            if (form == null || form.Icon == null)
            {
                return;
            }

            Icon icon = form.Icon;
            form.Icon = null;
            icon.Dispose();
        }

        public static void DisposeIcon(NotifyIcon notifyIcon)
        {
            if (notifyIcon == null || notifyIcon.Icon == null)
            {
                return;
            }

            Icon icon = notifyIcon.Icon;
            notifyIcon.Icon = null;
            icon.Dispose();
        }
    }
}
