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
            Font summaryTitleFont = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            int titleRowHeight = GetTextRowHeight(summaryTitleFont, 8, 30);
            int metaRowHeight = GetTextRowHeight(Font, 8, 28);
            int badgeRowHeight = GetTextRowHeight(Font, 10, 30);
            int textStackHeight = titleRowHeight + metaRowHeight + badgeRowHeight;
            int toolbarRowHeight = 44;
            int summaryHeight = 16 + textStackHeight + toolbarRowHeight;

            TableLayoutPanel summary = new TableLayoutPanel();
            summary.Dock = DockStyle.Top;
            summary.AutoSize = false;
            summary.Height = summaryHeight;
            summary.MinimumSize = new Size(0, summaryHeight);
            summary.MaximumSize = new Size(0, summaryHeight);
            summary.BackColor = UiSurfaceMuted;
            summary.Padding = new Padding(12, 8, 12, 8);
            summary.Margin = new Padding(0, 0, 0, 8);
            summary.ColumnCount = 1;
            summary.RowCount = 2;
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, textStackHeight));
            summary.RowStyles.Add(new RowStyle(SizeType.Absolute, toolbarRowHeight));

            TableLayoutPanel textStack = new TableLayoutPanel();
            textStack.Dock = DockStyle.Fill;
            textStack.AutoSize = false;
            textStack.ColumnCount = 1;
            textStack.RowCount = 3;
            textStack.Margin = new Padding(0);
            textStack.RowStyles.Add(new RowStyle(SizeType.Absolute, titleRowHeight));
            textStack.RowStyles.Add(new RowStyle(SizeType.Absolute, metaRowHeight));
            textStack.RowStyles.Add(new RowStyle(SizeType.Absolute, badgeRowHeight));

            _selectedZoneSummaryLabel = new Label();
            _selectedZoneSummaryLabel.Text = "위치를 선택하세요";
            _selectedZoneSummaryLabel.AutoSize = false;
            _selectedZoneSummaryLabel.Dock = DockStyle.Fill;
            _selectedZoneSummaryLabel.AutoEllipsis = true;
            _selectedZoneSummaryLabel.Font = summaryTitleFont;
            _selectedZoneSummaryLabel.ForeColor = UiAccentDark;
            _selectedZoneSummaryLabel.Margin = new Padding(0, 0, 0, 3);
            _selectedZoneSummaryLabel.Tag = "AccentTitle";
            textStack.Controls.Add(_selectedZoneSummaryLabel, 0, 0);

            _selectedZoneMetaLabel = new Label();
            _selectedZoneMetaLabel.Text = "등록된 위치를 선택하면 감지 조건과 실행 동작이 여기에 요약됩니다.";
            _selectedZoneMetaLabel.AutoSize = false;
            _selectedZoneMetaLabel.Dock = DockStyle.Fill;
            _selectedZoneMetaLabel.AutoEllipsis = true;
            _selectedZoneMetaLabel.ForeColor = UiTextMuted;
            _selectedZoneMetaLabel.Margin = new Padding(0, 0, 0, 2);
            _selectedZoneMetaLabel.Tag = "Muted";
            textStack.Controls.Add(_selectedZoneMetaLabel, 0, 1);

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.Dock = DockStyle.Fill;
            badges.AutoSize = false;
            badges.WrapContents = false;
            badges.Margin = new Padding(0);
            badges.BackColor = UiSurfaceMuted;
            _summaryOperatingBadge = CreateSummaryBadge("미선택");
            _summaryMatchBadge = CreateSummaryBadge("대기");
            _summaryModeBadge = CreateSummaryBadge("감지 조건 없음");
            badges.Controls.Add(_summaryOperatingBadge);
            badges.Controls.Add(_summaryMatchBadge);
            badges.Controls.Add(_summaryModeBadge);
            textStack.Controls.Add(badges, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.AutoSize = false;
            buttons.Height = toolbarRowHeight - 4;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Margin = new Padding(0, 8, 0, 0);
            buttons.Padding = new Padding(0);
            buttons.BackColor = UiSurfaceMuted;

            _saveSummaryButton = CreateSummaryIconButton("\uE74E", "저장");
            _saveSummaryButton.Click += delegate { SaveFromUi(); };

            _refreshSummaryButton = CreateSummaryIconButton("\uE72C", "화면 갱신");
            _refreshSummaryButton.Click += delegate { RefreshStatusAndLogsNow(); };

            _testConditionSummaryButton = CreateSummaryIconButton("\uE9D9", "조건 테스트");
            _testConditionSummaryButton.Click += delegate { TestSelectedZoneCondition(); };

            _testActionsSummaryButton = CreateSummaryIconButton("\uE768", "동작 테스트");
            _testActionsSummaryButton.Click += delegate { TestSelectedZoneActions(); };

            _openConfigSummaryButton = CreateSummaryIconButton("\uE8B7", "설정 폴더");
            _openConfigSummaryButton.Click += delegate { OpenConfigFolder(); };

            _operateSummaryButton = CreateSummaryIconButton("\uE73E", "운영하기");
            _operateSummaryButton.Click += delegate { SetSelectedZoneOperating(true); };

            _stopOperatingSummaryButton = CreateSummaryIconButton("\uE71A", "운영 중지");
            _stopOperatingSummaryButton.Click += delegate { ConfirmStopSelectedZoneOperating(); };

            FlowLayoutPanel saveGroup = CreateSummaryButtonGroup();
            saveGroup.Controls.Add(_saveSummaryButton);
            saveGroup.Controls.Add(_refreshSummaryButton);
            buttons.Controls.Add(saveGroup);

            FlowLayoutPanel testGroup = CreateSummaryButtonGroup();
            testGroup.Controls.Add(_testConditionSummaryButton);
            testGroup.Controls.Add(_testActionsSummaryButton);
            buttons.Controls.Add(testGroup);

            FlowLayoutPanel configGroup = CreateSummaryButtonGroup();
            configGroup.Controls.Add(_openConfigSummaryButton);
            buttons.Controls.Add(configGroup);

            FlowLayoutPanel operatingGroup = CreateSummaryButtonGroup();
            operatingGroup.Margin = new Padding(8, 0, 0, 0);
            operatingGroup.Controls.Add(_operateSummaryButton);
            operatingGroup.Controls.Add(_stopOperatingSummaryButton);
            buttons.Controls.Add(operatingGroup);

            summary.Controls.Add(textStack, 0, 0);
            summary.Controls.Add(buttons, 0, 1);
            return summary;
        }

        private static int GetTextRowHeight(Font font, int extraPixels, int minimum)
        {
            Size measured = TextRenderer.MeasureText("위치 자동 실행", font);
            return Math.Max(minimum, measured.Height + extraPixels);
        }

        private FlowLayoutPanel CreateSummaryButtonGroup()
        {
            FlowLayoutPanel group = new FlowLayoutPanel();
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.WrapContents = false;
            group.FlowDirection = FlowDirection.LeftToRight;
            group.Margin = new Padding(0, 0, 8, 0);
            group.Padding = new Padding(0);
            group.BackColor = UiSurfaceMuted;
            return group;
        }

        private Button CreateSummaryIconButton(string glyph, string tooltip)
        {
            Button button = new Button();
            button.Text = glyph;
            button.Font = new Font("Segoe MDL2 Assets", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Width = 36;
            button.Height = 34;
            button.Margin = new Padding(0, 0, 4, 4);
            button.Padding = new Padding(0);
            button.FlatStyle = FlatStyle.Standard;
            button.UseVisualStyleBackColor = true;
            button.Cursor = Cursors.Hand;
            button.ForeColor = UiText;
            button.AccessibleName = tooltip;
            button.AccessibleDescription = tooltip;
            EnsureToolTip().SetToolTip(button, tooltip);
            return button;
        }

        private ToolTip EnsureToolTip()
        {
            if (_toolTip != null)
            {
                return _toolTip;
            }

            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 6000;
            _toolTip.InitialDelay = 350;
            _toolTip.ReshowDelay = 100;
            _toolTip.ShowAlways = true;
            return _toolTip;
        }

        private void ConfirmStopSelectedZoneOperating()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                SetSelectedZoneOperating(false);
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                selected.Name + " 위치 운영을 중지할까요? 자동 실행과 앱 감시가 멈출 수 있습니다.",
                "운영 중지 확인",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result == DialogResult.Yes)
            {
                SetSelectedZoneOperating(false);
            }
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
            label.Height = GetTextRowHeight(Font, 6, 28);
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
                SetSummaryBadge(_summaryModeBadge, "감지 조건 없음", UiSurface, UiTextMuted);
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
                return "감지 조건 없음";
            }

            List<string> modes = new List<string>();
            if (zone.UseCoordinates)
            {
                modes.Add("좌표 " + zone.RadiusMeters + "m");
            }

            int wifiCount = zone.NearbySsids == null ? 0 : zone.NearbySsids.Count(s => !string.IsNullOrWhiteSpace(s));
            if (zone.UseWifiCondition.GetValueOrDefault(false))
            {
                modes.Add(wifiCount == 0 ? "Wi-Fi 미설정" : "Wi-Fi " + wifiCount + "개");
            }

            return modes.Count == 0 ? "감지 조건 없음" : string.Join(" 또는 ", modes.ToArray());
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
                actions.Add("앱 감시 " + appWatchCount + "개 · 앱 확인 " + BuildShortestAppWatchIntervalText(zone));
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
                schedules.Add("조건 진입 시 실행");
            }
            if (zone.MonitoringEnabled.GetValueOrDefault(false) || appWatchCount > 0)
            {
                schedules.Add("조건 확인 " + Math.Max(5, zone.ScanIntervalSeconds) + "초");
            }

            string prefix = zone.Enabled ? "자동 실행 준비됨" : "운영 중지됨";
            if (schedules.Count > 0)
            {
                prefix += " · " + string.Join("/", schedules.ToArray());
            }
            return actions.Count == 0 ? prefix + " · 실행 동작 없음" : prefix + " · " + string.Join(", ", actions.ToArray());
        }

        private static string BuildShortestAppWatchIntervalText(ZoneRule zone)
        {
            if (zone == null || zone.AppWatchItems == null)
            {
                return "5분";
            }

            AppWatchItem shortest = zone.AppWatchItems
                .Where(item => item != null && item.Enabled)
                .OrderBy(item => string.Equals(item.IntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(1, item.IntervalValue) * 60
                    : Math.Max(1, item.IntervalValue))
                .FirstOrDefault();
            if (shortest == null)
            {
                return "5분";
            }

            bool hours = string.Equals(shortest.IntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase);
            return Math.Max(1, shortest.IntervalValue) + (hours ? "시간" : "분");
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

        private void OpenSettingsDialog()
        {
            using (SettingsForm dialog = new SettingsForm(
                _startupCheck.Checked,
                _config.StartMinimized,
                _config.PreventSleepWhileAutomationActive,
                _config.TrayIconEnabled))
            {
                DialogResult result = dialog.ShowDialog(this);
                if (result != DialogResult.OK)
                {
                    return;
                }

                _startupCheck.Checked = dialog.StartupEnabled;
                _startMinimizedCheck.Checked = dialog.StartMinimized;
                _config.StartMinimized = dialog.StartMinimized;
                _config.PreventSleepWhileAutomationActive = dialog.PreventSleepWhileAutomationActive;
                _config.TrayIconEnabled = dialog.TrayIconEnabled;
                SaveFromUi();
            }
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
                checkBox.FlatStyle = FlatStyle.Standard;
                checkBox.UseVisualStyleBackColor = true;
            }
            else if (control is ComboBox)
            {
                ComboBox comboBox = (ComboBox)control;
                comboBox.BackColor = UiSurface;
                comboBox.ForeColor = UiText;
                comboBox.FlatStyle = FlatStyle.Standard;
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
            width = Math.Max(width, measured.Width + 48);
            height = Math.Max(height, 34);
            button.AutoSize = false;
            button.Size = new Size(width, height);
            button.MinimumSize = new Size(width, height);
        }

        private void DockSidebarButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.Dock = DockStyle.Fill;
            button.AutoSize = false;
            button.MinimumSize = new Size(0, 36);
            button.Height = 36;
            button.Margin = new Padding(4, 4, 4, 4);
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
            button.FlatAppearance.BorderSize = 1;

            if (tone == ButtonTone.Primary)
            {
                button.BackColor = UiAccent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = UiAccentDark;
                return;
            }

            if (tone == ButtonTone.Danger)
            {
                button.BackColor = Color.FromArgb(255, 245, 244);
                button.ForeColor = UiDanger;
                button.FlatAppearance.BorderColor = UiDanger;
                return;
            }

            button.BackColor = UiSurface;
            button.ForeColor = UiText;
            button.FlatAppearance.BorderColor = UiBorder;
        }

        private ListBox CreateZoneListBox()
        {
            ListBox listBox = new ListBox();
            listBox.Dock = DockStyle.Fill;
            listBox.IntegralHeight = false;
            listBox.DrawMode = DrawMode.Normal;
            listBox.BorderStyle = BorderStyle.FixedSingle;
            listBox.HorizontalScrollbar = true;
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

        private Label CreateCoordinateLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.TextAlign = ContentAlignment.MiddleRight;
            label.ForeColor = UiTextMuted;
            label.Margin = new Padding(0, 4, 6, 4);
            label.Tag = "Muted";
            return label;
        }

        private Label CreateStatusValueLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.AutoSize = true;
            label.MaximumSize = new Size(760, 0);
            label.MinimumSize = new Size(0, 24);
            label.Padding = new Padding(0, 3, 0, 3);
            label.ForeColor = UiText;
            label.Tag = "StatusValue";
            return label;
        }

        private Size GetChipSize(string text, Font font)
        {
            Size measured = TextRenderer.MeasureText(text ?? "", font ?? Font);
            int width = Math.Max(116, Math.Min(240, measured.Width + 28));
            int height = GetTextRowHeight(font ?? Font, 8, 32);
            return new Size(width, height);
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
            int height = GetTextRowHeight(font ?? Font, 8, 32);
            return new Size(width, height);
        }

        private void StyleChip(Button chip, bool selected)
        {
            if (selected)
            {
                chip.Text = "선택됨: " + chip.Text;
            }

            chip.FlatStyle = FlatStyle.Standard;
            chip.UseVisualStyleBackColor = true;
            chip.ForeColor = SystemColors.ControlText;
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

            toggle.Padding = new Padding(8, 3, 8, 3);
            toggle.MinimumSize = new Size(72, 28);
            toggle.TextAlign = ContentAlignment.MiddleCenter;
            toggle.Cursor = Cursors.Hand;
            toggle.FlatStyle = FlatStyle.Standard;
            toggle.UseVisualStyleBackColor = true;
            toggle.ForeColor = SystemColors.ControlText;
        }

        private void StyleSwitchToggle(CheckBox toggle, string checkedText, string uncheckedText)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.Appearance = Appearance.Normal;
            toggle.AutoSize = true;
            toggle.Text = toggle.Checked ? checkedText : uncheckedText;
            StyleToggleButton(toggle);
        }
    }
}
