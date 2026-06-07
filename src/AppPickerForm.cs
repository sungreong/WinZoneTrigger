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
    internal sealed class AppPickerForm : Form
    {
        private readonly TextBox _searchText;
        private readonly ListBox _resultList;
        private readonly Label _statusLabel;
        private readonly Button _refreshButton;
        private readonly Button _addButton;
        private readonly Button _cancelButton;
        private List<AppSearchCandidate> _candidates;

        public List<string> SelectedTargets { get; private set; }

        public AppPickerForm()
        {
            SelectedTargets = new List<string>();
            Text = "앱 찾기";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 440);
            MinimumSize = new Size(500, 360);
            Font = new Font("Malgun Gothic", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(246, 247, 242);
            ForeColor = Color.FromArgb(35, 45, 47);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(14);
            root.RowCount = 5;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            Controls.Add(root);

            Label title = new Label();
            title.Text = "컴퓨터에서 앱 찾기";
            title.AutoSize = true;
            title.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = Color.FromArgb(20, 91, 69);
            title.Margin = new Padding(0, 0, 0, 6);
            root.Controls.Add(title, 0, 0);

            TableLayoutPanel searchRow = new TableLayoutPanel();
            searchRow.Dock = DockStyle.Fill;
            searchRow.AutoSize = true;
            searchRow.ColumnCount = 2;
            searchRow.RowCount = 1;
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            searchRow.Margin = new Padding(0, 0, 0, 8);

            _searchText = new TextBox();
            _searchText.Dock = DockStyle.Fill;
            _searchText.Margin = new Padding(0, 0, 8, 0);
            _searchText.TextChanged += delegate { LoadCandidates(_searchText.Text); };
            searchRow.Controls.Add(_searchText, 0, 0);

            _refreshButton = CreateDialogButton("새로고침", false);
            _refreshButton.Click += delegate { LoadCandidates(_searchText.Text, true); };
            searchRow.Controls.Add(_refreshButton, 1, 0);
            root.Controls.Add(searchRow, 0, 1);

            Label hint = new Label();
            hint.Text = "시작 메뉴와 설치된 앱을 검색합니다. Ctrl 또는 Shift로 여러 앱을 선택할 수 있습니다.";
            hint.AutoSize = true;
            hint.ForeColor = Color.FromArgb(97, 111, 103);
            hint.Margin = new Padding(0, 0, 0, 3);

            _statusLabel = new Label();
            _statusLabel.AutoSize = true;
            _statusLabel.ForeColor = Color.FromArgb(97, 111, 103);
            _statusLabel.Margin = new Padding(0, 0, 0, 8);

            TableLayoutPanel hintStack = new TableLayoutPanel();
            hintStack.Dock = DockStyle.Fill;
            hintStack.AutoSize = true;
            hintStack.ColumnCount = 1;
            hintStack.RowCount = 2;
            hintStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            hintStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            hintStack.Controls.Add(hint, 0, 0);
            hintStack.Controls.Add(_statusLabel, 0, 1);
            root.Controls.Add(hintStack, 0, 2);

            _resultList = new ListBox();
            _resultList.Dock = DockStyle.Fill;
            _resultList.IntegralHeight = false;
            _resultList.DrawMode = DrawMode.Normal;
            _resultList.SelectionMode = SelectionMode.MultiExtended;
            _resultList.Margin = new Padding(0, 0, 0, 12);
            _resultList.SelectedIndexChanged += delegate { UpdateAddButton(); };
            _resultList.DoubleClick += delegate { AcceptSelection(); };
            root.Controls.Add(_resultList, 0, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.AutoSize = true;
            _cancelButton = CreateDialogButton("취소", false);
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(_cancelButton);

            _addButton = CreateDialogButton("선택한 앱 등록", true);
            _addButton.Click += delegate { AcceptSelection(); };
            buttons.Controls.Add(_addButton);
            root.Controls.Add(buttons, 0, 4);

            LoadCandidates("");
            _searchText.Focus();
        }

        private Button CreateDialogButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = false;
            button.Size = new Size(Math.Max(80, TextRenderer.MeasureText(text, Font).Width + 34), 32);
            button.Margin = new Padding(6, 0, 0, 0);
            button.Cursor = Cursors.Hand;
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;

            if (primary)
            {
                button.BackColor = Color.FromArgb(31, 122, 92);
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Color.FromArgb(20, 91, 69);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(20, 91, 69);
            }
            else
            {
                button.BackColor = Color.FromArgb(253, 253, 249);
                button.ForeColor = Color.FromArgb(35, 45, 47);
                button.FlatAppearance.BorderColor = Color.FromArgb(211, 218, 207);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 243, 235);
            }

            return button;
        }

        private void LoadCandidates(string query)
        {
            LoadCandidates(query, false);
        }

        private void LoadCandidates(string query, bool refresh)
        {
            if (refresh)
            {
                AppLauncher.RefreshInstalledAppIndex();
            }

            _candidates = AppLauncher.FindInstalledApps(query, 300, true);
            _resultList.Items.Clear();
            foreach (AppSearchCandidate candidate in _candidates)
            {
                _resultList.Items.Add(candidate);
            }

            if (_resultList.Items.Count > 0)
            {
                _resultList.SelectedIndex = 0;
            }

            string term = (query ?? "").Trim();
            _statusLabel.Text = string.IsNullOrWhiteSpace(term)
                ? "검색 가능한 앱 " + _candidates.Count + "개 표시"
                : "'" + term + "' 검색 결과 " + _candidates.Count + "개";
            UpdateAddButton();
        }

        private static string BuildCandidateTargetText(AppSearchCandidate candidate)
        {
            if (candidate == null)
            {
                return "";
            }

            if (!string.IsNullOrWhiteSpace(candidate.AppId))
            {
                return "시작 앱 · " + candidate.AppId;
            }

            if (!string.IsNullOrWhiteSpace(candidate.Source))
            {
                return candidate.Source + " · " + candidate.Target;
            }

            return string.Equals(candidate.Name, candidate.Target, StringComparison.OrdinalIgnoreCase)
                ? "시작 메뉴 앱"
                : candidate.Target;
        }

        private void UpdateAddButton()
        {
            int count = _resultList.SelectedItems.Count;
            _addButton.Enabled = count > 0;
            _addButton.Text = count <= 1 ? "선택한 앱 등록" : "선택한 앱 " + count + "개 등록";
            _addButton.Size = new Size(Math.Max(112, TextRenderer.MeasureText(_addButton.Text, Font).Width + 34), 32);
        }

        private void AcceptSelection()
        {
            SelectedTargets.Clear();
            foreach (object selected in _resultList.SelectedItems)
            {
                AppSearchCandidate candidate = selected as AppSearchCandidate;
                if (candidate != null && !string.IsNullOrWhiteSpace(candidate.Target) &&
                    !SelectedTargets.Any(target => string.Equals(target, candidate.Target, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedTargets.Add(candidate.Target);
                }
            }

            if (SelectedTargets.Count == 0)
            {
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
