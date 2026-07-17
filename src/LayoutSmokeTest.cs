using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

        internal static int Run(bool savePreviews)
        {
            List<string> failures = new List<string>();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (MainForm form = new MainForm(false, false, false))
                {
                    RunLayoutPasses(form, "main", Viewports, failures, savePreviews);
                }

                using (SettingsForm form = new SettingsForm(false, false, true, true, 70, new List<BrightnessPeriod>(), false))
                {
                    RunLayoutPasses(form, "settings", new[]
                    {
                        new Size(640, 560),
                        new Size(820, 700)
                    }, failures, savePreviews);
                }

                using (AppPickerForm form = new AppPickerForm())
                {
                    RunLayoutPasses(form, "app-picker", new[]
                    {
                        new Size(500, 360),
                        new Size(560, 440)
                    }, failures, savePreviews);
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

            DiagnosticsLog.WriteEvent("레이아웃 스모크 테스트 통과: 메인 720/900/1000/1180px, 설정 640/820px, 앱 선택 500/560px");
            return 0;
        }

        private static void RunLayoutPasses(Form form, string formName, IEnumerable<Size> viewports, List<string> failures, bool savePreviews)
        {
            PrepareFormForLayoutTest(form);
            foreach (Size viewport in viewports)
            {
                bool savedTabPreview = false;
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
                        if (savePreviews)
                        {
                            SavePreview(form, formName + "-tab" + (index + 1), viewport);
                            savedTabPreview = true;
                        }
                    }
                }

                if (savePreviews && !savedTabPreview)
                {
                    SavePreview(form, formName, viewport);
                }
            }
        }

        private static void PrepareFormForLayoutTest(Form form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            form.Show();
        }

        private static void SavePreview(Form form, string formName, Size viewport)
        {
            string folder = Path.Combine(ConfigStore.ConfigDirectory, "layout-preview");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, formName + "-" + viewport.Width + "x" + viewport.Height + ".png");
            using (Bitmap bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height)))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(path, ImageFormat.Png);
            }
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
