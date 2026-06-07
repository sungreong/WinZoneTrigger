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
    internal sealed partial class MainForm : Form
    {
        private void ShowTrayNotification(string title, string message)
        {
            DiagnosticsLog.WriteEvent("트레이 알림 생략: " + (title ?? "") + " / " + (message ?? ""));
        }

        private static string BuildLaunchNotificationText(string target)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return "등록된 프로그램을 실행했습니다.";
            }

            string display = TryGetAppsFolderDisplayName(value);
            if (string.IsNullOrWhiteSpace(display))
            {
                display = value;
                try
                {
                    if (display.IndexOf(Path.DirectorySeparatorChar) >= 0 || display.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(display);
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            display = fileName;
                        }
                    }
                }
                catch
                {
                }
            }

            return ShortenText(display, 80) + " 실행됨";
        }

        private void ShowCommandHelp()
        {
            string text =
                "고급 명령어는 위치에 진입했을 때 위에서 아래로 한 줄씩 실행됩니다." + Environment.NewLine + Environment.NewLine +
                "실행 방식" + Environment.NewLine +
                "- 각 줄은 cmd.exe /c 로 실행됩니다." + Environment.NewLine +
                "- 경로에 공백이 있으면 따옴표로 감싸주세요." + Environment.NewLine +
                "- 앱 실행이나 Chrome 링크로 표현하기 어려운 작업만 여기에 넣는 것을 권장합니다." + Environment.NewLine + Environment.NewLine +
                "예시" + Environment.NewLine +
                "notepad.exe" + Environment.NewLine +
                "explorer.exe C:\\Users" + Environment.NewLine +
                "start \"\" \"C:\\path\\file.txt\"" + Environment.NewLine +
                "powershell -NoProfile -Command \"Write-Output hello\"";

            MessageBox.Show(this, text, "고급 명령어 도움말", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InsertCommandExample()
        {
            string example =
                "notepad.exe" + Environment.NewLine +
                "explorer.exe C:\\Users";

            if (string.IsNullOrWhiteSpace(_commandsText.Text))
            {
                _commandsText.Text = example;
            }
            else
            {
                _commandsText.AppendText(Environment.NewLine + example);
            }

            _commandsText.Focus();
            _commandsText.SelectionStart = _commandsText.TextLength;
            CaptureCurrentZone();
        }

        private Button CreateAppLaunchChip(string target)
        {
            Button chip = CreateValueChip(target, ShortenAppTargetForChip(target), string.Equals(_selectedAppLaunch, target, StringComparison.OrdinalIgnoreCase));
            chip.Click += delegate
            {
                _selectedAppLaunch = Convert.ToString(chip.Tag);
                RenderAppLaunchChips();
            };
            return chip;
        }

        private string ShortenAppTargetForChip(string target)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return "";
            }

            string appsFolderName = TryGetAppsFolderDisplayName(value);
            if (!string.IsNullOrWhiteSpace(appsFolderName))
            {
                return ShortenText(appsFolderName, 34);
            }

            try
            {
                if (value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(value);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        return ShortenText(fileName, 34);
                    }
                }
            }
            catch
            {
            }

            return ShortenText(value, 34);
        }

        private static string TryGetAppsFolderDisplayName(string target)
        {
            string value = (target ?? "").Trim();
            const string prefix = @"shell:AppsFolder\";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string appId = value.Substring(prefix.Length);
            int bang = appId.IndexOf('!');
            if (bang > 0)
            {
                appId = appId.Substring(0, bang);
            }

            int underscore = appId.IndexOf('_');
            if (underscore > 0)
            {
                appId = appId.Substring(0, underscore);
            }

            int dot = appId.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < appId.Length)
            {
                appId = appId.Substring(dot + 1);
            }

            return appId.Trim();
        }

        private void RenderAppLaunchChips()
        {
            if (_appLaunchChipsPanel == null)
            {
                return;
            }

            List<string> apps = GetAppLaunches();
            ClearChildControls(_appLaunchChipsPanel);
            if (apps.Count == 0)
            {
                _selectedAppLaunch = null;
                AddEmptyChipHint(_appLaunchChipsPanel, "등록된 앱 없음");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedAppLaunch) || !apps.Any(app => string.Equals(app, _selectedAppLaunch, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedAppLaunch = apps[0];
            }

            foreach (string app in apps)
            {
                _appLaunchChipsPanel.Controls.Add(CreateAppLaunchChip(app));
            }
        }

        private void AddEmptyChipHint(FlowLayoutPanel panel, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = UiTextMuted;
            label.Tag = "Muted";
            label.Margin = new Padding(8, 8, 8, 4);
            panel.Controls.Add(label);
        }

        private static string NormalizeUrlForDisplay(string value)
        {
            string url = (value ?? "").Trim();
            if (url.Length == 0)
            {
                return "";
            }

            if (url.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                url = "https://" + url;
            }

            return url;
        }

        private void KeepDetailPaneReadable(SplitContainer split)
        {
            const int preferredLeftWidth = 320;
            const int minimumLeftWidth = 300;
            const int minimumDetailWidth = 560;

            if (split == null || split.Width <= 0)
            {
                return;
            }

            int availableWidth = split.Width - split.SplitterWidth;
            int maxLeftWidth = availableWidth - minimumDetailWidth;
            if (maxLeftWidth < minimumLeftWidth)
            {
                maxLeftWidth = Math.Max(minimumLeftWidth, availableWidth - 420);
            }

            int target = Math.Min(preferredLeftWidth, maxLeftWidth);
            target = Math.Max(minimumLeftWidth, target);

            if (split.Panel2.Width >= minimumDetailWidth &&
                split.SplitterDistance >= minimumLeftWidth &&
                split.SplitterDistance <= maxLeftWidth)
            {
                return;
            }

            if (split.SplitterDistance != target)
            {
                split.SplitterDistance = target;
            }
        }

        private void AddRow(string labelText, Control control)
        {
            AddRowTo(_conditionTable, labelText, control);
        }

        private void AddRowTo(TableLayoutPanel table, string labelText, Control control)
        {
            if (table == null || control == null)
            {
                return;
            }

            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = UiTextMuted;
            label.Margin = new Padding(0, 7, 10, 5);
            label.TextAlign = ContentAlignment.TopLeft;
            label.Tag = "Muted";
            table.Controls.Add(label, 0, row);

            control.Margin = new Padding(0, 3, 0, 5);
            table.Controls.Add(control, 1, row);
        }

        private void AddSectionHeader(string text)
        {
            AddSectionHeaderTo(_conditionTable, text);
        }

        private void AddSectionHeaderTo(TableLayoutPanel table, string text)
        {
            if (table == null)
            {
                return;
            }

            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label label = new Label();
            label.Text = text;
            label.Font = new Font(Font.FontFamily, 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = UiAccentDark;
            label.AutoSize = false;
            label.Height = 28;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.BackColor = UiAccentSoft;
            label.Margin = new Padding(0, 9, 0, 6);
            label.Padding = new Padding(10, 0, 0, 0);
            label.Tag = "SectionHeader";

            table.Controls.Add(label, 0, row);
            table.SetColumnSpan(label, 2);
        }

    }
}
