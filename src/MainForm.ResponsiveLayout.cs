using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal sealed partial class MainForm : Form
    {
        private void ApplyResponsiveLayout()
        {
            if (_contentGrid == null || _zoneSidebar == null || _contentDivider == null)
            {
                return;
            }

            bool stacked = ClientSize.Width < 930;
            _contentGrid.SuspendLayout();
            _contentGrid.ColumnStyles.Clear();
            _contentGrid.RowStyles.Clear();

            if (stacked)
            {
                int sidebarHeight = Math.Max(220, Math.Min(280, ClientSize.Height / 3));
                _contentGrid.ColumnCount = 1;
                _contentGrid.RowCount = 3;
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, sidebarHeight));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                _contentGrid.SetCellPosition(_zoneSidebar, new TableLayoutPanelCellPosition(0, 0));
                _contentGrid.SetCellPosition(_contentDivider, new TableLayoutPanelCellPosition(0, 1));
                _contentGrid.SetCellPosition(_detailTabs.Parent.Parent, new TableLayoutPanelCellPosition(0, 2));
                _detailTabs.ItemSize = new Size(112, 34);
            }
            else
            {
                int sidebarWidth = ClientSize.Width < 1120 ? 270 : 360;
                _contentGrid.ColumnCount = 3;
                _contentGrid.RowCount = 1;
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, sidebarWidth));
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5));
                _contentGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                _contentGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                _contentGrid.SetCellPosition(_zoneSidebar, new TableLayoutPanelCellPosition(0, 0));
                _contentGrid.SetCellPosition(_contentDivider, new TableLayoutPanelCellPosition(1, 0));
                _contentGrid.SetCellPosition(_detailTabs.Parent.Parent, new TableLayoutPanelCellPosition(2, 0));
                _detailTabs.ItemSize = new Size(142, 36);
            }

            _contentGrid.ResumeLayout(true);
            ResizeAppWatchItemRows();
        }
    }
}
