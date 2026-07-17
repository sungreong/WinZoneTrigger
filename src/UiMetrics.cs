using System;
using System.Drawing;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    internal static class UiMetrics
    {
        internal const int SpaceXs = 4;
        internal const int SpaceSm = 8;
        internal const int SpaceMd = 12;
        internal const int SpaceLg = 16;
        internal const int SpaceXl = 24;
        internal const int Space2Xl = 32;

        internal const int InputHeight = 32;
        internal const int IconButtonSize = 32;
        internal const int SidebarActionHeight = 36;
        internal const int DesktopLabelColumnWidth = 120;
        internal const int CompactLabelColumnWidth = 96;

        internal static int GetTextControlHeight(Font font)
        {
            Font effectiveFont = font ?? SystemFonts.MessageBoxFont;
            int textHeight = TextRenderer.MeasureText("가", effectiveFont).Height;
            return Math.Max(InputHeight, textHeight + (SpaceXs * 2));
        }

        internal static int GetSidebarActionHeight(Font font)
        {
            return Math.Max(SidebarActionHeight, GetTextControlHeight(font) + SpaceXs);
        }

        internal static TextBox CreateTextBox()
        {
            TextBox textBox = new TextBox();
            textBox.AutoSize = false;
            textBox.Height = InputHeight;
            return textBox;
        }

        internal static NumericUpDown CreateNumericUpDown()
        {
            NumericUpDown input = new NumericUpDown();
            input.Height = InputHeight;
            return input;
        }

        internal static ComboBox CreateComboBox()
        {
            ComboBox input = new ComboBox();
            input.Height = InputHeight;
            return input;
        }

        internal static void ApplyTextButtonMetrics(Button button)
        {
            if (button == null)
            {
                return;
            }

            button.MinimumSize = new Size(button.MinimumSize.Width, InputHeight);
            button.Height = InputHeight;
        }
    }
}
