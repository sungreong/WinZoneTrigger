using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private void ApplyTraySettings()
        {
            if (_config == null || !_config.TrayIconEnabled)
            {
                DisposeTrayIcon();
                DiagnosticsLog.WriteEvent("트레이 아이콘 비활성화");
                return;
            }

            EnsureTrayIcon();
        }

        private void EnsureTrayIcon()
        {
            if (IsShuttingDown())
            {
                return;
            }

            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = true;
                    return;
                }

                _trayMenu = CreateTrayMenu();
                _trayIcon = new NotifyIcon();
                _trayIcon.Icon = CreateTrayIcon();
                _trayIcon.Text = "위치 자동 실행 설정";
                _trayIcon.ContextMenuStrip = _trayMenu;
                _trayIcon.DoubleClick += delegate { ShowMainWindowFromTray(); };
                _trayIcon.Visible = true;
                DiagnosticsLog.WriteEvent("트레이 아이콘 활성화");
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("트레이 아이콘 활성화 실패", ex);
                DisposeTrayIcon();
            }
        }

        private ContextMenuStrip CreateTrayMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("열기", null, delegate { ShowMainWindowFromTray(); });
            menu.Items.Add("설정 폴더", null, delegate { OpenFolderFromTray(ConfigStore.ConfigDirectory); });
            menu.Items.Add("로그 파일", null, delegate { OpenFileFromTray(DiagnosticsLog.ActivityLogPath); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("설정 화면 종료", null, delegate
            {
                DiagnosticsLog.WriteEvent("트레이 종료 메뉴 클릭");
                Close();
            });
            return menu;
        }

        private static Icon CreateTrayIcon()
        {
            return AppIconProvider.CreateApplicationIcon();
        }

        private void ShowMainWindowFromTray()
        {
            try
            {
                if (IsDisposed || Disposing)
                {
                    return;
                }

                if (InvokeRequired)
                {
                    BeginInvoke(new Action(ShowMainWindowFromTray));
                    return;
                }

                Show();
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = FormWindowState.Normal;
                }

                ShowInTaskbar = true;
                Activate();
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("트레이 열기 처리 실패", ex);
            }
        }

        private static void OpenFolderFromTray(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("트레이 설정 폴더 열기 실패", ex);
            }
        }

        private static void OpenFileFromTray(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    DiagnosticsLog.WriteEvent("트레이에서 열 로그 파일이 아직 없습니다: " + path);
                    return;
                }

                Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("트레이 로그 파일 열기 실패", ex);
            }
        }

        private void DisposeTrayIcon()
        {
            NotifyIcon icon = _trayIcon;
            ContextMenuStrip menu = _trayMenu;
            _trayIcon = null;
            _trayMenu = null;

            try
            {
                if (icon != null)
                {
                    icon.Visible = false;
                    if (icon.Icon != null)
                    {
                        icon.Icon.Dispose();
                        icon.Icon = null;
                    }
                    icon.Dispose();
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("트레이 아이콘 정리 실패", ex);
            }

            try
            {
                if (menu != null)
                {
                    menu.Dispose();
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("트레이 메뉴 정리 실패", ex);
            }
        }
    }
}
