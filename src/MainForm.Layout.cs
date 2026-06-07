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
        private void InitializeComponent()
        {
            Text = "위치 자동 실행";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1020, 680);
            Size = new Size(1180, 820);
            BackColor = UiBackground;
            ForeColor = UiText;
            Font = new Font("Malgun Gothic", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            _toolTip = null;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.BackColor = UiBackground;
            root.Padding = new Padding(8);
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            FlowLayoutPanel topBar = new FlowLayoutPanel();
            topBar.Dock = DockStyle.Top;
            topBar.AutoSize = false;
            topBar.Height = 54;
            topBar.BackColor = UiSurfaceMuted;
            topBar.Padding = new Padding(12, 8, 12, 8);
            topBar.WrapContents = false;
            topBar.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(topBar, 0, 0);

            Label appTitle = new Label();
            appTitle.Text = "위치 자동 실행";
            appTitle.AutoSize = true;
            appTitle.Font = new Font(Font.FontFamily, 12.25F, FontStyle.Bold, GraphicsUnit.Point);
            appTitle.ForeColor = UiAccentDark;
            appTitle.Margin = new Padding(2, 5, 18, 4);
            appTitle.Tag = "AccentTitle";
            topBar.Controls.Add(appTitle);

            _startupCheck = new CheckBox();
            _startupCheck.Visible = false;

            _startMinimizedCheck = new CheckBox();
            _startMinimizedCheck.Visible = false;

            Button settingsButton = CreateButton("설정");
            settingsButton.Click += delegate { OpenSettingsDialog(); };
            topBar.Controls.Add(settingsButton);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.BackColor = UiBorder;
            split.FixedPanel = FixedPanel.Panel1;
            split.Panel1MinSize = 300;
            split.SplitterWidth = 5;
            split.SplitterDistance = 320;
            root.Controls.Add(split, 0, 1);

            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.RowCount = 3;
            left.ColumnCount = 1;
            left.BackColor = UiSurface;
            left.Padding = new Padding(12, 10, 10, 10);
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            split.Panel1.Controls.Add(left);

            Label zonesLabel = new Label();
            zonesLabel.Text = "등록된 위치";
            zonesLabel.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold, GraphicsUnit.Point);
            zonesLabel.ForeColor = UiText;
            zonesLabel.AutoSize = true;
            zonesLabel.Margin = new Padding(0, 0, 0, 8);
            zonesLabel.Tag = "SidebarTitle";
            left.Controls.Add(zonesLabel, 0, 0);

            _zoneTabs = new TabControl();
            _zoneTabs.Dock = DockStyle.Fill;
            _zoneTabs.Multiline = true;
            _zoneTabs.SizeMode = TabSizeMode.Fixed;
            _zoneTabs.ItemSize = new Size(92, 28);
            left.Controls.Add(_zoneTabs, 0, 1);

            _allZonesTab = new TabPage("전체");
            _activeZonesTab = new TabPage("운영 중");
            _inactiveZonesTab = new TabPage("미운영");
            _zoneTabs.TabPages.Add(_allZonesTab);
            _zoneTabs.TabPages.Add(_activeZonesTab);
            _zoneTabs.TabPages.Add(_inactiveZonesTab);

            _allZoneList = CreateZoneListBox();
            _activeZoneList = CreateZoneListBox();
            _inactiveZoneList = CreateZoneListBox();
            _allZonesTab.Controls.Add(_allZoneList);
            _activeZonesTab.Controls.Add(_activeZoneList);
            _inactiveZonesTab.Controls.Add(_inactiveZoneList);

            FlowLayoutPanel zoneButtons = new FlowLayoutPanel();
            zoneButtons.Dock = DockStyle.Fill;
            zoneButtons.AutoSize = true;
            zoneButtons.WrapContents = true;
            zoneButtons.Margin = new Padding(0, 10, 0, 0);
            left.Controls.Add(zoneButtons, 0, 2);

            Button addZoneButton = CreateButton("새 위치");
            SetFixedButtonSize(addZoneButton, 82, 32);
            addZoneButton.Click += delegate
            {
                CaptureCurrentZone();
                ZoneRule zone = ZoneRule.CreateDefault("새 위치");
                _config.Zones.Add(zone);
                BindZoneList(zone.Id);
                AppendLog("위치가 추가되었습니다: " + zone.Name);
            };
            zoneButtons.Controls.Add(addZoneButton);

            Button currentZoneButton = CreateButton("현재 위치 등록");
            SetFixedButtonSize(currentZoneButton, 120, 32);
            currentZoneButton.Click += delegate { CreateZoneFromCurrentLocation(); };
            zoneButtons.Controls.Add(currentZoneButton);

            Button duplicateZoneButton = CreateButton("복제");
            SetFixedButtonSize(duplicateZoneButton, 74, 32);
            duplicateZoneButton.Click += delegate { DuplicateSelectedZone(); };
            zoneButtons.Controls.Add(duplicateZoneButton);

            Button removeZoneButton = CreateButton("삭제");
            SetFixedButtonSize(removeZoneButton, 74, 32);
            removeZoneButton.Click += delegate { RemoveSelectedZone(); };
            zoneButtons.Controls.Add(removeZoneButton);

            Panel detailHost = new Panel();
            detailHost.Dock = DockStyle.Fill;
            detailHost.AutoScroll = false;
            detailHost.BackColor = UiSurface;
            detailHost.Padding = new Padding(10, 6, 10, 8);
            split.Panel2.Controls.Add(detailHost);

            TableLayoutPanel detailShell = new TableLayoutPanel();
            detailShell.Dock = DockStyle.Fill;
            detailShell.BackColor = UiSurface;
            detailShell.ColumnCount = 1;
            detailShell.RowCount = 2;
            detailShell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailShell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detailHost.Controls.Add(detailShell);

            detailShell.Controls.Add(CreateSelectedZoneSummaryBar(), 0, 0);

            _detailTabs = new TabControl();
            _detailTabs.Dock = DockStyle.Fill;
            _detailTabs.SizeMode = TabSizeMode.Fixed;
            _detailTabs.ItemSize = new Size(116, 30);
            detailShell.Controls.Add(_detailTabs, 0, 1);

            _conditionTable = CreateDetailTable();
            _actionTable = CreateDetailTable();
            _appWatchTable = CreateDetailTable();
            _statusTable = CreateDetailTable();

            _detailTabs.TabPages.Add(CreateDetailTabPage("감지 조건", _conditionTable));
            _detailTabs.TabPages.Add(CreateDetailTabPage("실행 동작", _actionTable));
            _detailTabs.TabPages.Add(CreateDetailTabPage("앱 감시", _appWatchTable));
            _detailTabs.TabPages.Add(CreateDetailTabPage("상태/로그", _statusTable));

            AddSectionHeaderTo(_conditionTable, "위치 등록");

            _zoneEnabledCheck = new CheckBox();
            _zoneEnabledCheck.Text = "이 위치 운영";
            _zoneEnabledCheck.AutoSize = true;
            AddRowTo(_conditionTable, "상태", _zoneEnabledCheck);

            FlowLayoutPanel zoneSchedulePanel = new FlowLayoutPanel();
            zoneSchedulePanel.Dock = DockStyle.Fill;
            zoneSchedulePanel.AutoSize = true;
            zoneSchedulePanel.WrapContents = true;

            _runOnceStartupCheck = new CheckBox();
            _runOnceStartupCheck.Text = "시작 시 1회 실행";
            _runOnceStartupCheck.AutoSize = true;
            _runOnceStartupCheck.Margin = new Padding(4, 7, 16, 4);
            zoneSchedulePanel.Controls.Add(_runOnceStartupCheck);

            _monitoringCheck = new CheckBox();
            _monitoringCheck.Text = "지속 감시";
            _monitoringCheck.AutoSize = true;
            _monitoringCheck.Margin = new Padding(4, 7, 12, 4);
            zoneSchedulePanel.Controls.Add(_monitoringCheck);

            zoneSchedulePanel.Controls.Add(CreateInlineLabel("주기(초)"));
            _intervalInput = new NumericUpDown();
            _intervalInput.Minimum = 5;
            _intervalInput.Maximum = 3600;
            _intervalInput.Width = 72;
            _intervalInput.Margin = new Padding(4, 4, 14, 4);
            zoneSchedulePanel.Controls.Add(_intervalInput);

            AddRowTo(_conditionTable, "자동 실행", zoneSchedulePanel);

            _zoneNameText = new TextBox();
            _zoneNameText.Dock = DockStyle.Fill;
            _zoneNameText.TextChanged += delegate { UpdateSelectedZoneSummary(); };
            AddRowTo(_conditionTable, "위치 이름", _zoneNameText);

            _useCoordinatesCheck = new CheckBox();
            _useCoordinatesCheck.Text = "Windows 위치 좌표로 감지";
            _useCoordinatesCheck.AutoSize = true;
            _useCoordinatesCheck.CheckedChanged += delegate { SetCoordinateInputsEnabled(); };
            AddRowTo(_conditionTable, "감지 방식", _useCoordinatesCheck);

            FlowLayoutPanel coordinatesPanel = new FlowLayoutPanel();
            coordinatesPanel.Dock = DockStyle.Fill;
            coordinatesPanel.AutoSize = true;
            coordinatesPanel.WrapContents = true;

            coordinatesPanel.Controls.Add(CreateInlineLabel("위도"));
            _latitudeText = new TextBox();
            _latitudeText.Width = 120;
            coordinatesPanel.Controls.Add(_latitudeText);

            coordinatesPanel.Controls.Add(CreateInlineLabel("경도"));
            _longitudeText = new TextBox();
            _longitudeText.Width = 120;
            coordinatesPanel.Controls.Add(_longitudeText);

            coordinatesPanel.Controls.Add(CreateInlineLabel("반경(m)"));
            _radiusInput = new NumericUpDown();
            _radiusInput.Minimum = 10;
            _radiusInput.Maximum = 100000;
            _radiusInput.Value = 200;
            _radiusInput.Width = 88;
            coordinatesPanel.Controls.Add(_radiusInput);

            Button currentLocationButton = CreateButton("현재 좌표 사용");
            currentLocationButton.Click += delegate { FillSelectedZoneFromCurrentLocation(); };
            coordinatesPanel.Controls.Add(currentLocationButton);

            AddRowTo(_conditionTable, "좌표", coordinatesPanel);

            _wifiChoicesPanel = new FlowLayoutPanel();
            _wifiChoicesPanel.Dock = DockStyle.Fill;
            _wifiChoicesPanel.AutoScroll = true;
            _wifiChoicesPanel.WrapContents = true;
            _wifiChoicesPanel.Height = 56;
            _wifiChoicesPanel.BorderStyle = BorderStyle.None;
            _wifiChoicesPanel.BackColor = UiSurfaceMuted;
            _wifiChoicesPanel.Padding = new Padding(7, 6, 7, 5);
            AddRowTo(_conditionTable, "근처 Wi-Fi", _wifiChoicesPanel);

            _selectedWifiLabel = new Label();
            _selectedWifiLabel.Dock = DockStyle.Fill;
            _selectedWifiLabel.AutoSize = true;
            _selectedWifiLabel.MaximumSize = new Size(760, 0);
            _selectedWifiLabel.Text = "선택된 Wi-Fi 없음";
            AddRowTo(_conditionTable, "선택됨", _selectedWifiLabel);

            FlowLayoutPanel wifiButtons = new FlowLayoutPanel();
            wifiButtons.AutoSize = true;
            Button useVisibleWifiButton = CreateButton("Wi-Fi 후보 새로고침");
            useVisibleWifiButton.Click += delegate { FillSelectedZoneFromVisibleNetworks(); };
            wifiButtons.Controls.Add(useVisibleWifiButton);
            AddRowTo(_conditionTable, "", wifiButtons);

            _requireAllSsidsCheck = new CheckBox();
            _requireAllSsidsCheck.Text = "위 Wi-Fi가 모두 보일 때만 감지";
            _requireAllSsidsCheck.AutoSize = true;
            AddRowTo(_conditionTable, "Wi-Fi 조건", _requireAllSsidsCheck);

            AddSectionHeaderTo(_actionTable, "실행할 동작");

            _connectWifiCheck = new CheckBox();
            _connectWifiCheck.Text = "위치 진입 시 특정 Wi-Fi 연결 시도";
            _connectWifiCheck.AutoSize = true;
            _connectWifiCheck.CheckedChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                if (_connectWifiCheck.Checked && string.IsNullOrWhiteSpace(_connectProfileText.Text) && _lastVisibleNetworks.Count > 0)
                {
                    SetConnectWifiTarget(_lastVisibleNetworks[0].Ssid);
                }
                CaptureCurrentZone();
            };
            AddRowTo(_actionTable, "Wi-Fi 연결", _connectWifiCheck);

            _connectWifiChoicesPanel = new FlowLayoutPanel();
            _connectWifiChoicesPanel.Dock = DockStyle.Fill;
            _connectWifiChoicesPanel.AutoScroll = true;
            _connectWifiChoicesPanel.WrapContents = true;
            _connectWifiChoicesPanel.Height = 54;
            _connectWifiChoicesPanel.BorderStyle = BorderStyle.None;
            _connectWifiChoicesPanel.BackColor = UiSurfaceMuted;
            _connectWifiChoicesPanel.Padding = new Padding(7, 6, 7, 5);
            AddRowTo(_actionTable, "연결 대상", _connectWifiChoicesPanel);

            _connectWifiTargetLabel = new Label();
            _connectWifiTargetLabel.Dock = DockStyle.Fill;
            _connectWifiTargetLabel.AutoSize = true;
            _connectWifiTargetLabel.MaximumSize = new Size(760, 0);
            _connectWifiTargetLabel.Text = "연결 대상 없음";
            AddRowTo(_actionTable, "연결 선택됨", _connectWifiTargetLabel);

            _connectProfileText = new TextBox();
            _connectProfileText.Dock = DockStyle.Fill;
            _connectProfileText.TextChanged += delegate { UpdateConnectWifiTargetLabel(); };
            AddRowTo(_actionTable, "프로필 이름", _connectProfileText);

            _connectSsidText = new TextBox();
            _connectSsidText.Dock = DockStyle.Fill;
            _connectSsidText.TextChanged += delegate { UpdateConnectWifiTargetLabel(); };
            AddRowTo(_actionTable, "연결 SSID", _connectSsidText);

            FlowLayoutPanel connectButtons = new FlowLayoutPanel();
            connectButtons.AutoSize = true;
            Button testWifiConnectButton = CreateButton("선택 Wi-Fi 연결 테스트");
            testWifiConnectButton.Click += delegate { TestSelectedWifiConnection(); };
            connectButtons.Controls.Add(testWifiConnectButton);
            AddRowTo(_actionTable, "", connectButtons);

            _audioActionCombo = new ComboBox();
            _audioActionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _audioActionCombo.Items.AddRange(new object[] { "안 함", "음소거", "음소거 해제" });
            AddRowTo(_actionTable, "소리", _audioActionCombo);

            TableLayoutPanel chromePanel = new TableLayoutPanel();
            chromePanel.Dock = DockStyle.Fill;
            chromePanel.AutoSize = true;
            chromePanel.ColumnCount = 1;
            chromePanel.RowCount = 4;
            chromePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chromePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chromePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            chromePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel chromeInputPanel = new FlowLayoutPanel();
            chromeInputPanel.Dock = DockStyle.Fill;
            chromeInputPanel.AutoSize = true;
            chromeInputPanel.WrapContents = false;
            chromeInputPanel.Margin = new Padding(0, 0, 0, 6);
            _chromeUrlInputText = new TextBox();
            _chromeUrlInputText.Width = 390;
            _chromeUrlInputText.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddChromeUrlFromInput();
                }
            };
            chromeInputPanel.Controls.Add(_chromeUrlInputText);
            Button addChromeUrlButton = CreateButton("링크 추가");
            SetFixedButtonSize(addChromeUrlButton, 88, 30);
            addChromeUrlButton.Click += delegate { AddChromeUrlFromInput(); };
            chromeInputPanel.Controls.Add(addChromeUrlButton);
            chromePanel.Controls.Add(chromeInputPanel, 0, 0);

            _chromeUrlChipsPanel = CreateChipPanel(68);
            chromePanel.Controls.Add(_chromeUrlChipsPanel, 0, 1);

            FlowLayoutPanel chromeButtons = new FlowLayoutPanel();
            chromeButtons.AutoSize = true;
            chromeButtons.Margin = new Padding(0, 0, 0, 4);
            Button removeChromeUrlButton = CreateButton("선택 삭제");
            SetFixedButtonSize(removeChromeUrlButton, 88, 30);
            removeChromeUrlButton.Click += delegate { RemoveSelectedChromeUrl(); };
            chromeButtons.Controls.Add(removeChromeUrlButton);
            Button testChromeUrlButton = CreateButton("선택 링크 테스트");
            SetFixedButtonSize(testChromeUrlButton, 120, 30);
            testChromeUrlButton.Click += delegate { TestSelectedChromeUrl(); };
            chromeButtons.Controls.Add(testChromeUrlButton);
            Button addChromeSampleButton = CreateButton("ChatGPT 웹 추가");
            SetFixedButtonSize(addChromeSampleButton, 118, 30);
            addChromeSampleButton.Click += delegate { AddChromeUrl("https://chatgpt.com/"); };
            chromeButtons.Controls.Add(addChromeSampleButton);
            chromePanel.Controls.Add(chromeButtons, 0, 2);

            Label chromeRunHintLabel = new Label();
            chromeRunHintLabel.Dock = DockStyle.Fill;
            chromeRunHintLabel.AutoSize = true;
            chromeRunHintLabel.Text = "Wi-Fi 연결 시도가 켜진 경우, 연결 성공 후 등록된 링크가 각각 Chrome 탭으로 열립니다.";
            chromeRunHintLabel.ForeColor = UiTextMuted;
            chromeRunHintLabel.Tag = "Muted";
            chromeRunHintLabel.Margin = new Padding(2, 0, 0, 0);
            chromePanel.Controls.Add(chromeRunHintLabel, 0, 3);
            AddRowTo(_actionTable, "Chrome 링크", chromePanel);

            TableLayoutPanel appPanel = new TableLayoutPanel();
            appPanel.Dock = DockStyle.Fill;
            appPanel.AutoSize = true;
            appPanel.ColumnCount = 1;
            appPanel.RowCount = 5;
            appPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel appInputPanel = new FlowLayoutPanel();
            appInputPanel.Dock = DockStyle.Fill;
            appInputPanel.AutoSize = true;
            appInputPanel.WrapContents = true;
            appInputPanel.Margin = new Padding(0, 0, 0, 6);
            _appLaunchInputText = new TextBox();
            _appLaunchInputText.Width = 300;
            _appLaunchInputText.TextChanged += delegate { RenderAppSearchResults(_appLaunchInputText.Text); };
            _appLaunchInputText.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    AddAppLaunchFromInput();
                }
            };
            appInputPanel.Controls.Add(_appLaunchInputText);
            Button addAppLaunchButton = CreateButton("앱 추가");
            SetFixedButtonSize(addAppLaunchButton, 78, 30);
            addAppLaunchButton.Click += delegate { AddAppLaunchFromInput(); };
            appInputPanel.Controls.Add(addAppLaunchButton);
            Button findAppButton = CreateButton("앱 찾기");
            SetFixedButtonSize(findAppButton, 78, 30);
            findAppButton.Click += delegate { ShowAppPicker(); };
            appInputPanel.Controls.Add(findAppButton);
            Button browseAppButton = CreateButton("파일 선택");
            SetFixedButtonSize(browseAppButton, 86, 30);
            browseAppButton.Click += delegate { BrowseAppLaunchFile(); };
            appInputPanel.Controls.Add(browseAppButton);

            _appSearchResultsPanel = CreateChipPanel(66);

            FlowLayoutPanel appButtons = new FlowLayoutPanel();
            appButtons.AutoSize = true;
            appButtons.Margin = new Padding(0, 0, 0, 6);
            Button addChatGptButton = CreateButton("ChatGPT");
            SetFixedButtonSize(addChatGptButton, 86, 30);
            addChatGptButton.Click += delegate { AddAppLaunch("ChatGPT"); };
            appButtons.Controls.Add(addChatGptButton);
            Button addObsidianButton = CreateButton("Obsidian");
            SetFixedButtonSize(addObsidianButton, 86, 30);
            addObsidianButton.Click += delegate { AddAppLaunch("Obsidian"); };
            appButtons.Controls.Add(addObsidianButton);
            Button addDockerButton = CreateButton("Docker");
            SetFixedButtonSize(addDockerButton, 86, 30);
            addDockerButton.Click += delegate { AddAppLaunch("Docker Desktop"); };
            appButtons.Controls.Add(addDockerButton);

            _appLaunchChipsPanel = CreateChipPanel(78);

            FlowLayoutPanel appListButtons = new FlowLayoutPanel();
            appListButtons.AutoSize = true;
            appListButtons.Margin = new Padding(0);
            Button removeAppLaunchButton = CreateButton("선택 삭제");
            SetFixedButtonSize(removeAppLaunchButton, 88, 30);
            removeAppLaunchButton.Click += delegate { RemoveSelectedAppLaunch(); };
            appListButtons.Controls.Add(removeAppLaunchButton);
            Button testAppLaunchButton = CreateButton("선택 앱 테스트");
            SetFixedButtonSize(testAppLaunchButton, 112, 30);
            testAppLaunchButton.Click += delegate { TestSelectedAppLaunch(); };
            appListButtons.Controls.Add(testAppLaunchButton);

            appPanel.Controls.Add(appInputPanel, 0, 0);
            appPanel.Controls.Add(_appSearchResultsPanel, 0, 1);
            appPanel.Controls.Add(appButtons, 0, 2);
            appPanel.Controls.Add(_appLaunchChipsPanel, 0, 3);
            appPanel.Controls.Add(appListButtons, 0, 4);
            AddRowTo(_actionTable, "앱 실행", appPanel);
            RenderAppSearchResults("");

            TableLayoutPanel commandPanel = new TableLayoutPanel();
            commandPanel.Dock = DockStyle.Fill;
            commandPanel.AutoSize = true;
            commandPanel.ColumnCount = 1;
            commandPanel.RowCount = 2;
            commandPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            commandPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel commandHelpPanel = new FlowLayoutPanel();
            commandHelpPanel.AutoSize = true;
            commandHelpPanel.Margin = new Padding(0, 0, 0, 6);
            Button commandHelpButton = CreateButton("? 명령어 도움말");
            commandHelpButton.Click += delegate { ShowCommandHelp(); };
            commandHelpPanel.Controls.Add(commandHelpButton);
            Button commandExampleButton = CreateButton("예시 넣기");
            commandExampleButton.Click += delegate { InsertCommandExample(); };
            commandHelpPanel.Controls.Add(commandExampleButton);

            _commandsText = new TextBox();
            _commandsText.Dock = DockStyle.Fill;
            _commandsText.Multiline = true;
            _commandsText.ScrollBars = ScrollBars.Vertical;
            _commandsText.Height = 72;
            commandPanel.Controls.Add(commandHelpPanel, 0, 0);
            commandPanel.Controls.Add(_commandsText, 0, 1);
            AddRowTo(_actionTable, "고급 명령어", commandPanel);

            AddSectionHeaderTo(_appWatchTable, "앱 감시");

            TableLayoutPanel appWatchListPanel = new TableLayoutPanel();
            appWatchListPanel.Dock = DockStyle.Fill;
            appWatchListPanel.AutoSize = true;
            appWatchListPanel.ColumnCount = 1;
            appWatchListPanel.RowCount = 2;
            appWatchListPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appWatchListPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _appWatchItemsPanel = CreateChipPanel(150);
            appWatchListPanel.Controls.Add(_appWatchItemsPanel, 0, 0);

            FlowLayoutPanel appWatchListButtons = new FlowLayoutPanel();
            appWatchListButtons.Dock = DockStyle.Fill;
            appWatchListButtons.AutoSize = true;
            appWatchListButtons.WrapContents = true;
            appWatchListButtons.Margin = new Padding(0);

            Button addAppWatchButton = CreateButton("새 감시");
            SetFixedButtonSize(addAppWatchButton, 78, 30);
            addAppWatchButton.Click += delegate { AddNewAppWatchItem(); };
            appWatchListButtons.Controls.Add(addAppWatchButton);

            Button findAppWatchButton = CreateButton("앱 찾기");
            SetFixedButtonSize(findAppWatchButton, 78, 30);
            findAppWatchButton.Click += delegate { ShowAppWatchPicker(); };
            appWatchListButtons.Controls.Add(findAppWatchButton);

            Button browseAppWatchButton = CreateButton("파일 선택");
            SetFixedButtonSize(browseAppWatchButton, 86, 30);
            browseAppWatchButton.Click += delegate { BrowseAppWatchFile(); };
            appWatchListButtons.Controls.Add(browseAppWatchButton);

            Button removeAppWatchButton = CreateButton("선택 삭제");
            SetFixedButtonSize(removeAppWatchButton, 88, 30);
            removeAppWatchButton.Click += delegate { RemoveSelectedAppWatchItem(); };
            appWatchListButtons.Controls.Add(removeAppWatchButton);

            appWatchListPanel.Controls.Add(appWatchListButtons, 0, 1);
            AddRowTo(_appWatchTable, "등록 목록", appWatchListPanel);

            FlowLayoutPanel appWatchStatePanel = new FlowLayoutPanel();
            appWatchStatePanel.Dock = DockStyle.Fill;
            appWatchStatePanel.AutoSize = true;
            appWatchStatePanel.WrapContents = true;
            appWatchStatePanel.Margin = new Padding(0);

            _appWatchEnabledCheck = new CheckBox();
            _appWatchEnabledCheck.Appearance = Appearance.Normal;
            _appWatchEnabledCheck.AutoSize = true;
            _appWatchEnabledCheck.Margin = new Padding(4);
            StyleSwitchToggle(_appWatchEnabledCheck, "앱 감시 사용", "앱 감시 미사용");
            appWatchStatePanel.Controls.Add(_appWatchEnabledCheck);

            _appWatchRequireWindowCheck = new CheckBox();
            _appWatchRequireWindowCheck.Appearance = Appearance.Normal;
            _appWatchRequireWindowCheck.AutoSize = true;
            _appWatchRequireWindowCheck.Margin = new Padding(4);
            StyleSwitchToggle(_appWatchRequireWindowCheck, "표시 창 필요", "표시 창 무시");
            appWatchStatePanel.Controls.Add(_appWatchRequireWindowCheck);

            Label appWatchStateHint = new Label();
            appWatchStateHint.AutoSize = true;
            appWatchStateHint.Text = "ON이면 프로세스가 꺼졌을 때 다시 실행합니다. 창 ON은 표시 창이 없을 때도 다시 엽니다.";
            appWatchStateHint.ForeColor = UiTextMuted;
            appWatchStateHint.Tag = "Muted";
            appWatchStateHint.Margin = new Padding(8, 9, 0, 4);
            appWatchStatePanel.Controls.Add(appWatchStateHint);
            AddRowTo(_appWatchTable, "선택 항목", appWatchStatePanel);

            TableLayoutPanel appWatchTargetPanel = new TableLayoutPanel();
            appWatchTargetPanel.Dock = DockStyle.Fill;
            appWatchTargetPanel.AutoSize = true;
            appWatchTargetPanel.ColumnCount = 1;
            appWatchTargetPanel.RowCount = 2;
            appWatchTargetPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            appWatchTargetPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            FlowLayoutPanel appWatchTargetInputPanel = new FlowLayoutPanel();
            appWatchTargetInputPanel.Dock = DockStyle.Fill;
            appWatchTargetInputPanel.AutoSize = true;
            appWatchTargetInputPanel.WrapContents = true;
            appWatchTargetInputPanel.Margin = new Padding(0, 0, 0, 6);

            _appWatchTargetText = new TextBox();
            _appWatchTargetText.Width = 330;
            appWatchTargetInputPanel.Controls.Add(_appWatchTargetText);

            Button findAppWatchTargetButton = CreateButton("앱 찾기");
            SetFixedButtonSize(findAppWatchTargetButton, 78, 30);
            findAppWatchTargetButton.Click += delegate { ShowAppWatchPicker(); };
            appWatchTargetInputPanel.Controls.Add(findAppWatchTargetButton);

            Button browseAppWatchTargetButton = CreateButton("파일 선택");
            SetFixedButtonSize(browseAppWatchTargetButton, 86, 30);
            browseAppWatchTargetButton.Click += delegate { BrowseAppWatchFile(); };
            appWatchTargetInputPanel.Controls.Add(browseAppWatchTargetButton);

            Button testAppWatchLaunchButton = CreateButton("실행 테스트");
            SetFixedButtonSize(testAppWatchLaunchButton, 92, 30);
            testAppWatchLaunchButton.Click += delegate { TestAppWatchLaunchTarget(); };
            appWatchTargetInputPanel.Controls.Add(testAppWatchLaunchButton);

            Label appWatchTargetHint = new Label();
            appWatchTargetHint.Dock = DockStyle.Fill;
            appWatchTargetHint.AutoSize = true;
            appWatchTargetHint.MaximumSize = new Size(760, 0);
            appWatchTargetHint.Text = "앱 이름, 실행 파일, 바로가기, 앱 프로토콜을 사용할 수 있습니다.";
            appWatchTargetHint.ForeColor = UiTextMuted;
            appWatchTargetHint.Tag = "Muted";
            appWatchTargetHint.Margin = new Padding(2, 0, 0, 0);

            appWatchTargetPanel.Controls.Add(appWatchTargetInputPanel, 0, 0);
            appWatchTargetPanel.Controls.Add(appWatchTargetHint, 0, 1);
            AddRowTo(_appWatchTable, "실행 대상", appWatchTargetPanel);

            _appWatchProcessText = new TextBox();
            _appWatchProcessText.Dock = DockStyle.Fill;
            AddRowTo(_appWatchTable, "프로세스 이름", _appWatchProcessText);

            FlowLayoutPanel appWatchIntervalPanel = new FlowLayoutPanel();
            appWatchIntervalPanel.Dock = DockStyle.Fill;
            appWatchIntervalPanel.AutoSize = true;
            appWatchIntervalPanel.WrapContents = true;

            _appWatchIntervalInput = new NumericUpDown();
            _appWatchIntervalInput.Minimum = 1;
            _appWatchIntervalInput.Maximum = 10080;
            _appWatchIntervalInput.Width = 78;
            appWatchIntervalPanel.Controls.Add(_appWatchIntervalInput);

            _appWatchIntervalUnitCombo = new ComboBox();
            _appWatchIntervalUnitCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _appWatchIntervalUnitCombo.Width = 88;
            _appWatchIntervalUnitCombo.Items.AddRange(new object[] { "분", "시간" });
            appWatchIntervalPanel.Controls.Add(_appWatchIntervalUnitCombo);
            AddRowTo(_appWatchTable, "체크 주기", appWatchIntervalPanel);

            _appWatchStatusLabel = new Label();
            _appWatchStatusLabel.Dock = DockStyle.Fill;
            _appWatchStatusLabel.AutoSize = true;
            _appWatchStatusLabel.MaximumSize = new Size(760, 0);
            _appWatchStatusLabel.Text = "아직 확인 전입니다.";
            AddRowTo(_appWatchTable, "최근 확인", _appWatchStatusLabel);

            FlowLayoutPanel appWatchButtons = new FlowLayoutPanel();
            appWatchButtons.AutoSize = true;
            Button testAppWatchStatusButton = CreateButton("상태 테스트");
            SetFixedButtonSize(testAppWatchStatusButton, 92, 30);
            testAppWatchStatusButton.Click += delegate { TestAppWatchStatusOnly(); };
            appWatchButtons.Controls.Add(testAppWatchStatusButton);
            Button testAppWatchRestartButton = CreateButton("재실행 테스트");
            SetFixedButtonSize(testAppWatchRestartButton, 104, 30);
            testAppWatchRestartButton.Click += delegate { TestAppWatchRestart(); };
            appWatchButtons.Controls.Add(testAppWatchRestartButton);
            AddRowTo(_appWatchTable, "", appWatchButtons);

            AddSectionHeaderTo(_statusTable, "현재 상태");

            _activeZonesLabel = CreateStatusValueLabel("아직 확인 전입니다.");
            AddRowTo(_statusTable, "조건 일치", _activeZonesLabel);

            _currentLocationLabel = CreateStatusValueLabel("아직 위치 확인 전입니다.");
            AddRowTo(_statusTable, "현재 위치", _currentLocationLabel);

            _visibleNetworksLabel = CreateStatusValueLabel("아직 Wi-Fi 확인 전입니다.");
            AddRowTo(_statusTable, "보이는 Wi-Fi", _visibleNetworksLabel);

            _lastActionLabel = CreateStatusValueLabel("아직 실행된 동작이 없습니다.");
            AddRowTo(_statusTable, "최근 동작", _lastActionLabel);

            _lastAppWatchLabel = CreateStatusValueLabel("아직 앱 감시 결과가 없습니다.");
            AddRowTo(_statusTable, "앱 감시", _lastAppWatchLabel);

            _backgroundProcessLabel = CreateStatusValueLabel("백그라운드 상태 확인 중입니다.");
            AddRowTo(_statusTable, "백그라운드", _backgroundProcessLabel);

            AddSectionHeaderTo(_statusTable, "실행 로그");

            _recentLogLabel = CreateStatusValueLabel("아직 기록된 이벤트가 없습니다.");
            AddRowTo(_statusTable, "최근 이벤트", _recentLogLabel);

            _logText = new TextBox();
            _logText.Dock = DockStyle.Fill;
            _logText.Multiline = true;
            _logText.ReadOnly = true;
            _logText.ScrollBars = ScrollBars.Vertical;
            _logText.BorderStyle = BorderStyle.None;
            _logText.Height = 230;
            _logText.Margin = new Padding(0, 5, 0, 10);
            AddRowTo(_statusTable, "전체 로그", _logText);

            ApplyTheme(root);
            StyleLogBox();

            _monitoringCheck.CheckedChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ResetScanTimer();
                ZoneRule selected = GetSelectedZone();
                string name = selected == null ? "선택 위치" : selected.Name;
                AppendLog(_monitoringCheck.Checked ? "지속 감시를 켰습니다: " + name : "지속 감시를 껐습니다: " + name);
            };

            _runOnceStartupCheck.CheckedChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                if (_runOnceStartupCheck.Checked && !_startupCheck.Checked)
                {
                    _startupCheck.Checked = true;
                }
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ZoneRule selected = GetSelectedZone();
                string name = selected == null ? "선택 위치" : selected.Name;
                AppendLog(_runOnceStartupCheck.Checked ? "시작 시 1회 실행을 켰습니다: " + name : "시작 시 1회 실행을 껐습니다: " + name);
            };

            _intervalInput.ValueChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ResetScanTimer();
            };

            _appWatchEnabledCheck.CheckedChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                StyleSwitchToggle(_appWatchEnabledCheck, "앱 감시 사용", "앱 감시 미사용");
                ClearSelectedAppWatchStatus();
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ResetScanTimer();
                ResetAppWatchTimer();
                ZoneRule selected = GetSelectedZone();
                AppWatchItem item = GetSelectedAppWatchItem(selected);
                string name = BuildAppWatchLogName(selected, item);
                AppendLog(_appWatchEnabledCheck.Checked ? "앱 감시를 시작했습니다: " + name : "앱 감시를 껐습니다: " + name);
                RenderAppWatchItems();
                if (_appWatchEnabledCheck.Checked)
                {
                    RunAppWatchCheckWhenZoneIsActive(selected, item, "앱 감시 시작 확인");
                }
            };

            _appWatchRequireWindowCheck.CheckedChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                StyleSwitchToggle(_appWatchRequireWindowCheck, "표시 창 필요", "표시 창 무시");
                ClearSelectedAppWatchStatus();
                CaptureCurrentZone();
                CaptureGlobalSettings();
                RenderAppWatchItems();
                RefreshSelectedAppWatchStatusLabel();
            };

            _appWatchTargetText.Leave += delegate
            {
                ClearSelectedAppWatchStatus();
                FillAppWatchProcessNameFromTarget(false);
                CaptureCurrentZone();
                CaptureGlobalSettings();
                RenderAppWatchItems();
                RefreshSelectedAppWatchStatusLabel();
            };

            _appWatchProcessText.TextChanged += delegate
            {
                if (!_loadingSelection)
                {
                    ClearSelectedAppWatchStatus();
                    CaptureCurrentZone();
                    CaptureGlobalSettings();
                    RenderAppWatchItems();
                    RefreshSelectedAppWatchStatusLabel();
                }
            };

            _appWatchIntervalInput.ValueChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                ClearSelectedAppWatchStatus();
                CaptureCurrentZone();
                CaptureGlobalSettings();
                RenderAppWatchItems();
                ResetScanTimer();
                ResetAppWatchTimer();
            };

            _appWatchIntervalUnitCombo.SelectedIndexChanged += delegate
            {
                UpdateAppWatchIntervalLimits();
                if (_loadingSelection)
                {
                    return;
                }
                ClearSelectedAppWatchStatus();
                CaptureCurrentZone();
                CaptureGlobalSettings();
                RenderAppWatchItems();
                ResetScanTimer();
                ResetAppWatchTimer();
            };
        }

    }
}
