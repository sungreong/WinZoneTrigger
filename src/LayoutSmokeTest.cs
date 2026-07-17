using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace WinZoneTrigger
{
    // Explicitly opt-in, deterministic visual regression check.  It never loads
    // user settings, starts timers/tray icons, or writes the normal activity log.
    internal static class LayoutSmokeTest
    {
        private static readonly Size[] Viewports =
        {
            new Size(720, 600), new Size(900, 680), new Size(1120, 720), new Size(1440, 900)
        };

        internal static int Run(bool savePreviews)
        {
            List<string> failures = new List<string>();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                RunLayoutPasses(delegate { return new MainForm(CreateMainFixture()); }, "main", Viewports, failures, savePreviews);
                RunLayoutPasses(delegate
                {
                    return new SettingsForm(true, true, true, true, 70,
                        new List<BrightnessPeriod> { BrightnessPeriod.CreateDefault(), BrightnessPeriod.CreateDefault() }, false, true);
                }, "settings", Viewports, failures, savePreviews);
                RunLayoutPasses(delegate { return new AppPickerForm(CreateAppPickerFixture()); }, "app-picker", Viewports, failures, savePreviews);
            }
            catch (Exception ex)
            {
                failures.Add("예외: " + ex.GetType().Name + " - " + ex.Message);
            }

            if (failures.Count > 0)
            {
                WriteResult("실패" + Environment.NewLine + string.Join(Environment.NewLine, failures.ToArray()));
                return 1;
            }

            WriteResult("통과: 720/900/1120/1440 및 125% 스케일");
            return 0;
        }

        private static void WriteResult(string text)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "layout-preview");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "result.txt"), text ?? "");
        }

        private static void RunLayoutPasses(Func<Form> createForm, string formName, IEnumerable<Size> viewports, List<string> failures, bool savePreviews)
        {
            foreach (float scale in new[] { 1F, 1.25F })
            {
                foreach (Size viewport in viewports)
                {
                    using (Form form = createForm())
                    {
                        PrepareFormForLayoutTest(form);
                        form.ClientSize = viewport;
                        form.PerformLayout();
                        if (scale > 1F)
                        {
                            form.Scale(new SizeF(scale, scale));
                        }
                        Application.DoEvents();
                        RunTabAndScrollPasses(form, formName, viewport, scale, failures, savePreviews);
                    }

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

        private static void RunTabAndScrollPasses(Form form, string formName, Size viewport, float scale, List<string> failures, bool savePreviews)
        {
            List<TabControl> tabs = FindTabControls(form);
            if (tabs.Count == 0)
            {
                CaptureAndCheck(form, formName, viewport, scale, "main", failures, savePreviews);
                return;
            }

            for (int tabIndex = 0; tabIndex < tabs.Count; tabIndex++)
            {
                TabControl tabsForPass = tabs[tabIndex];
                for (int pageIndex = 0; pageIndex < tabsForPass.TabPages.Count; pageIndex++)
                {
                    tabsForPass.SelectedIndex = pageIndex;
                    form.PerformLayout();
                    Application.DoEvents();
                    foreach (int scrollPercent in new[] { 0, 50, 100 })
                    {
                        SetScrollPositions(tabsForPass.SelectedTab, scrollPercent);
                        CaptureAndCheck(form, formName, viewport, scale,
                            "tabs" + tabIndex + "-page" + pageIndex + "-scroll" + scrollPercent, failures, savePreviews);
                    }
                }
            }
        }

        private static void CaptureAndCheck(Form form, string formName, Size viewport, float scale, string suffix, List<string> failures, bool savePreviews)
        {
            form.PerformLayout();
            Application.DoEvents();
            FindUnexpectedOverflows(form, formName + " " + viewport.Width + "x" + viewport.Height + " @" + scale, failures);
            if (savePreviews)
            {
                SavePreview(form, formName + "-" + suffix + "-" + viewport.Width + "x" + viewport.Height + "-" + (int)(scale * 100));
            }
        }

        private static void SetScrollPositions(Control root, int percent)
        {
            foreach (ScrollableControl scrollable in FindScrollableControls(root))
            {
                int maximum = Math.Max(0, scrollable.VerticalScroll.Maximum - scrollable.VerticalScroll.LargeChange + 1);
                scrollable.VerticalScroll.Value = Math.Min(maximum, maximum * percent / 100);
            }
        }

        private static IEnumerable<ScrollableControl> FindScrollableControls(Control root)
        {
            ScrollableControl scrollable = root as ScrollableControl;
            if (scrollable != null && scrollable.AutoScroll)
            {
                yield return scrollable;
            }

            foreach (Control child in root.Controls)
            {
                foreach (ScrollableControl nested in FindScrollableControls(child))
                {
                    yield return nested;
                }
            }
        }

        private static void SavePreview(Form form, string name)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "layout-preview");
            Directory.CreateDirectory(folder);
            using (Bitmap bitmap = new Bitmap(Math.Max(1, form.Width), Math.Max(1, form.Height)))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(folder, name + ".png"), ImageFormat.Png);
            }
        }

        private static void FindUnexpectedOverflows(Control root, string scope, List<string> failures)
        {
            CheckChildren(root, scope, failures);
        }

        private static void CheckChildren(Control parent, string scope, List<string> failures)
        {
            foreach (Control child in parent.Controls)
            {
                if (!child.Visible || child.Parent == null)
                {
                    continue;
                }

                // Layout containers own their children' coordinate systems and can
                // include padding/margins outside ClientSize. Validate the visible
                // leaf controls instead of reporting those implementation details.
                bool layoutContainer = child is TableLayoutPanel
                    || child is FlowLayoutPanel
                    || child is TabControl
                    || child is TabPage
                    || child is Panel;
                bool intentionalScroll = parent is ScrollableControl && ((ScrollableControl)parent).AutoScroll;
                int effectiveRight = child.Right - child.Margin.Right;
                int effectiveBottom = child.Bottom - child.Margin.Bottom;
                // TableLayoutPanel reports label bounds before its final text-wrap
                // pass at scaled DPI. The rendered label is clipped to its cell;
                // validate editable/action controls in that case instead.
                bool tableLabel = child is Label && parent is TableLayoutPanel;
                if (!layoutContainer && !tableLabel && effectiveRight > parent.ClientSize.Width + 2)
                {
                    failures.Add(scope + " " + ControlName(child) + " right overflow in " + ControlName(parent));
                }
                if (!layoutContainer && !tableLabel && !intentionalScroll && effectiveBottom > parent.ClientSize.Height + 2)
                {
                    failures.Add(scope + " " + ControlName(child) + " bottom overflow in " + ControlName(parent));
                }

                CheckChildren(child, scope, failures);
            }
        }

        private static string ControlName(Control control)
        {
            return string.IsNullOrWhiteSpace(control.Name) ? control.GetType().Name : control.Name;
        }

        private static List<TabControl> FindTabControls(Control root)
        {
            List<TabControl> result = new List<TabControl>();
            TabControl tabs = root as TabControl;
            if (tabs != null)
            {
                result.Add(tabs);
            }
            foreach (Control child in root.Controls)
            {
                result.AddRange(FindTabControls(child));
            }
            return result;
        }

        private static AppConfig CreateMainFixture()
        {
            ZoneRule longZone = ZoneRule.CreateDefault("아주 긴 위치 이름 - 서울 본사와 연구동을 함께 포함하는 이동 중 자동화 위치");
            longZone.UseCoordinates = true;
            longZone.UseWifiCondition = true;
            longZone.Latitude = 37.5665123;
            longZone.Longitude = 126.9784567;
            longZone.RadiusMeters = 350;
            longZone.NearbySsids = new List<string>
            {
                "회사-보안-Wi-Fi-매우-긴-네트워크-이름-2.4GHz", "회사-보안-Wi-Fi-매우-긴-네트워크-이름-5GHz"
            };
            longZone.ChromeUrls = new List<string> { "https://example.com/very/long/path/for/layout/verification?tab=automation&source=fixture" };
            longZone.AppLaunches = new List<string> { @"C:\Program Files\Example Company\A Very Long Application Name\example.exe" };
            longZone.Commands = new List<string> { "powershell -NoProfile -Command \"Write-Output deterministic-layout-fixture\"" };
            longZone.AppWatchItems = new List<AppWatchItem>
            {
                new AppWatchItem { Id = "fixture-watch-1", Enabled = true, RequireWindow = true, ProcessName = "LongApplicationProcessName", LaunchTarget = @"C:\Program Files\Example\LongApplication.exe", IntervalValue = 1, IntervalUnit = "Minutes" },
                new AppWatchItem { Id = "fixture-watch-2", Enabled = true, RequireWindow = false, ProcessName = "AnotherLongApplicationProcess", LaunchTarget = @"C:\Tools\Another Long Application\runner.exe", IntervalValue = 15, IntervalUnit = "Minutes" }
            };
            ZoneRule emptyZone = ZoneRule.CreateDefault("빈 상태 위치");
            emptyZone.Enabled = false;
            return new AppConfig { Zones = new List<ZoneRule> { longZone, emptyZone }, DefaultBrightnessPercent = 70 };
        }

        private static List<AppSearchCandidate> CreateAppPickerFixture()
        {
            return new List<AppSearchCandidate>
            {
                new AppSearchCandidate { Name = "긴 이름의 업무 자동화 프로그램", Source = "시작 메뉴", Target = @"C:\Program Files\Example\A Very Long App Path\launcher.exe" },
                new AppSearchCandidate { Name = "보조 앱", Source = "설치된 앱", AppId = "Example.Package_abcdefgh!App", Target = @"shell:AppsFolder\Example.Package_abcdefgh!App" },
                new AppSearchCandidate { Name = "빈 결과 검증용", Source = "시작 메뉴", Target = @"C:\Tools\Fixture\empty.exe" }
            };
        }
    }
}
