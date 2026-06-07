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
        private void RenderWifiChoiceButtons(IEnumerable<string> selectedSsids, IEnumerable<WifiNetwork> visibleNetworks)
        {
            HashSet<string> selected = new HashSet<string>(
                (selectedSsids ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()),
                StringComparer.OrdinalIgnoreCase);

            List<WifiNetwork> visible = (visibleNetworks ?? new List<WifiNetwork>())
                .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                .OrderByDescending(n => n.SignalQuality)
                .ThenBy(n => n.Ssid)
                .ToList();

            ClearChildControls(_wifiChoicesPanel);

            foreach (WifiNetwork network in visible)
            {
                AddWifiToggle(network.Ssid, network.Ssid + " · " + network.SignalQuality + "%", selected.Contains(network.Ssid));
            }

            foreach (string ssid in selected.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                if (!visible.Any(n => string.Equals(n.Ssid, ssid, StringComparison.OrdinalIgnoreCase)))
                {
                    AddWifiToggle(ssid, ssid + " · 저장됨", true);
                }
            }

            if (_wifiChoicesPanel.Controls.Count == 0)
            {
                Label emptyLabel = new Label();
                emptyLabel.Text = "Wi-Fi 후보가 없습니다. 'Wi-Fi 후보 새로고침'을 눌러주세요.";
                emptyLabel.AutoSize = true;
                emptyLabel.ForeColor = UiTextMuted;
                emptyLabel.Tag = "Muted";
                emptyLabel.Margin = new Padding(8);
                _wifiChoicesPanel.Controls.Add(emptyLabel);
            }

            UpdateSelectedWifiLabel();
        }

        private void AddWifiToggle(string ssid, string text, bool isChecked)
        {
            CheckBox toggle = new CheckBox();
            toggle.Appearance = Appearance.Button;
            toggle.AutoSize = false;
            toggle.Text = text;
            toggle.Tag = ssid;
            toggle.Checked = isChecked;
            toggle.Margin = new Padding(4, 3, 4, 3);
            toggle.Size = GetChipSize(text, Font);
            StyleToggleButton(toggle);
            toggle.CheckedChanged += delegate
            {
                StyleToggleButton(toggle);
                UpdateSelectedWifiLabel();
                if (!_loadingSelection)
                {
                    CaptureCurrentZone();
                }
            };
            _wifiChoicesPanel.Controls.Add(toggle);
        }

        private List<string> GetSelectedWifiSsids()
        {
            List<string> values = new List<string>();
            foreach (Control control in _wifiChoicesPanel.Controls)
            {
                CheckBox toggle = control as CheckBox;
                if (toggle != null && toggle.Checked && toggle.Tag != null)
                {
                    string ssid = Convert.ToString(toggle.Tag);
                    if (!string.IsNullOrWhiteSpace(ssid) && !values.Any(v => string.Equals(v, ssid, StringComparison.OrdinalIgnoreCase)))
                    {
                        values.Add(ssid.Trim());
                    }
                }
            }

            return values;
        }

        private void UpdateSelectedWifiLabel()
        {
            List<string> selected = GetSelectedWifiSsids();
            _selectedWifiLabel.Text = selected.Count == 0
                ? "선택된 Wi-Fi 없음"
                : string.Join(", ", selected.ToArray());
        }

        private void RenderConnectWifiTargetButtons(IEnumerable<WifiNetwork> visibleNetworks)
        {
            List<WifiNetwork> visible = (visibleNetworks ?? new List<WifiNetwork>())
                .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                .OrderByDescending(n => n.SignalQuality)
                .ThenBy(n => n.Ssid)
                .ToList();

            ClearChildControls(_connectWifiChoicesPanel);
            string selected = string.IsNullOrWhiteSpace(_connectSsidText.Text) ? _connectProfileText.Text : _connectSsidText.Text;

            foreach (WifiNetwork network in visible)
            {
                Button button = CreateButton(network.Ssid + " · " + network.SignalQuality + "%");
                button.Tag = network.Ssid;
                Size chipSize = GetChipSize(button.Text, button.Font);
                button.AutoSize = false;
                button.Size = new Size(chipSize.Width, 30);
                if (string.Equals(network.Ssid, selected, StringComparison.OrdinalIgnoreCase))
                {
                    button.Font = new Font(button.Font, FontStyle.Bold);
                    button.BackColor = UiAccentSoft;
                    button.ForeColor = UiAccentDark;
                    button.FlatAppearance.BorderColor = UiAccent;
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(207, 232, 218);
                }
                button.Click += delegate
                {
                    string ssid = Convert.ToString(button.Tag);
                    SetConnectWifiTarget(ssid);
                    RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                };
                _connectWifiChoicesPanel.Controls.Add(button);
            }

            if (_connectWifiChoicesPanel.Controls.Count == 0)
            {
                Label emptyLabel = new Label();
                emptyLabel.Text = "Wi-Fi 후보가 없습니다. 'Wi-Fi 후보 새로고침' 또는 '테스트해보기'를 눌러주세요.";
                emptyLabel.AutoSize = true;
                emptyLabel.ForeColor = UiTextMuted;
                emptyLabel.Tag = "Muted";
                emptyLabel.Margin = new Padding(8);
                _connectWifiChoicesPanel.Controls.Add(emptyLabel);
            }

            UpdateConnectWifiTargetLabel();
        }

        private void SetConnectWifiTarget(string ssid)
        {
            if (string.IsNullOrWhiteSpace(ssid))
            {
                return;
            }

            _connectWifiCheck.Checked = true;
            _connectProfileText.Text = ssid.Trim();
            _connectSsidText.Text = ssid.Trim();
            UpdateConnectWifiTargetLabel();
            if (!_loadingSelection)
            {
                CaptureCurrentZone();
            }
        }

        private void UpdateConnectWifiTargetLabel()
        {
            string profile = _connectProfileText == null ? "" : _connectProfileText.Text.Trim();
            string ssid = _connectSsidText == null ? "" : _connectSsidText.Text.Trim();

            if (string.IsNullOrWhiteSpace(profile) && string.IsNullOrWhiteSpace(ssid))
            {
                _connectWifiTargetLabel.Text = "연결 대상 없음";
                return;
            }

            if (string.IsNullOrWhiteSpace(ssid))
            {
                ssid = profile;
            }

            _connectWifiTargetLabel.Text = "프로필: " + profile + " / SSID: " + ssid;
        }

        private void AddChromeUrlFromInput()
        {
            AddChromeUrl(_chromeUrlInputText.Text);
            _chromeUrlInputText.Text = "";
            _chromeUrlInputText.Focus();
        }

        private void AddChromeUrl(string value)
        {
            string url = NormalizeUrlForDisplay(value);
            if (string.IsNullOrWhiteSpace(url) || ActionValueCleaner.IsAudioStatusValue(url))
            {
                return;
            }

            if (GetChromeUrls().Any(item => string.Equals(item, url, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedChromeUrl = url;
                RenderChromeUrlChips();
                return;
            }

            _selectedChromeUrl = url;
            Button chip = CreateChromeUrlChip(url);
            _chromeUrlChipsPanel.Controls.Add(chip);
            RenderChromeUrlChips();
            if (!_loadingSelection)
            {
                CaptureCurrentZone();
            }
        }

        private void RemoveSelectedChromeUrl()
        {
            string selected = _selectedChromeUrl;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            foreach (Control control in _chromeUrlChipsPanel.Controls.Cast<Control>().ToList())
            {
                if (string.Equals(Convert.ToString(control.Tag), selected, StringComparison.OrdinalIgnoreCase))
                {
                    _chromeUrlChipsPanel.Controls.Remove(control);
                    control.Dispose();
                    break;
                }
            }

            _selectedChromeUrl = GetChromeUrls().FirstOrDefault();
            RenderChromeUrlChips();
            if (!_loadingSelection)
            {
                CaptureCurrentZone();
            }
        }

        private void SetChromeUrls(IEnumerable<string> urls)
        {
            ClearChildControls(_chromeUrlChipsPanel);
            _selectedChromeUrl = null;
            foreach (string url in (urls ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u) && !ActionValueCleaner.IsAudioStatusValue(u)))
            {
                string normalized = NormalizeUrlForDisplay(url);
                if (ActionValueCleaner.IsAudioStatusValue(normalized))
                {
                    continue;
                }

                if (!GetChromeUrls().Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrWhiteSpace(_selectedChromeUrl))
                    {
                        _selectedChromeUrl = normalized;
                    }
                    _chromeUrlChipsPanel.Controls.Add(CreateChromeUrlChip(normalized));
                }
            }
            RenderChromeUrlChips();
        }

        private List<string> GetChromeUrls()
        {
            return _chromeUrlChipsPanel.Controls
                .Cast<Control>()
                .Select(control => Convert.ToString(control.Tag))
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList();
        }

        private void TestSelectedChromeUrl()
        {
            string url = _selectedChromeUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(this, "테스트할 링크를 먼저 선택하세요.", "Chrome 링크 테스트", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                AppLauncher.OpenChromeUrls(new List<string> { url }, AppendLog);
            }
            catch (Exception ex)
            {
                AppendLog("Chrome 링크 테스트 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Chrome 링크 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Button CreateChromeUrlChip(string url)
        {
            Button chip = CreateValueChip(url, ShortenUrlForChip(url), string.Equals(_selectedChromeUrl, url, StringComparison.OrdinalIgnoreCase));
            chip.Click += delegate
            {
                _selectedChromeUrl = Convert.ToString(chip.Tag);
                RenderChromeUrlChips();
            };
            return chip;
        }

        private void RenderChromeUrlChips()
        {
            if (_chromeUrlChipsPanel == null)
            {
                return;
            }

            List<string> urls = GetChromeUrls();
            ClearChildControls(_chromeUrlChipsPanel);
            if (urls.Count == 0)
            {
                _selectedChromeUrl = null;
                AddEmptyChipHint(_chromeUrlChipsPanel, "등록된 링크 없음");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedChromeUrl) || !urls.Any(url => string.Equals(url, _selectedChromeUrl, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedChromeUrl = urls[0];
            }

            foreach (string url in urls)
            {
                _chromeUrlChipsPanel.Controls.Add(CreateChromeUrlChip(url));
            }
        }

        private void AddAppLaunchFromInput()
        {
            AddAppLaunch(_appLaunchInputText.Text);
            _appLaunchInputText.Text = "";
            _appLaunchInputText.Focus();
        }

        private void ShowAppPicker()
        {
            using (AppPickerForm picker = new AppPickerForm())
            {
                if (picker.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (string target in picker.SelectedTargets)
                    {
                        AddAppLaunch(target);
                    }
                }
            }
        }

        private void BrowseAppLaunchFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "실행할 앱 선택";
                dialog.Filter = "실행 파일 또는 바로가기|*.exe;*.lnk;*.bat;*.cmd|모든 파일|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (string fileName in dialog.FileNames)
                    {
                        AddAppLaunch(fileName);
                    }
                }
            }
        }

        private void RenderAppSearchResults(string query)
        {
            if (_appSearchResultsPanel == null)
            {
                return;
            }

            ClearChildControls(_appSearchResultsPanel);
            string term = (query ?? "").Trim();
            if (term.Length < 2)
            {
                AddEmptyChipHint(_appSearchResultsPanel, "앱 이름 2글자 이상 입력 또는 '앱 찾기' 사용");
                return;
            }

            List<AppSearchCandidate> candidates = AppLauncher.FindInstalledApps(term, 8);
            if (candidates.Count == 0)
            {
                AddEmptyChipHint(_appSearchResultsPanel, "검색 결과 없음");
                return;
            }

            foreach (AppSearchCandidate candidate in candidates)
            {
                Button chip = CreateValueChip(candidate.Target, ShortenText(candidate.Name, 28), false);
                if (_toolTip != null)
                {
                    _toolTip.SetToolTip(chip, candidate.Target);
                }
                chip.Click += delegate
                {
                    AddAppLaunch(Convert.ToString(chip.Tag));
                    _appLaunchInputText.Text = "";
                    RenderAppSearchResults("");
                };
                _appSearchResultsPanel.Controls.Add(chip);
            }
        }

        private void AddAppLaunch(string value)
        {
            string target = (value ?? "").Trim();
            if (string.IsNullOrWhiteSpace(target) || ActionValueCleaner.IsAudioStatusValue(target))
            {
                return;
            }

            if (GetAppLaunches().Any(item => string.Equals(item, target, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedAppLaunch = target;
                RenderAppLaunchChips();
                return;
            }

            _selectedAppLaunch = target;
            _appLaunchChipsPanel.Controls.Add(CreateAppLaunchChip(target));
            RenderAppLaunchChips();
            if (!_loadingSelection)
            {
                CaptureCurrentZone();
            }
        }

        private void RemoveSelectedAppLaunch()
        {
            string selected = _selectedAppLaunch;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            foreach (Control control in _appLaunchChipsPanel.Controls.Cast<Control>().ToList())
            {
                if (string.Equals(Convert.ToString(control.Tag), selected, StringComparison.OrdinalIgnoreCase))
                {
                    _appLaunchChipsPanel.Controls.Remove(control);
                    control.Dispose();
                    break;
                }
            }

            _selectedAppLaunch = GetAppLaunches().FirstOrDefault();
            RenderAppLaunchChips();
            if (!_loadingSelection)
            {
                CaptureCurrentZone();
            }
        }

        private void SetAppLaunches(IEnumerable<string> apps)
        {
            ClearChildControls(_appLaunchChipsPanel);
            _selectedAppLaunch = null;
            foreach (string app in (apps ?? new List<string>()).Where(a => !string.IsNullOrWhiteSpace(a) && !ActionValueCleaner.IsAudioStatusValue(a)))
            {
                string value = app.Trim();
                if (!GetAppLaunches().Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrWhiteSpace(_selectedAppLaunch))
                    {
                        _selectedAppLaunch = value;
                    }
                    _appLaunchChipsPanel.Controls.Add(CreateAppLaunchChip(value));
                }
            }
            RenderAppLaunchChips();
        }

        private List<string> GetAppLaunches()
        {
            return _appLaunchChipsPanel.Controls
                .Cast<Control>()
                .Select(control => Convert.ToString(control.Tag))
                .Where(app => !string.IsNullOrWhiteSpace(app))
                .ToList();
        }

        private void TestSelectedAppLaunch()
        {
            string target = _selectedAppLaunch;
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(this, "테스트할 앱을 먼저 선택하세요.", "앱 실행 테스트", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                AppLauncher.LaunchAppIfNotRunning(target, AppendLog);
                ShowTrayNotification("앱 실행 테스트", "실행 상태를 확인했습니다: " + ShortenAppTargetForChip(target));
            }
            catch (Exception ex)
            {
                AppendLog("앱 실행 테스트 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "앱 실행 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

    }
}
