using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed class SettingsForm : Form
    {
        private readonly CheckBox _startupCheck;
        private readonly CheckBox _startMinimizedCheck;
        private readonly CheckBox _preventSleepCheck;
        private readonly CheckBox _trayIconCheck;

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

        public bool TrayIconEnabled
        {
            get { return _trayIconCheck.Checked; }
        }

        public SettingsForm(
            bool startupEnabled,
            bool startMinimized,
            bool preventSleepWhileAutomationActive,
            bool trayIconEnabled)
        {
            Text = "설정";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(560, 500);
            Size = new Size(590, 540);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            BackColor = Color.FromArgb(246, 247, 242);
            ForeColor = Color.FromArgb(35, 45, 47);
            Font = new Font("Malgun Gothic", 9.25F, FontStyle.Regular, GraphicsUnit.Point);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(16);
            root.RowCount = 6;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label title = new Label();
            title.Text = "앱 설정";
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(20, 91, 69);
            title.Margin = new Padding(0, 0, 0, 12);
            root.Controls.Add(title, 0, 0);

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
            root.Controls.Add(startupPanel, 0, 1);

            TableLayoutPanel powerPanel = CreateSection("전원");
            _preventSleepCheck = new CheckBox();
            _preventSleepCheck.Text = "자동 감시 중 Windows 자동 절전 방지";
            _preventSleepCheck.Checked = preventSleepWhileAutomationActive;
            _preventSleepCheck.AutoSize = true;
            _preventSleepCheck.Margin = new Padding(0, 4, 0, 4);
            powerPanel.Controls.Add(_preventSleepCheck, 0, powerPanel.RowCount++);
            powerPanel.Controls.Add(CreateNoteLine("자동 절전만 방지합니다. 직접 전원 동작은 그대로 진행됩니다."), 0, powerPanel.RowCount++);
            root.Controls.Add(powerPanel, 0, 2);

            TableLayoutPanel trayPanel = CreateSection("트레이");
            _trayIconCheck = new CheckBox();
            _trayIconCheck.Text = "설정 화면 트레이 아이콘 표시";
            _trayIconCheck.Checked = trayIconEnabled;
            _trayIconCheck.AutoSize = true;
            _trayIconCheck.Margin = new Padding(0, 4, 0, 4);
            trayPanel.Controls.Add(_trayIconCheck, 0, trayPanel.RowCount++);
            trayPanel.Controls.Add(CreateNoteLine("상태 확인과 설정 열기만 제공합니다. 풍선 알림은 사용하지 않습니다."), 0, trayPanel.RowCount++);
            root.Controls.Add(trayPanel, 0, 3);

            TableLayoutPanel diagnosticsPanel = CreateSection("진단");
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
            root.Controls.Add(diagnosticsPanel, 0, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            buttons.Margin = new Padding(0, 12, 0, 0);
            Button saveButton = CreateButton("저장");
            saveButton.DialogResult = DialogResult.OK;
            Button cancelButton = CreateButton("취소");
            cancelButton.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            root.Controls.Add(buttons, 0, 5);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
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
