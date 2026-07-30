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
            _trayAutomationMenuItem = CreateTrayAutomationMenuItem();
            menu.Items.Add(_trayAutomationMenuItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("설정 폴더", null, delegate { OpenFolderFromTray(ConfigStore.ConfigDirectory); });
            menu.Items.Add("로그 파일", null, delegate { OpenFileFromTray(DiagnosticsLog.ActivityLogPath); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("설정 화면 종료", null, delegate
            {
                DiagnosticsLog.WriteEvent("트레이 종료 메뉴 클릭");
                _allowSettingsScreenClose = true;
                Close();
            });
            menu.Opening += delegate { UpdateTrayAutomationMenu(); };
            UpdateTrayAutomationMenu();
            return menu;
        }

        private ToolStripMenuItem CreateTrayAutomationMenuItem()
        {
            ToolStripMenuItem item = new ToolStripMenuItem();
            item.DropDownOpening += delegate { PopulateTrayAutomationMenu(item); };
            return item;
        }

        private void UpdateTrayAutomationMenu()
        {
            if (_trayAutomationMenuItem == null || _trayAutomationMenuItem.IsDisposed)
            {
                return;
            }

            bool paused = _config != null && _config.IsAutomationPaused();
            _trayAutomationMenuItem.Text = paused
                ? "자동화 정지 중 · " + FormatPauseButtonUntil(_config.AutomationPausedUntilUtc.Value)
                : "자동화 실행 중 · 임시 정지";

            if (_trayIcon != null)
            {
                _trayIcon.Text = paused
                    ? "위치 자동 실행 설정 (자동화 정지 중)"
                    : "위치 자동 실행 설정 (자동화 실행 중)";
            }
        }

        private void PopulateTrayAutomationMenu(ToolStripMenuItem item)
        {
            item.DropDownItems.Clear();

            if (_config != null && _config.IsAutomationPaused())
            {
                item.DropDownItems.Add("자동화 바로 다시 시작", null, delegate { ResumeAutomation(); });
                return;
            }

            AddTrayPauseMenuItem(item, "30분 동안 정지", TimeSpan.FromMinutes(30));
            AddTrayPauseMenuItem(item, "1시간 동안 정지", TimeSpan.FromHours(1));
            AddTrayPauseMenuItem(item, "2시간 동안 정지", TimeSpan.FromHours(2));
            AddTrayPauseMenuItem(item, "12시간 동안 정지", TimeSpan.FromHours(12));
            item.DropDownItems.Add(new ToolStripSeparator());
            item.DropDownItems.Add("오늘 자정까지 정지", null, delegate
            {
                PauseAutomationFor(DateTime.Today.AddDays(1) - DateTime.Now);
            });
        }

        private void AddTrayPauseMenuItem(ToolStripMenuItem menu, string text, TimeSpan duration)
        {
            menu.DropDownItems.Add(text, null, delegate { PauseAutomationFor(duration); });
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
            _trayAutomationMenuItem = null;

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
