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
        private Control CreateSelectedZoneSummaryBar()
        {
            TableLayoutPanel summary = new TableLayoutPanel();
            summary.Dock = DockStyle.Top;
            summary.AutoSize = true;
            summary.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            summary.BackColor = UiSurfaceMuted;
            summary.Padding = new Padding(12, 8, 12, 8);
            summary.Margin = new Padding(0, 0, 0, 8);
            summary.ColumnCount = 1;
            summary.RowCount = 2;
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TableLayoutPanel textStack = new TableLayoutPanel();
            textStack.Dock = DockStyle.Top;
            textStack.AutoSize = true;
            textStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            textStack.ColumnCount = 1;
            textStack.RowCount = 3;
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _selectedZoneSummaryLabel = new Label();
            _selectedZoneSummaryLabel.Text = "위치를 선택하세요";
            _selectedZoneSummaryLabel.AutoSize = true;
            _selectedZoneSummaryLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            _selectedZoneSummaryLabel.ForeColor = UiAccentDark;
            _selectedZoneSummaryLabel.Margin = new Padding(0, 0, 0, 3);
            _selectedZoneSummaryLabel.Tag = "AccentTitle";
            textStack.Controls.Add(_selectedZoneSummaryLabel, 0, 0);

            _selectedZoneMetaLabel = new Label();
            _selectedZoneMetaLabel.Text = "등록된 위치를 선택하면 감지 조건과 실행 동작이 여기에 요약됩니다.";
            _selectedZoneMetaLabel.AutoSize = true;
            _selectedZoneMetaLabel.MaximumSize = new Size(620, 0);
            _selectedZoneMetaLabel.ForeColor = UiTextMuted;
            _selectedZoneMetaLabel.Margin = new Padding(0, 0, 0, 5);
            _selectedZoneMetaLabel.Tag = "Muted";
            textStack.Controls.Add(_selectedZoneMetaLabel, 0, 1);

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.AutoSize = true;
            badges.WrapContents = true;
            badges.Margin = new Padding(0);
            badges.BackColor = UiSurfaceMuted;
            _summaryOperatingBadge = CreateSummaryBadge("미선택");
            _summaryMatchBadge = CreateSummaryBadge("대기");
            _summaryModeBadge = CreateSummaryBadge("감지 방식 없음");
            badges.Controls.Add(_summaryOperatingBadge);
            badges.Controls.Add(_summaryMatchBadge);
            badges.Controls.Add(_summaryModeBadge);
            textStack.Controls.Add(badges, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.AutoSize = false;
            buttons.Height = 34;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Margin = new Padding(0, 7, 0, 0);
            buttons.BackColor = UiSurfaceMuted;

            Button saveSelectedButton = CreateButton("저장");
            SetFixedButtonSize(saveSelectedButton, 74, 30);
            saveSelectedButton.Click += delegate { SaveFromUi(); };
            buttons.Controls.Add(saveSelectedButton);

            Button operateButton = CreateButton("운영하기");
            SetFixedButtonSize(operateButton, 82, 30);
            operateButton.Click += delegate { SetSelectedZoneOperating(true); };
            buttons.Controls.Add(operateButton);

            Button stopOperatingButton = CreateButton("운영 중지");
            SetFixedButtonSize(stopOperatingButton, 82, 30);
            stopOperatingButton.Click += delegate { SetSelectedZoneOperating(false); };
            buttons.Controls.Add(stopOperatingButton);

            Button testConditionButton = CreateButton("테스트해보기");
            SetFixedButtonSize(testConditionButton, 94, 30);
            testConditionButton.Click += delegate { TestSelectedZoneCondition(); };
            buttons.Controls.Add(testConditionButton);

            Button testActionsButton = CreateButton("동작 테스트");
            SetFixedButtonSize(testActionsButton, 88, 30);
            testActionsButton.Click += delegate { TestSelectedZoneActions(); };
            buttons.Controls.Add(testActionsButton);

            Button openConfigButton = CreateButton("설정 폴더");
            SetFixedButtonSize(openConfigButton, 82, 30);
            openConfigButton.Click += delegate { OpenConfigFolder(); };
            buttons.Controls.Add(openConfigButton);

            summary.Controls.Add(textStack, 0, 0);
            summary.Controls.Add(buttons, 0, 1);
            return summary;
        }

        private TableLayoutPanel CreateDetailTable()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.ColumnCount = 2;
            table.BackColor = UiSurface;
            table.Padding = new Padding(2, 0, 6, 10);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private TabPage CreateDetailTabPage(string title, TableLayoutPanel table)
        {
            TabPage page = new TabPage(title);
            page.BackColor = UiSurface;
            page.ForeColor = UiText;

            Panel scroller = new Panel();
            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.BackColor = UiSurface;
            scroller.Padding = new Padding(0, 5, 0, 0);
            scroller.Controls.Add(table);
            page.Controls.Add(scroller);
            return page;
        }

        private Label CreateSummaryBadge(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Height = 24;
            label.Width = Math.Max(82, TextRenderer.MeasureText(text, Font).Width + 22);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Margin = new Padding(0, 0, 6, 0);
            label.Padding = new Padding(8, 0, 8, 0);
            label.BorderStyle = BorderStyle.FixedSingle;
            label.Tag = "SummaryBadge";
            SetSummaryBadge(label, text, UiSurface, UiTextMuted);
            return label;
        }

        private void SetSummaryBadge(Label label, string text, Color background, Color foreground)
        {
            if (label == null)
            {
                return;
            }

            label.Text = text;
            label.Width = Math.Max(82, TextRenderer.MeasureText(text, Font).Width + 22);
            label.BackColor = background;
            label.ForeColor = foreground;
        }

        private void UpdateSelectedZoneSummary()
        {
            if (_selectedZoneSummaryLabel == null)
            {
                return;
            }

            ZoneRule zone = GetSelectedZone();
            if (zone == null)
            {
                _selectedZoneSummaryLabel.Text = "위치를 선택하세요";
                _selectedZoneMetaLabel.Text = "등록된 위치를 선택하면 감지 조건과 실행 동작이 여기에 요약됩니다.";
                SetSummaryBadge(_summaryOperatingBadge, "미선택", UiSurface, UiTextMuted);
                SetSummaryBadge(_summaryMatchBadge, "대기", UiSurface, UiTextMuted);
                SetSummaryBadge(_summaryModeBadge, "감지 방식 없음", UiSurface, UiTextMuted);
                return;
            }

            string name = _loadingSelection ? zone.Name : (string.IsNullOrWhiteSpace(_zoneNameText.Text) ? zone.Name : _zoneNameText.Text.Trim());
            _selectedZoneSummaryLabel.Text = name;
            _selectedZoneMetaLabel.Text = BuildZoneActionSummary(zone);

            bool matched = IsZoneActive(zone);
            SetSummaryBadge(
                _summaryOperatingBadge,
                zone.Enabled ? "운영 중" : "미운영",
                zone.Enabled ? UiAccent : UiSurface,
                zone.Enabled ? Color.White : UiTextMuted);
            SetSummaryBadge(
                _summaryMatchBadge,
                matched ? "조건 일치" : "대기",
                matched ? UiAccentSoft : UiSurface,
                matched ? UiAccentDark : UiTextMuted);
            SetSummaryBadge(_summaryModeBadge, BuildZoneModeSummary(zone), UiSurface, UiText);
        }

        private string BuildZoneModeSummary(ZoneRule zone)
        {
            if (zone == null)
            {
                return "감지 방식 없음";
            }

            if (zone.UseCoordinates)
            {
                return "좌표 " + zone.RadiusMeters + "m";
            }

            int wifiCount = zone.NearbySsids == null ? 0 : zone.NearbySsids.Count(s => !string.IsNullOrWhiteSpace(s));
            return wifiCount == 0 ? "Wi-Fi 미설정" : "Wi-Fi " + wifiCount + "개";
        }

        private string BuildZoneActionSummary(ZoneRule zone)
        {
            if (zone == null)
            {
                return "";
            }

            List<string> actions = new List<string>();
            if (zone.ConnectWifiEnabled.GetValueOrDefault(false))
            {
                actions.Add("Wi-Fi 연결");
            }
            if (!string.IsNullOrWhiteSpace(zone.AudioAction) && !string.Equals(zone.AudioAction, "None", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(string.Equals(zone.AudioAction, "Mute", StringComparison.OrdinalIgnoreCase) ? "음소거" : "음소거 해제");
            }
            if (zone.ChromeUrls != null && zone.ChromeUrls.Any(u => !string.IsNullOrWhiteSpace(u)))
            {
                actions.Add("링크 " + zone.ChromeUrls.Count(u => !string.IsNullOrWhiteSpace(u)) + "개");
            }
            if (zone.AppLaunches != null && zone.AppLaunches.Any(a => !string.IsNullOrWhiteSpace(a)))
            {
                actions.Add("앱 " + zone.AppLaunches.Count(a => !string.IsNullOrWhiteSpace(a)) + "개");
            }
            int appWatchCount = zone.AppWatchItems == null ? 0 : zone.AppWatchItems.Count(item => item != null && item.Enabled);
            if (appWatchCount > 0)
            {
                actions.Add("앱 감시 " + appWatchCount + "개");
            }
            if (zone.Commands != null && zone.Commands.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                actions.Add("명령 " + zone.Commands.Count(c => !string.IsNullOrWhiteSpace(c)) + "개");
            }

            List<string> schedules = new List<string>();
            if (zone.RunOnceAtStartup.GetValueOrDefault(true))
            {
                schedules.Add("시작 1회");
            }
            if (zone.MonitoringEnabled.GetValueOrDefault(false))
            {
                schedules.Add("지속 " + Math.Max(5, zone.ScanIntervalSeconds) + "초");
            }

            string prefix = zone.Enabled ? "자동 실행 준비됨" : "운영 중지됨";
            if (schedules.Count > 0)
            {
                prefix += " · " + string.Join("/", schedules.ToArray());
            }
            return actions.Count == 0 ? prefix + " · 실행 동작 없음" : prefix + " · " + string.Join(", ", actions.ToArray());
        }

        private void TestSelectedZoneActions()
        {
            CaptureCurrentZone();
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, "'" + selected.Name + "'의 동작을 지금 실행할까요?", "동작 테스트", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                TriggerZone(selected.Clone(), "동작 테스트");
            }
        }

        private void OpenConfigFolder()
        {
            if (!Directory.Exists(ConfigStore.ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigStore.ConfigDirectory);
            }
            Process.Start("explorer.exe", ConfigStore.ConfigDirectory);
        }

        private void ApplyTheme(Control root)
        {
            ApplyThemeToControl(root);

            foreach (Control child in root.Controls)
            {
                ApplyTheme(child);
            }
        }

        private void ApplyThemeToControl(Control control)
        {
            if (control is Label)
            {
                Label label = (Label)control;
                string tag = Convert.ToString(label.Tag);
                if (string.Equals(tag, "SectionHeader", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                if (string.Equals(tag, "SummaryBadge", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                label.BackColor = GetStableBackColor(label);
                if (string.Equals(tag, "Muted", StringComparison.OrdinalIgnoreCase))
                {
                    label.ForeColor = UiTextMuted;
                }
                else if (string.Equals(tag, "AccentTitle", StringComparison.OrdinalIgnoreCase))
                {
                    label.ForeColor = UiAccentDark;
                }
                else
                {
                    label.ForeColor = UiText;
                }
            }
            else if (control is TextBox)
            {
                TextBox textBox = (TextBox)control;
                textBox.BackColor = UiSurface;
                textBox.ForeColor = UiText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is CheckBox)
            {
                CheckBox checkBox = (CheckBox)control;
                checkBox.ForeColor = UiText;
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.BackColor = GetStableBackColor(checkBox);
            }
            else if (control is ComboBox)
            {
                ComboBox comboBox = (ComboBox)control;
                comboBox.BackColor = UiSurface;
                comboBox.ForeColor = UiText;
                comboBox.FlatStyle = FlatStyle.Flat;
            }
            else if (control is NumericUpDown)
            {
                NumericUpDown numeric = (NumericUpDown)control;
                numeric.BackColor = UiSurface;
                numeric.ForeColor = UiText;
                numeric.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ListBox)
            {
                ListBox listBox = (ListBox)control;
                listBox.BackColor = UiSurface;
                listBox.ForeColor = UiText;
                listBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is TabPage)
            {
                control.BackColor = UiSurface;
                control.ForeColor = UiText;
            }
            else if (control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel || control is SplitterPanel)
            {
                if (control.BackColor == SystemColors.Control)
                {
                    control.BackColor = UiBackground;
                }

                control.ForeColor = UiText;
            }
        }

        private void StyleLogBox()
        {
            if (_logText == null)
            {
                return;
            }

            _logText.BackColor = UiLogBackground;
            _logText.ForeColor = UiLogText;
            _logText.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _logText.BorderStyle = BorderStyle.None;
            _logText.Padding = new Padding(8);
        }

        private static Color GetStableBackColor(Control control)
        {
            Control parent = control == null ? null : control.Parent;
            if (parent != null && parent.BackColor != Color.Transparent)
            {
                return parent.BackColor;
            }

            return UiSurface;
        }

        private Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(74, 30);
            button.Padding = new Padding(10, 3, 10, 3);
            button.Margin = new Padding(4);
            button.Cursor = Cursors.Hand;
            StyleButton(button, ResolveButtonTone(text));
            return button;
        }

        private void SetFixedButtonSize(Button button, int width, int height)
        {
            if (button == null)
            {
                return;
            }

            Size measured = TextRenderer.MeasureText(button.Text ?? "", button.Font ?? Font);
            width = Math.Max(width, measured.Width + 34);
            button.AutoSize = false;
            button.Size = new Size(width, height);
            button.MinimumSize = new Size(width, height);
        }

        private ButtonTone ResolveButtonTone(string text)
        {
            string value = text ?? "";
            if (value.IndexOf("삭제", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("제거", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("운영 안함", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("운영 중지", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ButtonTone.Danger;
            }

            if (value.IndexOf("저장", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("운영하기", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ButtonTone.Primary;
            }

            return ButtonTone.Default;
        }

        private void StyleButton(Button button, ButtonTone tone)
        {
            if (button == null)
            {
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Font = new Font(Font.FontFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);

            if (tone == ButtonTone.Primary)
            {
                button.BackColor = UiAccent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = UiAccentDark;
                button.FlatAppearance.MouseOverBackColor = UiAccentDark;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(12, 68, 51);
            }
            else if (tone == ButtonTone.Danger)
            {
                button.BackColor = UiSurface;
                button.ForeColor = UiDanger;
                button.FlatAppearance.BorderColor = Color.FromArgb(222, 190, 184);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 237, 233);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(244, 217, 211);
            }
            else
            {
                button.BackColor = UiSurface;
                button.ForeColor = UiText;
                button.FlatAppearance.BorderColor = UiBorder;
                button.FlatAppearance.MouseOverBackColor = UiSurfaceMuted;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 231, 221);
            }
        }

        private ListBox CreateZoneListBox()
        {
            ListBox listBox = new ListBox();
            listBox.Dock = DockStyle.Fill;
            listBox.IntegralHeight = false;
            listBox.DrawMode = DrawMode.Normal;
            listBox.BorderStyle = BorderStyle.FixedSingle;
            listBox.BackColor = UiSurface;
            listBox.ForeColor = UiText;
            listBox.SelectedIndexChanged += ZoneListSelectedIndexChanged;
            return listBox;
        }

        private static void ClearChildControls(Control parent)
        {
            if (parent == null || parent.Controls.Count == 0)
            {
                return;
            }

            Control[] children = parent.Controls.Cast<Control>().ToArray();
            parent.Controls.Clear();
            foreach (Control child in children)
            {
                child.Dispose();
            }
        }

        private Label CreateInlineLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = UiTextMuted;
            label.Margin = new Padding(8, 9, 4, 4);
            label.Tag = "Muted";
            return label;
        }

        private Size GetChipSize(string text, Font font)
        {
            Size measured = TextRenderer.MeasureText(text ?? "", font ?? Font);
            int width = Math.Max(116, Math.Min(240, measured.Width + 28));
            return new Size(width, 30);
        }

        private FlowLayoutPanel CreateChipPanel(int height)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            panel.WrapContents = true;
            panel.Height = height;
            panel.BackColor = UiSurfaceMuted;
            panel.Padding = new Padding(7, 6, 7, 5);
            panel.Margin = new Padding(0, 0, 0, 6);
            return panel;
        }

        private Button CreateValueChip(string value, string text, bool selected)
        {
            Button chip = new Button();
            chip.Text = text;
            chip.Tag = value;
            chip.AutoSize = false;
            chip.Size = GetValueChipSize(text, chip.Font);
            chip.Margin = new Padding(4, 3, 4, 3);
            chip.Padding = new Padding(8, 2, 8, 2);
            chip.TextAlign = ContentAlignment.MiddleCenter;
            chip.Cursor = Cursors.Hand;
            StyleChip(chip, selected);
            if (_toolTip != null)
            {
                _toolTip.SetToolTip(chip, value);
            }
            return chip;
        }

        private Size GetValueChipSize(string text, Font font)
        {
            Size measured = TextRenderer.MeasureText(text ?? "", font ?? Font);
            int width = Math.Max(104, Math.Min(360, measured.Width + 30));
            return new Size(width, 30);
        }

        private void StyleChip(Button chip, bool selected)
        {
            chip.FlatStyle = FlatStyle.Flat;
            chip.UseVisualStyleBackColor = false;
            if (selected)
            {
                chip.BackColor = UiAccent;
                chip.ForeColor = Color.White;
                chip.FlatAppearance.BorderColor = UiAccentDark;
                chip.FlatAppearance.MouseOverBackColor = UiAccentDark;
                chip.FlatAppearance.MouseDownBackColor = Color.FromArgb(12, 68, 51);
            }
            else
            {
                chip.BackColor = UiSurface;
                chip.ForeColor = UiText;
                chip.FlatAppearance.BorderColor = UiBorder;
                chip.FlatAppearance.MouseOverBackColor = UiSurfaceMuted;
                chip.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 231, 221);
            }
        }

        private string ShortenUrlForChip(string value)
        {
            string url = value ?? "";
            try
            {
                Uri uri = new Uri(url);
                string host = uri.Host.Replace("www.", "");
                string path = uri.AbsolutePath.Trim('/');
                if (path.Length == 0)
                {
                    return host;
                }

                string first = path.Split('/')[0];
                return host + " · " + ShortenText(first, 18);
            }
            catch
            {
                return ShortenText(url, 34);
            }
        }

        private static string ShortenText(string value, int maxLength)
        {
            string text = (value ?? "").Trim();
            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, Math.Max(1, maxLength - 1)) + "…";
        }

        private void StyleToggleButton(CheckBox toggle)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.FlatStyle = FlatStyle.Flat;
            toggle.UseVisualStyleBackColor = false;
            toggle.Padding = new Padding(8, 3, 8, 3);
            toggle.MinimumSize = new Size(72, 28);
            toggle.TextAlign = ContentAlignment.MiddleCenter;
            toggle.Cursor = Cursors.Hand;

            if (toggle.Checked)
            {
                toggle.BackColor = UiAccent;
                toggle.ForeColor = Color.White;
                toggle.FlatAppearance.BorderColor = UiAccentDark;
                toggle.FlatAppearance.MouseOverBackColor = UiAccentDark;
                toggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(12, 68, 51);
            }
            else
            {
                toggle.BackColor = UiSurface;
                toggle.ForeColor = UiText;
                toggle.FlatAppearance.BorderColor = UiBorder;
                toggle.FlatAppearance.MouseOverBackColor = UiSurfaceMuted;
                toggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 231, 221);
            }
        }

        private void StyleSwitchToggle(CheckBox toggle, string checkedText, string uncheckedText)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.Appearance = Appearance.Button;
            toggle.AutoSize = false;
            toggle.Text = toggle.Checked ? checkedText : uncheckedText;
            StyleToggleButton(toggle);
        }
    }
}
