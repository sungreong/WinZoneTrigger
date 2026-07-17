using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private const int CompactLayoutWidth = 1000;

        private void ApplyResponsiveLayout()
        {
            if (_contentGrid == null || _zoneSidebar == null || _contentDivider == null || _detailHost == null)
            {
                return;
            }

            // The location list is navigation, not form content.  On smaller
            // windows it becomes a picker so the active form keeps the full width.
            bool compact = ClientSize.Width < CompactLayoutWidth;
            _contentGrid.SuspendLayout();
            _contentGrid.ColumnStyles.Clear();
            _contentGrid.RowStyles.Clear();

            if (compact)
            {
                _contentGrid.ColumnCount = 1;
                _contentGrid.RowCount = 1;
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                _zoneSidebar.Visible = false;
                _contentDivider.Visible = false;
                _contentGrid.SetCellPosition(_detailHost, new TableLayoutPanelCellPosition(0, 0));
            }
            else
            {
                int sidebarWidth = ClientSize.Width < 1240 ? 320 : 332;
                _contentGrid.ColumnCount = 3;
                _contentGrid.RowCount = 1;
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, sidebarWidth));
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1));
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                _zoneSidebar.Visible = true;
                _contentDivider.Visible = true;
                _contentGrid.SetCellPosition(_zoneSidebar, new TableLayoutPanelCellPosition(0, 0));
                _contentGrid.SetCellPosition(_contentDivider, new TableLayoutPanelCellPosition(1, 0));
                _contentGrid.SetCellPosition(_detailHost, new TableLayoutPanelCellPosition(2, 0));
            }

            if (_zonePickerButton != null)
            {
                _zonePickerButton.Visible = compact;
            }

            if (_detailTabs != null)
            {
                int tabWidth = compact ? 104 : 132;
                _detailTabs.ItemSize = new Size(tabWidth, 34);
            }

            if (_zoneTabs != null)
            {
                _zoneTabs.ItemSize = new Size(compact || ClientSize.Width >= 1240 ? 100 : 90, 34);
            }

            _contentGrid.ResumeLayout(true);
            SetDetailTableLabelWidth(_conditionTable, compact ? 96 : 118);
            SetDetailTableLabelWidth(_actionTable, compact ? 96 : 118);
            SetDetailTableLabelWidth(_appWatchTable, compact ? 96 : 118);
            SetDetailTableLabelWidth(_statusTable, compact ? 96 : 118);
            ConfigureCoordinateLayout(ShouldUseCompactCoordinateLayout(compact));
            ResizeAppWatchItemRows();
        }

        private bool ShouldUseCompactCoordinateLayout(bool compactWindow)
        {
            if (compactWindow || _conditionTable == null)
            {
                return true;
            }

            int labelColumnWidth = _conditionTable.ColumnStyles.Count == 0
                ? 118
                : Convert.ToInt32(_conditionTable.ColumnStyles[0].Width);
            int inputWidth = _conditionTable.ClientSize.Width - _conditionTable.Padding.Horizontal - labelColumnWidth;
            return inputWidth < 620;
        }

        private void ConfigureCoordinateLayout(bool compact)
        {
            if (_coordinatesPanel == null || _latitudeText == null || _longitudeText == null || _radiusInput == null)
            {
                return;
            }

            _coordinatesPanel.SuspendLayout();
            _coordinatesPanel.Controls.Clear();
            _coordinatesPanel.ColumnStyles.Clear();
            _coordinatesPanel.RowStyles.Clear();

            if (compact)
            {
                _coordinatesPanel.ColumnCount = 2;
                _coordinatesPanel.RowCount = 4;
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int row = 0; row < 4; row++)
                {
                    _coordinatesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                }

                AddCoordinateControlPair(_latitudeCoordinateLabel, _latitudeText, 0);
                AddCoordinateControlPair(_longitudeCoordinateLabel, _longitudeText, 1);
                AddCoordinateControlPair(_radiusCoordinateLabel, _radiusInput, 2);
                _coordinatesPanel.Controls.Add(_currentLocationButton, 0, 3);
                _coordinatesPanel.SetColumnSpan(_currentLocationButton, 2);
            }
            else
            {
                // Two short input pairs on the first line and the optional action
                // below it keep the form readable without an eight-column squeeze.
                _coordinatesPanel.ColumnCount = 6;
                _coordinatesPanel.RowCount = 2;
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38));
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
                _coordinatesPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
                _coordinatesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                _coordinatesPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                _coordinatesPanel.Controls.Add(_latitudeCoordinateLabel, 0, 0);
                _coordinatesPanel.Controls.Add(_latitudeText, 1, 0);
                _coordinatesPanel.Controls.Add(_longitudeCoordinateLabel, 2, 0);
                _coordinatesPanel.Controls.Add(_longitudeText, 3, 0);
                _coordinatesPanel.Controls.Add(_radiusCoordinateLabel, 4, 0);
                _coordinatesPanel.Controls.Add(_radiusInput, 5, 0);
                _coordinatesPanel.Controls.Add(_currentLocationButton, 0, 1);
                _coordinatesPanel.SetColumnSpan(_currentLocationButton, 6);
            }

            _coordinatesPanel.ResumeLayout(true);
        }

        private void AddCoordinateControlPair(Label label, Control input, int row)
        {
            _coordinatesPanel.Controls.Add(label, 0, row);
            _coordinatesPanel.Controls.Add(input, 1, row);
        }

        private void ShowCompactZonePicker()
        {
            if (_zonePickerButton == null)
            {
                return;
            }

            CaptureCurrentZone();
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.Closed += delegate { menu.Dispose(); };

            foreach (ZoneRule zone in _config.Zones)
            {
                ZoneRule selectedZone = zone;
                ToolStripMenuItem item = new ToolStripMenuItem((selectedZone.Enabled ? "● " : "○ ") + selectedZone.Name);
                item.Checked = string.Equals(_currentZoneId, selectedZone.Id, StringComparison.OrdinalIgnoreCase);
                item.Click += delegate { BindZoneList(selectedZone.Id); };
                menu.Items.Add(item);
            }

            if (menu.Items.Count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("등록된 위치가 없습니다");
                empty.Enabled = false;
                menu.Items.Add(empty);
            }

            menu.Show(_zonePickerButton, new Point(0, _zonePickerButton.Height));
        }

        private void SetDetailTableLabelWidth(TableLayoutPanel table, int width)
        {
            if (table == null || table.ColumnStyles.Count == 0)
            {
                return;
            }

            table.ColumnStyles[0].SizeType = SizeType.Absolute;
            table.ColumnStyles[0].Width = width;
            UpdateDetailTableWrappedLabels(table);
        }
    }
}
