using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class SettingsForm : Form
    {
        private readonly CheckBox _startupCheck;
        private readonly CheckBox _startMinimizedCheck;
        private readonly CheckBox _preventSleepCheck;
        private readonly CheckBox _brightnessScheduleCheck;
        private readonly NumericUpDown _defaultBrightnessInput;
        private readonly DataGridView _brightnessGrid;
        private readonly TextBox _brightnessVerifyTimeText;
        private readonly Label _brightnessVerifyResultLabel;
        private readonly Label _nightLightStatusLabel;
        private readonly CheckBox _trayIconCheck;
        private readonly System.Windows.Forms.Timer _backgroundStatusTimer;
        private Label _backgroundHealthValue;
        private Label _backgroundProcessValue;
        private Label _backgroundUpdatedValue;
        private Label _backgroundActiveZonesValue;
        private Label _backgroundWifiValue;
        private Label _backgroundLocationValue;
        private Label _backgroundLastEventValue;
        private Label _backgroundLastActionValue;
        private Label _backgroundLastAppWatchValue;

        public bool StartupEnabled
        {
            get { return _startupCheck.Checked; }
        }

        public bool StartMinimized
        {
            get { return _startMinimizedCheck.Checked; }
        }

        public bool PreventSleepWhileAutomationActive
        {
            get { return _preventSleepCheck.Checked; }
        }

        public bool BrightnessScheduleEnabled
        {
            get { return _brightnessScheduleCheck.Checked; }
        }

        public int DefaultBrightnessPercent
        {
            get { return Convert.ToInt32(_defaultBrightnessInput.Value); }
        }

        public List<BrightnessPeriod> BrightnessPeriods
        {
            get { return ReadBrightnessPeriods(); }
        }

        public bool TrayIconEnabled
        {
            get { return _trayIconCheck.Checked; }
        }

        public SettingsForm(
            bool startupEnabled,
            bool startMinimized,
            bool preventSleepWhileAutomationActive,
            bool brightnessScheduleEnabled,
            int defaultBrightnessPercent,
            List<BrightnessPeriod> brightnessPeriods,
            bool trayIconEnabled)
        {
            Text = "설정";
            StartPosition = FormStartPosition.CenterParent;
            Rectangle workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            MinimumSize = new Size(640, 560);
            Size = new Size(
                Math.Min(820, Math.Max(640, workingArea.Width - 48)),
                Math.Min(760, Math.Max(560, workingArea.Height - 48)));
            MaximizeBox = true;
            MinimizeBox = false;
            ShowIcon = false;
            BackColor = Color.FromArgb(246, 247, 242);
            ForeColor = Color.FromArgb(35, 45, 47);
            Font = new Font("Malgun Gothic", 9.25F, FontStyle.Regular, GraphicsUnit.Point);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            TabControl tabs = CreateSettingsTabs();
            root.Controls.Add(tabs, 0, 0);

            TabPage generalTab = new TabPage("일반 설정");
            generalTab.BackColor = BackColor;
            generalTab.Padding = new Padding(0);
            tabs.TabPages.Add(generalTab);

            TabPage backgroundTab = new TabPage("백그라운드 상태");
            backgroundTab.BackColor = BackColor;
            backgroundTab.Padding = new Padding(0);
            tabs.TabPages.Add(backgroundTab);

            Panel contentPanel = new Panel();
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.AutoScroll = true;
            contentPanel.Margin = new Padding(0);
            generalTab.Controls.Add(contentPanel);

            TableLayoutPanel content = new TableLayoutPanel();
            content.AutoSize = true;
            content.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            content.ColumnCount = 1;
            content.RowCount = 0;
            content.Dock = DockStyle.Top;
            content.Margin = new Padding(0);
            contentPanel.Controls.Add(content);
            contentPanel.Resize += delegate { content.Width = Math.Max(0, contentPanel.ClientSize.Width - 1); };
            content.Width = Math.Max(0, contentPanel.ClientSize.Width - 1);

            Label title = new Label();
            title.Text = "앱 설정";
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(20, 91, 69);
            title.Margin = new Padding(0, 0, 0, 12);
            content.Controls.Add(title, 0, content.RowCount++);

            TableLayoutPanel startupPanel = CreateSection("시작 동작");
            _startupCheck = new CheckBox();
            _startupCheck.Text = "Windows 시작 시 실행";
            _startupCheck.Checked = startupEnabled;
            _startupCheck.AutoSize = true;
            _startupCheck.Margin = new Padding(0, 4, 0, 7);
            startupPanel.Controls.Add(_startupCheck, 0, startupPanel.RowCount++);

            _startMinimizedCheck = new CheckBox();
            _startMinimizedCheck.Text = "최소화로 시작";
            _startMinimizedCheck.Checked = startMinimized;
            _startMinimizedCheck.AutoSize = true;
            _startMinimizedCheck.Margin = new Padding(0, 0, 0, 6);
            startupPanel.Controls.Add(_startMinimizedCheck, 0, startupPanel.RowCount++);
            content.Controls.Add(startupPanel, 0, content.RowCount++);

            TableLayoutPanel powerPanel = CreateSection("전원");
            _preventSleepCheck = new CheckBox();
            _preventSleepCheck.Text = "자동 감시 중 Windows 자동 절전 방지";
            _preventSleepCheck.Checked = preventSleepWhileAutomationActive;
            _preventSleepCheck.AutoSize = true;
            _preventSleepCheck.Margin = new Padding(0, 4, 0, 4);
            powerPanel.Controls.Add(_preventSleepCheck, 0, powerPanel.RowCount++);
            powerPanel.Controls.Add(CreateNoteLine("자동 절전만 방지합니다. 직접 전원 동작은 그대로 진행됩니다."), 0, powerPanel.RowCount++);
            content.Controls.Add(powerPanel, 0, content.RowCount++);

            TableLayoutPanel brightnessPanel = CreateSection("화면 밝기");
            _brightnessScheduleCheck = new CheckBox();
            _brightnessScheduleCheck.Text = "시간대별 화면 밝기 조정";
            _brightnessScheduleCheck.Checked = brightnessScheduleEnabled;
            _brightnessScheduleCheck.AutoSize = true;
            _brightnessScheduleCheck.Margin = new Padding(0, 4, 0, 7);
            _brightnessScheduleCheck.CheckedChanged += delegate { UpdateBrightnessControlsEnabled(); };
            brightnessPanel.Controls.Add(_brightnessScheduleCheck, 0, brightnessPanel.RowCount++);

            TableLayoutPanel defaultBrightnessRow = new TableLayoutPanel();
            defaultBrightnessRow.AutoSize = true;
            defaultBrightnessRow.ColumnCount = 3;
            defaultBrightnessRow.Margin = new Padding(21, 0, 0, 8);
            defaultBrightnessRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            defaultBrightnessRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            defaultBrightnessRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Label defaultBrightnessLabel = new Label();
            defaultBrightnessLabel.Text = "기본 밝기";
            defaultBrightnessLabel.AutoSize = true;
            defaultBrightnessLabel.Margin = new Padding(0, 5, 8, 0);
            defaultBrightnessRow.Controls.Add(defaultBrightnessLabel, 0, 0);
            _defaultBrightnessInput = new NumericUpDown();
            _defaultBrightnessInput.Minimum = 1;
            _defaultBrightnessInput.Maximum = 100;
            _defaultBrightnessInput.Value = Math.Max(1, Math.Min(100, defaultBrightnessPercent <= 0 ? 70 : defaultBrightnessPercent));
            _defaultBrightnessInput.Width = 76;
            _defaultBrightnessInput.Margin = new Padding(0, 0, 6, 0);
            defaultBrightnessRow.Controls.Add(_defaultBrightnessInput, 1, 0);
            Label percentLabel = new Label();
            percentLabel.Text = "%";
            percentLabel.AutoSize = true;
            percentLabel.Margin = new Padding(0, 5, 0, 0);
            defaultBrightnessRow.Controls.Add(percentLabel, 2, 0);
            brightnessPanel.Controls.Add(defaultBrightnessRow, 0, brightnessPanel.RowCount++);

            _brightnessGrid = CreateBrightnessGrid();
            List<BrightnessPeriod> initialPeriods = brightnessPeriods ?? new List<BrightnessPeriod>();
            foreach (BrightnessPeriod period in initialPeriods)
            {
                AddBrightnessRow(period);
            }
            brightnessPanel.Controls.Add(_brightnessGrid, 0, brightnessPanel.RowCount++);

            FlowLayoutPanel brightnessButtons = new FlowLayoutPanel();
            brightnessButtons.AutoSize = true;
            brightnessButtons.WrapContents = true;
            brightnessButtons.Margin = new Padding(21, 8, 0, 0);
            Button addBrightnessButton = CreateButton("시간 추가");
            addBrightnessButton.Click += delegate { AddBrightnessRow(BrightnessPeriod.CreateDefault()); };
            brightnessButtons.Controls.Add(addBrightnessButton);
            Button removeBrightnessButton = CreateButton("선택 삭제");
            removeBrightnessButton.Click += delegate { RemoveSelectedBrightnessRows(); };
            brightnessButtons.Controls.Add(removeBrightnessButton);
            brightnessPanel.Controls.Add(brightnessButtons, 0, brightnessPanel.RowCount++);
            brightnessPanel.Controls.Add(CreateNoteLine("각 시각에 들어올 때 한 번만 밝기를 바꿉니다. 이후 직접 바꾼 밝기는 다음 시각 전까지 덮어쓰지 않습니다."), 0, brightnessPanel.RowCount++);

            TableLayoutPanel verifyRow = new TableLayoutPanel();
            verifyRow.AutoSize = true;
            verifyRow.ColumnCount = 4;
            verifyRow.Margin = new Padding(21, 8, 0, 6);
            verifyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            verifyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            verifyRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            verifyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Label verifyLabel = new Label();
            verifyLabel.Text = "시간 검증";
            verifyLabel.AutoSize = true;
            verifyLabel.Margin = new Padding(0, 6, 8, 0);
            verifyRow.Controls.Add(verifyLabel, 0, 0);
            _brightnessVerifyTimeText = new TextBox();
            _brightnessVerifyTimeText.Text = DateTime.Now.ToString("HH:mm");
            _brightnessVerifyTimeText.Width = 70;
            _brightnessVerifyTimeText.Margin = new Padding(0, 2, 6, 0);
            verifyRow.Controls.Add(_brightnessVerifyTimeText, 1, 0);
            Button verifyButton = CreateButton("확인");
            verifyButton.Click += delegate { VerifyBrightnessTime(); };
            verifyRow.Controls.Add(verifyButton, 2, 0);
            brightnessPanel.Controls.Add(verifyRow, 0, brightnessPanel.RowCount++);

            _brightnessVerifyResultLabel = new Label();
            _brightnessVerifyResultLabel.AutoSize = true;
            _brightnessVerifyResultLabel.MaximumSize = new Size(560, 0);
            _brightnessVerifyResultLabel.ForeColor = Color.FromArgb(35, 45, 47);
            _brightnessVerifyResultLabel.Margin = new Padding(21, 0, 0, 8);
            brightnessPanel.Controls.Add(_brightnessVerifyResultLabel, 0, brightnessPanel.RowCount++);

            _nightLightStatusLabel = new Label();
            _nightLightStatusLabel.Text = "야간모드: " + NightLightController.GetStatusSummary();
            _nightLightStatusLabel.AutoSize = true;
            _nightLightStatusLabel.MaximumSize = new Size(560, 0);
            _nightLightStatusLabel.ForeColor = Color.FromArgb(97, 111, 103);
            _nightLightStatusLabel.Margin = new Padding(21, 0, 0, 8);
            brightnessPanel.Controls.Add(_nightLightStatusLabel, 0, brightnessPanel.RowCount++);

            content.Controls.Add(brightnessPanel, 0, content.RowCount++);
            UpdateBrightnessControlsEnabled();

            TableLayoutPanel trayPanel = CreateSection("트레이");
            _trayIconCheck = new CheckBox();
            _trayIconCheck.Text = "설정 화면 트레이 아이콘 표시";
            _trayIconCheck.Checked = trayIconEnabled;
            _trayIconCheck.AutoSize = true;
            _trayIconCheck.Margin = new Padding(0, 4, 0, 4);
            trayPanel.Controls.Add(_trayIconCheck, 0, trayPanel.RowCount++);
            trayPanel.Controls.Add(CreateNoteLine("상태 확인과 설정 열기만 제공합니다. 풍선 알림은 사용하지 않습니다."), 0, trayPanel.RowCount++);
            content.Controls.Add(trayPanel, 0, content.RowCount++);

            TableLayoutPanel diagnosticsPanel = CreateSection("진단");
            diagnosticsPanel.Controls.Add(CreateStatusLine("자동 시작", StartupManager.GetStartupStatusSummary()), 0, diagnosticsPanel.RowCount++);
            diagnosticsPanel.Controls.Add(CreateStatusLine("화면 밝기", brightnessScheduleEnabled ? "시간대별 조정" : "꺼짐"), 0, diagnosticsPanel.RowCount++);
            diagnosticsPanel.Controls.Add(CreateStatusLine("야간모드", NightLightController.GetStatusSummary()), 0, diagnosticsPanel.RowCount++);
            diagnosticsPanel.Controls.Add(CreateStatusLine("트레이", "설정에서 선택 가능"), 0, diagnosticsPanel.RowCount++);
            diagnosticsPanel.Controls.Add(CreateStatusLine("설정 파일", "config.json"), 0, diagnosticsPanel.RowCount++);
            diagnosticsPanel.Controls.Add(CreateStatusLine("로그 파일", "activity.log"), 0, diagnosticsPanel.RowCount++);

            FlowLayoutPanel folderButtons = new FlowLayoutPanel();
            folderButtons.AutoSize = true;
            folderButtons.WrapContents = true;
            folderButtons.Margin = new Padding(0, 10, 0, 0);
            Button configFolderButton = CreateButton("설정 폴더");
            configFolderButton.Click += delegate { OpenFolder(ConfigStore.ConfigDirectory); };
            folderButtons.Controls.Add(configFolderButton);
            Button logFileButton = CreateButton("로그 파일");
            logFileButton.Click += delegate { OpenFile(DiagnosticsLog.ActivityLogPath); };
            folderButtons.Controls.Add(logFileButton);
            Button dumpFolderButton = CreateButton("덤프 폴더");
            dumpFolderButton.Click += delegate { OpenFolder(Path.Combine(ConfigStore.ConfigDirectory, "dumps")); };
            folderButtons.Controls.Add(dumpFolderButton);
            diagnosticsPanel.Controls.Add(folderButtons, 0, diagnosticsPanel.RowCount++);
            content.Controls.Add(diagnosticsPanel, 0, content.RowCount++);

            BuildBackgroundStatusTab(backgroundTab);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            buttons.Margin = new Padding(0, 12, 0, 0);
            Button saveButton = CreateButton("저장");
            saveButton.Click += delegate
            {
                if (!ValidateBrightnessRows())
                {
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };
            Button cancelButton = CreateButton("취소");
            cancelButton.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            root.Controls.Add(buttons, 0, 1);

            AcceptButton = saveButton;
            CancelButton = cancelButton;

            _backgroundStatusTimer = new System.Windows.Forms.Timer();
            _backgroundStatusTimer.Interval = 5000;
            _backgroundStatusTimer.Tick += delegate { RefreshBackgroundStatus(); };
            _backgroundStatusTimer.Start();
            RefreshBackgroundStatus();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_backgroundStatusTimer != null)
            {
                _backgroundStatusTimer.Stop();
                _backgroundStatusTimer.Dispose();
            }

            base.OnFormClosed(e);
        }

        private DataGridView CreateBrightnessGrid()
        {
            DataGridView grid = new DataGridView();
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = true;
            grid.AllowUserToResizeRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.FromArgb(253, 253, 249);
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.Dock = DockStyle.Top;
            grid.Height = 150;
            grid.Margin = new Padding(21, 0, 0, 0);
            grid.MultiSelect = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            DataGridViewCheckBoxColumn enabledColumn = new DataGridViewCheckBoxColumn();
            enabledColumn.Name = "Enabled";
            enabledColumn.HeaderText = "사용";
            enabledColumn.FillWeight = 18;
            grid.Columns.Add(enabledColumn);

            DataGridViewTextBoxColumn startColumn = new DataGridViewTextBoxColumn();
            startColumn.Name = "StartTime";
            startColumn.HeaderText = "시작";
            startColumn.FillWeight = 36;
            grid.Columns.Add(startColumn);

            DataGridViewTextBoxColumn brightnessColumn = new DataGridViewTextBoxColumn();
            brightnessColumn.Name = "Brightness";
            brightnessColumn.HeaderText = "밝기(%)";
            brightnessColumn.FillWeight = 36;
            grid.Columns.Add(brightnessColumn);

            DataGridViewComboBoxColumn nightLightColumn = new DataGridViewComboBoxColumn();
            nightLightColumn.Name = "NightLight";
            nightLightColumn.HeaderText = "야간모드";
            nightLightColumn.FillWeight = 38;
            nightLightColumn.Items.AddRange(new object[] { "유지", "켜기", "끄기" });
            grid.Columns.Add(nightLightColumn);

            return grid;
        }

        private void AddBrightnessRow(BrightnessPeriod period)
        {
            if (_brightnessGrid == null)
            {
                return;
            }

            BrightnessPeriod value = period == null ? BrightnessPeriod.CreateDefault() : period.Clone();
            value.Normalize();
            _brightnessGrid.Rows.Add(
                value.Enabled,
                BrightnessSchedule.FormatStartTime(value.StartMinuteOfDay),
                value.BrightnessPercent.ToString(CultureInfo.InvariantCulture),
                BrightnessSchedule.FormatNightLightAction(value.NightLightAction));
        }

        private void RemoveSelectedBrightnessRows()
        {
            if (_brightnessGrid == null)
            {
                return;
            }

            foreach (DataGridViewRow row in _brightnessGrid.SelectedRows.Cast<DataGridViewRow>().Where(row => !row.IsNewRow).ToList())
            {
                _brightnessGrid.Rows.Remove(row);
            }
        }

        private void UpdateBrightnessControlsEnabled()
        {
            bool enabled = _brightnessScheduleCheck != null && _brightnessScheduleCheck.Checked;
            if (_defaultBrightnessInput != null)
            {
                _defaultBrightnessInput.Enabled = enabled;
            }
            if (_brightnessGrid != null)
            {
                _brightnessGrid.Enabled = enabled;
            }
            if (_brightnessVerifyTimeText != null)
            {
                _brightnessVerifyTimeText.Enabled = enabled;
            }
        }

        private bool ValidateBrightnessRows()
        {
            if (_brightnessGrid == null || !_brightnessScheduleCheck.Checked)
            {
                return true;
            }

            HashSet<int> startTimes = new HashSet<int>();
            foreach (DataGridViewRow row in _brightnessGrid.Rows)
            {
                if (row.IsNewRow || IsBrightnessRowEmpty(row))
                {
                    continue;
                }

                int startMinute;
                string startText = Convert.ToString(row.Cells["StartTime"].Value);
                if (!BrightnessSchedule.TryParseStartTime(startText, out startMinute))
                {
                    MessageBox.Show(this, "밝기 시작 시각은 HH:mm 형식으로 입력하세요.", "화면 밝기 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _brightnessGrid.CurrentCell = row.Cells["StartTime"];
                    return false;
                }

                int percent;
                string percentText = Convert.ToString(row.Cells["Brightness"].Value);
                if (!int.TryParse((percentText ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)
                    || percent < 1
                    || percent > 100)
                {
                    MessageBox.Show(this, "밝기는 1부터 100 사이 숫자로 입력하세요.", "화면 밝기 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _brightnessGrid.CurrentCell = row.Cells["Brightness"];
                    return false;
                }

                if (startTimes.Contains(startMinute))
                {
                    MessageBox.Show(this, "같은 시작 시각이 중복되어 있습니다: " + BrightnessSchedule.FormatStartTime(startMinute), "화면 밝기 설정", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _brightnessGrid.CurrentCell = row.Cells["StartTime"];
                    return false;
                }

                startTimes.Add(startMinute);
            }

            return true;
        }

        private List<BrightnessPeriod> ReadBrightnessPeriods()
        {
            List<BrightnessPeriod> periods = new List<BrightnessPeriod>();
            if (_brightnessGrid == null)
            {
                return periods;
            }

            foreach (DataGridViewRow row in _brightnessGrid.Rows)
            {
                if (row.IsNewRow || IsBrightnessRowEmpty(row))
                {
                    continue;
                }

                int startMinute;
                int percent;
                if (!BrightnessSchedule.TryParseStartTime(Convert.ToString(row.Cells["StartTime"].Value), out startMinute)
                    || !int.TryParse(Convert.ToString(row.Cells["Brightness"].Value), NumberStyles.Integer, CultureInfo.InvariantCulture, out percent))
                {
                    continue;
                }

                object enabledValue = row.Cells["Enabled"].Value;
                bool enabled = enabledValue == null || Convert.ToBoolean(enabledValue, CultureInfo.InvariantCulture);
                BrightnessPeriod period = new BrightnessPeriod
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Enabled = enabled,
                    StartMinuteOfDay = startMinute,
                    BrightnessPercent = percent,
                    NightLightAction = ReadNightLightAction(row.Cells["NightLight"].Value)
                };
                period.Normalize();
                periods.Add(period);
            }

            return periods
                .OrderBy(period => period.StartMinuteOfDay)
                .ToList();
        }

        private static bool IsBrightnessRowEmpty(DataGridViewRow row)
        {
            if (row == null)
            {
                return true;
            }

            string startText = Convert.ToString(row.Cells["StartTime"].Value);
            string percentText = Convert.ToString(row.Cells["Brightness"].Value);
            return string.IsNullOrWhiteSpace(startText) && string.IsNullOrWhiteSpace(percentText);
        }

        private void VerifyBrightnessTime()
        {
            if (_brightnessVerifyResultLabel == null)
            {
                return;
            }

            if (!ValidateBrightnessRows())
            {
                return;
            }

            int startMinute;
            if (!BrightnessSchedule.TryParseStartTime(_brightnessVerifyTimeText.Text, out startMinute))
            {
                _brightnessVerifyResultLabel.Text = "검증할 시간은 HH:mm 형식으로 입력하세요.";
                return;
            }

            AppConfig config = AppConfig.CreateDefault();
            config.BrightnessScheduleEnabled = _brightnessScheduleCheck.Checked;
            config.DefaultBrightnessPercent = DefaultBrightnessPercent;
            config.BrightnessPeriods = ReadBrightnessPeriods();
            config.Normalize();

            DateTime verifyTime = DateTime.Today.AddMinutes(startMinute);
            BrightnessScheduleTarget target = BrightnessSchedule.GetTarget(config, verifyTime);
            string source = target.IsDefault
                ? "기본 밝기"
                : BrightnessSchedule.FormatStartTime(target.EffectiveStartMinuteOfDay) + " 시작 행";
            _brightnessVerifyResultLabel.Text =
                BrightnessSchedule.FormatStartTime(startMinute)
                + " 기준: " + source
                + " -> 밝기 " + target.BrightnessPercent + "%"
                + " · 야간모드 " + BrightnessSchedule.FormatNightLightAction(target.NightLightAction)
                + " · 현재 " + NightLightController.GetStatusSummary();

            if (_nightLightStatusLabel != null)
            {
                _nightLightStatusLabel.Text = "야간모드: " + NightLightController.GetStatusSummary();
            }
        }

        private static string ReadNightLightAction(object value)
        {
            string text = Convert.ToString(value);
            if (string.Equals(text, "켜기", StringComparison.OrdinalIgnoreCase))
            {
                return "On";
            }

            if (string.Equals(text, "끄기", StringComparison.OrdinalIgnoreCase))
            {
                return "Off";
            }

            return "Keep";
        }

        private TableLayoutPanel CreateSection(string titleText)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Top;
            section.AutoSize = true;
            section.ColumnCount = 1;
            section.RowCount = 0;
            section.Padding = new Padding(12, 10, 12, 10);
            section.Margin = new Padding(0, 0, 0, 12);
            section.BackColor = Color.FromArgb(253, 253, 249);

            Label title = new Label();
            title.Text = titleText;
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(20, 91, 69);
            title.Margin = new Padding(0, 0, 0, 8);
            section.Controls.Add(title, 0, section.RowCount++);
            return section;
        }

        private Control CreateStatusLine(string labelText, string valueText)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Top;
            row.AutoSize = true;
            row.ColumnCount = 2;
            row.Margin = new Padding(0, 2, 0, 4);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.ForeColor = Color.FromArgb(97, 111, 103);
            label.Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold, GraphicsUnit.Point);
            label.Margin = new Padding(0, 2, 8, 2);
            row.Controls.Add(label, 0, 0);

            Label value = new Label();
            value.Text = valueText ?? "";
            value.AutoSize = true;
            value.MaximumSize = new Size(380, 0);
            value.ForeColor = Color.FromArgb(35, 45, 47);
            value.Margin = new Padding(0, 2, 0, 2);
            row.Controls.Add(value, 1, 0);
            return row;
        }

        private Label CreateStatusValueLabel()
        {
            Label value = new Label();
            value.AutoSize = true;
            value.MaximumSize = new Size(470, 0);
            value.ForeColor = Color.FromArgb(35, 45, 47);
            value.BackColor = Color.FromArgb(253, 253, 249);
            value.Padding = new Padding(8, 4, 8, 4);
            value.Margin = new Padding(0, 0, 0, 0);
            return value;
        }

        private Control CreateStatusValueLine(string labelText, Label valueLabel)
        {
            TableLayoutPanel row = new TableLayoutPanel();
            row.Dock = DockStyle.Top;
            row.AutoSize = true;
            row.ColumnCount = 2;
            row.Margin = new Padding(0, 2, 0, 6);
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.ForeColor = Color.FromArgb(97, 111, 103);
            label.Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold, GraphicsUnit.Point);
            label.Margin = new Padding(0, 6, 8, 2);
            row.Controls.Add(label, 0, 0);
            row.Controls.Add(valueLabel, 1, 0);
            return row;
        }

        private void SetStatusValue(Label label, string text, bool emphasized)
        {
            if (label == null || label.IsDisposed)
            {
                return;
            }

            label.Text = text ?? "";
            label.BackColor = emphasized ? Color.FromArgb(220, 240, 229) : Color.FromArgb(253, 253, 249);
            label.ForeColor = emphasized ? Color.FromArgb(20, 91, 69) : Color.FromArgb(35, 45, 47);
        }

        private Control CreateNoteLine(string text)
        {
            Label note = new Label();
            note.Text = text ?? "";
            note.AutoSize = true;
            note.MaximumSize = new Size(470, 0);
            note.ForeColor = Color.FromArgb(97, 111, 103);
            note.Margin = new Padding(21, 0, 0, 7);
            return note;
        }

        private Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Size = new Size(Math.Max(84, TextRenderer.MeasureText(text, Font).Width + 32), 32);
            button.Margin = new Padding(4, 0, 0, 0);
            button.UseVisualStyleBackColor = true;
            return button;
        }

        private static void OpenFolder(string path)
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
                DiagnosticsLog.Write("설정 폴더 열기 실패", ex);
            }
        }

        private static void OpenFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    DiagnosticsLog.WriteEvent("열 로그 파일이 아직 없습니다: " + path);
                    return;
                }

                Process.Start("notepad.exe", path);
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("로그 파일 열기 실패", ex);
            }
        }
    }
}
