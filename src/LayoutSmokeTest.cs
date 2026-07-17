using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    // A release-safe layout check. It runs only when explicitly requested and
    // never starts the background automation process.
    internal static class LayoutSmokeTest
    {
        private static readonly Size[] Viewports =
        {
            new Size(720, 600),
            new Size(900, 680),
            new Size(1000, 720),
            new Size(1180, 820)
        };

        internal static int Run()
        {
            List<string> failures = new List<string>();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (MainForm form = new MainForm(false, false, false))
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(-10000, -10000);
                    form.ShowInTaskbar = false;
                    form.Opacity = 0;
                    form.Show();

                    foreach (Size viewport in Viewports)
                    {
                        form.ClientSize = viewport;
                        form.PerformLayout();
                        Application.DoEvents();
                        foreach (TabControl tabs in FindTabControls(form))
                        {
                            for (int index = 0; index < tabs.TabPages.Count; index++)
                            {
                                tabs.SelectedIndex = index;
                                form.PerformLayout();
                                Application.DoEvents();
                                FindHorizontalOverflows(form, viewport, failures);
                            }
                        }
                    }

                    form.Close();
                }
            }
            catch (Exception ex)
            {
                DiagnosticsLog.Write("레이아웃 스모크 테스트 예외", ex);
                return 1;
            }

            if (failures.Count > 0)
            {
                DiagnosticsLog.WriteEvent("레이아웃 스모크 테스트 실패: " + string.Join(" | ", failures.ToArray()));
                return 1;
            }

            DiagnosticsLog.WriteEvent("레이아웃 스모크 테스트 통과: 720, 900, 1000, 1180px");
            return 0;
        }

        private static void FindHorizontalOverflows(Control root, Size viewport, List<string> failures)
        {
            CheckChildren(root, viewport, failures);
        }

        private static void CheckChildren(Control parent, Size viewport, List<string> failures)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible || child.Parent == null)
                {
                    continue;
                }

                if (ShouldCheckBounds(parent, child)
                    && child.Right > parent.ClientSize.Width + 2)
                {
                    string name = string.IsNullOrWhiteSpace(child.Name) ? child.GetType().Name : child.Name;
                    failures.Add(viewport.Width + "px " + name + " exceeds " + parent.GetType().Name);
                }

                CheckChildren(child, viewport, failures);
            }
        }

        private static bool ShouldCheckBounds(Control parent, Control child)
        {
            // Containers use a DisplayRectangle whose origin may differ from the
            // client origin because of padding and tab chrome. Their children are
            // checked separately; validating the containers here creates noise.
            if (child is TableLayoutPanel || child is TabControl)
            {
                return false;
            }

            if (parent is TableLayoutPanel)
            {
                return true;
            }

            if (parent is FlowLayoutPanel || parent is TabControl || parent is TabPage)
            {
                return false;
            }

            ScrollableControl scrollableParent = parent as ScrollableControl;
            if (scrollableParent != null && scrollableParent.AutoScroll && child.Dock == DockStyle.None)
            {
                return false;
            }

            return child.Dock != DockStyle.Fill && child.Dock != DockStyle.Top && child.Dock != DockStyle.Bottom;
        }

        private static List<TabControl> FindTabControls(Control root)
        {
            List<TabControl> result = new List<TabControl>();
            AddTabControls(root, result);
            return result;
        }

        private static void AddTabControls(Control root, List<TabControl> result)
        {
            TabControl tabs = root as TabControl;
            if (tabs != null)
            {
                result.Add(tabs);
            }

            foreach (Control child in root.Controls)
            {
                AddTabControls(child, result);
            }
        }
    }
}
