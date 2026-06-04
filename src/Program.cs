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
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool startMinimized = args != null && args.Any(a => string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));
            bool startedFromWindowsStartup = args != null && args.Any(a => string.Equals(a, "--startup", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(startMinimized, startedFromWindowsStartup));
        }
    }

    public sealed class AppConfig
    {
        public bool MonitoringEnabled { get; set; }
        public bool? RunOnceAtStartup { get; set; }
        public int ScanIntervalSeconds { get; set; }
        public bool StartMinimized { get; set; }
        public bool AppWatchEnabled { get; set; }
        public string AppWatchProcessName { get; set; }
        public string AppWatchLaunchTarget { get; set; }
        public int AppWatchIntervalValue { get; set; }
        public string AppWatchIntervalUnit { get; set; }
        public List<ZoneRule> Zones { get; set; }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                MonitoringEnabled = false,
                RunOnceAtStartup = true,
                ScanIntervalSeconds = 30,
                StartMinimized = true,
                AppWatchEnabled = false,
                AppWatchProcessName = "",
                AppWatchLaunchTarget = "",
                AppWatchIntervalValue = 5,
                AppWatchIntervalUnit = "Minutes",
                Zones = new List<ZoneRule>
                {
                    ZoneRule.CreateDefault("내 위치")
                }
            };
        }

        public void Normalize()
        {
            if (!RunOnceAtStartup.HasValue)
            {
                RunOnceAtStartup = true;
                MonitoringEnabled = false;
            }

            if (ScanIntervalSeconds < 5)
            {
                ScanIntervalSeconds = 30;
            }

            if (AppWatchProcessName == null)
            {
                AppWatchProcessName = "";
            }

            if (AppWatchLaunchTarget == null)
            {
                AppWatchLaunchTarget = "";
            }

            if (!string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase))
            {
                AppWatchIntervalUnit = "Minutes";
            }

            int maxAppWatchInterval = string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            if (AppWatchIntervalValue <= 0)
            {
                AppWatchIntervalValue = 5;
            }
            else if (AppWatchIntervalValue > maxAppWatchInterval)
            {
                AppWatchIntervalValue = maxAppWatchInterval;
            }

            if (Zones == null)
            {
                Zones = new List<ZoneRule>();
            }

            foreach (ZoneRule zone in Zones)
            {
                if (!zone.RunOnceAtStartup.HasValue)
                {
                    zone.RunOnceAtStartup = RunOnceAtStartup.GetValueOrDefault(true);
                }

                if (!zone.MonitoringEnabled.HasValue)
                {
                    zone.MonitoringEnabled = MonitoringEnabled;
                }

                if (zone.ScanIntervalSeconds < 5)
                {
                    zone.ScanIntervalSeconds = ScanIntervalSeconds;
                }

                zone.Normalize();
            }

            if (AppWatchEnabled
                && Zones.Count > 0
                && !string.IsNullOrWhiteSpace(AppWatchLaunchTarget)
                && !Zones.Any(z => z.AppWatchEnabled.GetValueOrDefault(false)))
            {
                ZoneRule firstZone = Zones.FirstOrDefault(z => z.Enabled) ?? Zones[0];
                firstZone.AppWatchEnabled = true;
                firstZone.AppWatchLaunchTarget = AppWatchLaunchTarget;
                firstZone.AppWatchProcessName = AppWatchProcessName;
                firstZone.AppWatchIntervalValue = AppWatchIntervalValue;
                firstZone.AppWatchIntervalUnit = AppWatchIntervalUnit;
                firstZone.Normalize();
            }
        }
    }

    public sealed class ZoneRule
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public bool? RunOnceAtStartup { get; set; }
        public bool? MonitoringEnabled { get; set; }
        public int ScanIntervalSeconds { get; set; }
        public bool? AppWatchEnabled { get; set; }
        public string AppWatchProcessName { get; set; }
        public string AppWatchLaunchTarget { get; set; }
        public int AppWatchIntervalValue { get; set; }
        public string AppWatchIntervalUnit { get; set; }
        public bool UseCoordinates { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int RadiusMeters { get; set; }
        public bool RequireAllSsids { get; set; }
        public List<string> NearbySsids { get; set; }
        public bool? ConnectWifiEnabled { get; set; }
        public string ConnectProfile { get; set; }
        public string ConnectSsid { get; set; }
        public string AudioAction { get; set; }
        public List<string> ChromeUrls { get; set; }
        public List<string> AppLaunches { get; set; }
        public List<string> Commands { get; set; }

        public static ZoneRule CreateDefault(string name)
        {
            return new ZoneRule
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                Enabled = true,
                RunOnceAtStartup = true,
                MonitoringEnabled = false,
                ScanIntervalSeconds = 30,
                AppWatchEnabled = false,
                AppWatchProcessName = "",
                AppWatchLaunchTarget = "",
                AppWatchIntervalValue = 5,
                AppWatchIntervalUnit = "Minutes",
                UseCoordinates = false,
                Latitude = 0,
                Longitude = 0,
                RadiusMeters = 200,
                RequireAllSsids = false,
                NearbySsids = new List<string>(),
                ConnectWifiEnabled = false,
                ConnectProfile = "",
                ConnectSsid = "",
                AudioAction = "None",
                ChromeUrls = new List<string>(),
                AppLaunches = new List<string>(),
                Commands = new List<string>()
            };
        }

        public ZoneRule Clone()
        {
            return new ZoneRule
            {
                Id = Id,
                Name = Name,
                Enabled = Enabled,
                RunOnceAtStartup = RunOnceAtStartup,
                MonitoringEnabled = MonitoringEnabled,
                ScanIntervalSeconds = ScanIntervalSeconds,
                AppWatchEnabled = AppWatchEnabled,
                AppWatchProcessName = AppWatchProcessName,
                AppWatchLaunchTarget = AppWatchLaunchTarget,
                AppWatchIntervalValue = AppWatchIntervalValue,
                AppWatchIntervalUnit = AppWatchIntervalUnit,
                UseCoordinates = UseCoordinates,
                Latitude = Latitude,
                Longitude = Longitude,
                RadiusMeters = RadiusMeters,
                RequireAllSsids = RequireAllSsids,
                NearbySsids = NearbySsids == null ? new List<string>() : new List<string>(NearbySsids),
                ConnectWifiEnabled = ConnectWifiEnabled,
                ConnectProfile = ConnectProfile,
                ConnectSsid = ConnectSsid,
                AudioAction = AudioAction,
                ChromeUrls = ChromeUrls == null ? new List<string>() : new List<string>(ChromeUrls),
                AppLaunches = AppLaunches == null ? new List<string>() : new List<string>(AppLaunches),
                Commands = Commands == null ? new List<string>() : new List<string>(Commands)
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                Id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                Name = "이름 없는 위치";
            }
            else if (string.Equals(Name, "My first zone", StringComparison.OrdinalIgnoreCase))
            {
                Name = "내 위치";
            }
            else if (string.Equals(Name, "New zone", StringComparison.OrdinalIgnoreCase))
            {
                Name = "새 위치";
            }

            if (RadiusMeters <= 0)
            {
                RadiusMeters = 200;
            }

            if (!RunOnceAtStartup.HasValue)
            {
                RunOnceAtStartup = true;
            }

            if (!MonitoringEnabled.HasValue)
            {
                MonitoringEnabled = false;
            }

            if (ScanIntervalSeconds < 5)
            {
                ScanIntervalSeconds = 30;
            }

            if (!AppWatchEnabled.HasValue)
            {
                AppWatchEnabled = false;
            }

            if (AppWatchProcessName == null)
            {
                AppWatchProcessName = "";
            }

            if (AppWatchLaunchTarget == null)
            {
                AppWatchLaunchTarget = "";
            }

            if (!string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase))
            {
                AppWatchIntervalUnit = "Minutes";
            }

            int maxAppWatchInterval = string.Equals(AppWatchIntervalUnit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            if (AppWatchIntervalValue <= 0)
            {
                AppWatchIntervalValue = 5;
            }
            else if (AppWatchIntervalValue > maxAppWatchInterval)
            {
                AppWatchIntervalValue = maxAppWatchInterval;
            }

            if (NearbySsids == null)
            {
                NearbySsids = new List<string>();
            }
            else if (NearbySsids.Count == 1 && string.Equals(NearbySsids[0], "ExampleWifiName", StringComparison.OrdinalIgnoreCase))
            {
                NearbySsids.Clear();
            }

            if (Commands == null)
            {
                Commands = new List<string>();
            }

            if (ChromeUrls == null)
            {
                ChromeUrls = new List<string>();
            }
            else
            {
                ChromeUrls = ChromeUrls
                    .Where(url => !ActionValueCleaner.IsAudioStatusValue(url))
                    .ToList();
            }

            if (AppLaunches == null)
            {
                AppLaunches = new List<string>();
            }
            else
            {
                AppLaunches = AppLaunches
                    .Where(app => !ActionValueCleaner.IsAudioStatusValue(app))
                    .ToList();
            }

            if (ConnectProfile == null)
            {
                ConnectProfile = "";
            }

            if (ConnectSsid == null)
            {
                ConnectSsid = "";
            }

            if (!ConnectWifiEnabled.HasValue)
            {
                ConnectWifiEnabled = !string.IsNullOrWhiteSpace(ConnectProfile);
            }

            if (string.IsNullOrWhiteSpace(AudioAction))
            {
                AudioAction = "None";
            }
        }
    }

    internal static class ActionValueCleaner
    {
        public static bool IsAudioStatusValue(string value)
        {
            string token = ExtractToken(value);
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            string compact = token.Replace(" ", "").Replace("-", "").Replace("_", "");
            return string.Equals(compact, "mute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "muted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "unmute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "unmuted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "음소거", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "음소거해제", StringComparison.OrdinalIgnoreCase)
                || string.Equals(compact, "안함", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractToken(string value)
        {
            string text = (value ?? "").Trim().Trim('"', '\'');
            if (text.Length == 0)
            {
                return "";
            }

            try
            {
                Uri uri;
                if (Uri.TryCreate(text, UriKind.Absolute, out uri)
                    && (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                    && string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/'))
                    && string.IsNullOrWhiteSpace(uri.Query)
                    && string.IsNullOrWhiteSpace(uri.Fragment))
                {
                    text = uri.Host;
                }
            }
            catch
            {
            }

            return text.Trim().TrimEnd('.').ToLowerInvariant();
        }
    }

    internal static class ConfigStore
    {
        public static readonly string ConfigDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinZoneTrigger");

        public static readonly string ConfigPath = Path.Combine(ConfigDirectory, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    AppConfig created = AppConfig.CreateDefault();
                    Save(created);
                    return created;
                }

                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                AppConfig config = new JavaScriptSerializer().Deserialize<AppConfig>(json);
                if (config == null)
                {
                    config = AppConfig.CreateDefault();
                }

                config.Normalize();
                return config;
            }
            catch
            {
                AppConfig fallback = AppConfig.CreateDefault();
                fallback.Normalize();
                return fallback;
            }
        }

        public static void Save(AppConfig config)
        {
            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            config.Normalize();
            string json = new JavaScriptSerializer().Serialize(config);
            File.WriteAllText(ConfigPath, json, Encoding.UTF8);
        }
    }

    internal static class AppIcons
    {
        public static Icon GetAppIcon()
        {
            try
            {
                Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                return icon ?? SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly bool _startMinimizedRequested;
        private readonly bool _startedFromWindowsStartup;
        private readonly Dictionary<string, bool> _insideZones;
        private readonly Dictionary<string, DateTime> _lastAppWatchChecks;
        private readonly System.Windows.Forms.Timer _scanTimer;
        private readonly System.Windows.Forms.Timer _startupRetryTimer;
        private readonly System.Windows.Forms.Timer _appWatchTimer;
        private AppConfig _config;
        private bool _loadingSelection;
        private bool _allowExit;
        private bool _scanInProgress;
        private bool _appWatchInProgress;
        private bool _startupRetryActive;
        private int _startupRetryAttemptsRemaining;
        private int _startupRetryAttemptsTotal;
        private bool _lastScanHadActiveZone;
        private string _currentZoneId;

        private NotifyIcon _trayIcon;
        private TabControl _zoneTabs;
        private TabPage _allZonesTab;
        private TabPage _activeZonesTab;
        private TabPage _inactiveZonesTab;
        private ListBox _allZoneList;
        private ListBox _activeZoneList;
        private ListBox _inactiveZoneList;
        private TabControl _detailTabs;
        private TableLayoutPanel _conditionTable;
        private TableLayoutPanel _actionTable;
        private TableLayoutPanel _appWatchTable;
        private TableLayoutPanel _statusTable;
        private Label _selectedZoneSummaryLabel;
        private Label _selectedZoneMetaLabel;
        private Label _summaryOperatingBadge;
        private Label _summaryMatchBadge;
        private Label _summaryModeBadge;
        private CheckBox _monitoringCheck;
        private CheckBox _runOnceStartupCheck;
        private CheckBox _startupCheck;
        private CheckBox _startMinimizedCheck;
        private NumericUpDown _intervalInput;
        private CheckBox _zoneEnabledCheck;
        private TextBox _zoneNameText;
        private CheckBox _useCoordinatesCheck;
        private TextBox _latitudeText;
        private TextBox _longitudeText;
        private NumericUpDown _radiusInput;
        private FlowLayoutPanel _wifiChoicesPanel;
        private Label _selectedWifiLabel;
        private CheckBox _requireAllSsidsCheck;
        private CheckBox _connectWifiCheck;
        private FlowLayoutPanel _connectWifiChoicesPanel;
        private Label _connectWifiTargetLabel;
        private TextBox _connectProfileText;
        private TextBox _connectSsidText;
        private ComboBox _audioActionCombo;
        private TextBox _chromeUrlInputText;
        private FlowLayoutPanel _chromeUrlChipsPanel;
        private string _selectedChromeUrl;
        private TextBox _appLaunchInputText;
        private FlowLayoutPanel _appSearchResultsPanel;
        private FlowLayoutPanel _appLaunchChipsPanel;
        private string _selectedAppLaunch;
        private CheckBox _appWatchEnabledCheck;
        private TextBox _appWatchTargetText;
        private TextBox _appWatchProcessText;
        private NumericUpDown _appWatchIntervalInput;
        private ComboBox _appWatchIntervalUnitCombo;
        private Label _appWatchStatusLabel;
        private TextBox _commandsText;
        private Label _activeZonesLabel;
        private Label _currentLocationLabel;
        private Label _visibleNetworksLabel;
        private TextBox _logText;
        private Label _recentLogLabel;
        private ToolTip _toolTip;
        private List<WifiNetwork> _lastVisibleNetworks;

        private static readonly Color UiBackground = Color.FromArgb(246, 247, 242);
        private static readonly Color UiSurface = Color.FromArgb(253, 253, 249);
        private static readonly Color UiSurfaceMuted = Color.FromArgb(239, 243, 235);
        private static readonly Color UiBorder = Color.FromArgb(211, 218, 207);
        private static readonly Color UiText = Color.FromArgb(35, 45, 47);
        private static readonly Color UiTextMuted = Color.FromArgb(97, 111, 103);
        private static readonly Color UiAccent = Color.FromArgb(31, 122, 92);
        private static readonly Color UiAccentDark = Color.FromArgb(20, 91, 69);
        private static readonly Color UiAccentSoft = Color.FromArgb(220, 240, 229);
        private static readonly Color UiAmberSoft = Color.FromArgb(255, 242, 202);
        private static readonly Color UiDanger = Color.FromArgb(176, 70, 61);
        private static readonly Color UiLogBackground = Color.FromArgb(35, 43, 43);
        private static readonly Color UiLogText = Color.FromArgb(232, 238, 232);

        private enum ButtonTone
        {
            Default,
            Primary,
            Danger
        }

        public MainForm(bool startMinimizedRequested, bool startedFromWindowsStartup)
        {
            _startMinimizedRequested = startMinimizedRequested;
            _startedFromWindowsStartup = startedFromWindowsStartup;
            _insideZones = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _lastAppWatchChecks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _lastVisibleNetworks = new List<WifiNetwork>();
            _config = ConfigStore.Load();
            _scanTimer = new System.Windows.Forms.Timer();
            _startupRetryTimer = new System.Windows.Forms.Timer();
            _appWatchTimer = new System.Windows.Forms.Timer();
            _startupRetryTimer.Interval = 15000;
            _startupRetryTimer.Tick += StartupRetryTimerTick;
            _appWatchTimer.Tick += AppWatchTimerTick;

            InitializeComponent();
            ConfigureTray();
            BindConfigToControls();
            ResetScanTimer();
            ResetAppWatchTimer();
        }

        private void InitializeComponent()
        {
            Text = "위치 자동 실행";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1020, 680);
            Size = new Size(1180, 820);
            BackColor = UiBackground;
            ForeColor = UiText;
            Font = new Font("Malgun Gothic", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            Icon = AppIcons.GetAppIcon();
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 12000;
            _toolTip.InitialDelay = 400;
            _toolTip.ReshowDelay = 100;

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
            _startupCheck.Text = "Windows 시작 시 실행";
            _startupCheck.AutoSize = true;
            _startupCheck.Margin = new Padding(4, 8, 14, 4);
            topBar.Controls.Add(_startupCheck);

            _startMinimizedCheck = new CheckBox();
            _startMinimizedCheck.Text = "트레이로 시작";
            _startMinimizedCheck.AutoSize = true;
            _startMinimizedCheck.Margin = new Padding(4, 8, 14, 4);
            topBar.Controls.Add(_startMinimizedCheck);

            Button saveButton = CreateButton("전체 저장");
            saveButton.Click += delegate { SaveFromUi(); };
            topBar.Controls.Add(saveButton);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.BackColor = UiBorder;
            split.FixedPanel = FixedPanel.Panel1;
            split.Panel1MinSize = 300;
            split.SplitterWidth = 5;
            split.SplitterDistance = 320;
            split.Resize += delegate { KeepDetailPaneReadable(split); };
            split.SplitterMoved += delegate { KeepDetailPaneReadable(split); };
            Load += delegate { KeepDetailPaneReadable(split); };
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
            _zoneTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _zoneTabs.DrawItem += DrawZoneTab;
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
            _detailTabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _detailTabs.SizeMode = TabSizeMode.Fixed;
            _detailTabs.ItemSize = new Size(116, 30);
            _detailTabs.DrawItem += DrawDetailTab;
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

            _appWatchEnabledCheck = new CheckBox();
            _appWatchEnabledCheck.Text = "프로그램이 꺼져 있으면 다시 실행";
            _appWatchEnabledCheck.AutoSize = true;
            AddRowTo(_appWatchTable, "상태", _appWatchEnabledCheck);

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

            Button findAppWatchButton = CreateButton("앱 찾기");
            SetFixedButtonSize(findAppWatchButton, 78, 30);
            findAppWatchButton.Click += delegate { ShowAppWatchPicker(); };
            appWatchTargetInputPanel.Controls.Add(findAppWatchButton);

            Button browseAppWatchButton = CreateButton("파일 선택");
            SetFixedButtonSize(browseAppWatchButton, 86, 30);
            browseAppWatchButton.Click += delegate { BrowseAppWatchFile(); };
            appWatchTargetInputPanel.Controls.Add(browseAppWatchButton);

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

            _activeZonesLabel = new Label();
            _activeZonesLabel.Dock = DockStyle.Fill;
            _activeZonesLabel.AutoSize = true;
            _activeZonesLabel.MaximumSize = new Size(760, 0);
            _activeZonesLabel.Text = "아직 확인 전입니다.";
            AddRowTo(_statusTable, "조건 일치", _activeZonesLabel);

            _currentLocationLabel = new Label();
            _currentLocationLabel.Dock = DockStyle.Fill;
            _currentLocationLabel.AutoSize = true;
            _currentLocationLabel.MaximumSize = new Size(760, 0);
            _currentLocationLabel.Text = "아직 위치 확인 전입니다.";
            AddRowTo(_statusTable, "현재 위치", _currentLocationLabel);

            _visibleNetworksLabel = new Label();
            _visibleNetworksLabel.Dock = DockStyle.Fill;
            _visibleNetworksLabel.AutoSize = true;
            _visibleNetworksLabel.MaximumSize = new Size(760, 0);
            _visibleNetworksLabel.Text = "아직 Wi-Fi 확인 전입니다.";
            AddRowTo(_statusTable, "보이는 Wi-Fi", _visibleNetworksLabel);

            AddSectionHeaderTo(_statusTable, "실행 로그");

            _recentLogLabel = new Label();
            _recentLogLabel.Dock = DockStyle.Fill;
            _recentLogLabel.AutoSize = true;
            _recentLogLabel.MaximumSize = new Size(760, 0);
            _recentLogLabel.Text = "아직 기록된 이벤트가 없습니다.";
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
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ResetAppWatchTimer();
                ZoneRule selected = GetSelectedZone();
                string name = selected == null ? "선택 위치" : selected.Name;
                AppendLog(_appWatchEnabledCheck.Checked ? "앱 감시를 시작했습니다: " + name : "앱 감시를 껐습니다: " + name);
                if (_appWatchEnabledCheck.Checked)
                {
                    RunAppWatchCheck(selected == null ? null : selected.Clone(), true, "앱 감시 시작 확인", false);
                }
            };

            _appWatchTargetText.Leave += delegate
            {
                FillAppWatchProcessNameFromTarget(false);
                CaptureCurrentZone();
                CaptureGlobalSettings();
            };

            _appWatchProcessText.TextChanged += delegate
            {
                if (!_loadingSelection)
                {
                    CaptureCurrentZone();
                    CaptureGlobalSettings();
                }
            };

            _appWatchIntervalInput.ValueChanged += delegate
            {
                if (_loadingSelection)
                {
                    return;
                }
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ResetAppWatchTimer();
            };

            _appWatchIntervalUnitCombo.SelectedIndexChanged += delegate
            {
                UpdateAppWatchIntervalLimits();
                if (_loadingSelection)
                {
                    return;
                }
                CaptureCurrentZone();
                CaptureGlobalSettings();
                ResetAppWatchTimer();
            };
        }

        private Control CreateSelectedZoneSummaryBar()
        {
            TableLayoutPanel summary = new TableLayoutPanel();
            summary.Dock = DockStyle.Top;
            summary.AutoSize = true;
            summary.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            summary.BackColor = UiSurfaceMuted;
            summary.Padding = new Padding(12, 8, 12, 8);
            summary.Margin = new Padding(0, 0, 0, 8);
            summary.ColumnCount = 1;
            summary.RowCount = 2;
            summary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            summary.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            TableLayoutPanel textStack = new TableLayoutPanel();
            textStack.Dock = DockStyle.Top;
            textStack.AutoSize = true;
            textStack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            textStack.ColumnCount = 1;
            textStack.RowCount = 3;
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _selectedZoneSummaryLabel = new Label();
            _selectedZoneSummaryLabel.Text = "위치를 선택하세요";
            _selectedZoneSummaryLabel.AutoSize = true;
            _selectedZoneSummaryLabel.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold, GraphicsUnit.Point);
            _selectedZoneSummaryLabel.ForeColor = UiAccentDark;
            _selectedZoneSummaryLabel.Margin = new Padding(0, 0, 0, 3);
            _selectedZoneSummaryLabel.Tag = "AccentTitle";
            textStack.Controls.Add(_selectedZoneSummaryLabel, 0, 0);

            _selectedZoneMetaLabel = new Label();
            _selectedZoneMetaLabel.Text = "등록된 위치를 선택하면 감지 조건과 실행 동작이 여기에 요약됩니다.";
            _selectedZoneMetaLabel.AutoSize = true;
            _selectedZoneMetaLabel.MaximumSize = new Size(620, 0);
            _selectedZoneMetaLabel.ForeColor = UiTextMuted;
            _selectedZoneMetaLabel.Margin = new Padding(0, 0, 0, 5);
            _selectedZoneMetaLabel.Tag = "Muted";
            textStack.Controls.Add(_selectedZoneMetaLabel, 0, 1);

            FlowLayoutPanel badges = new FlowLayoutPanel();
            badges.AutoSize = true;
            badges.WrapContents = true;
            badges.Margin = new Padding(0);
            badges.BackColor = UiSurfaceMuted;
            _summaryOperatingBadge = CreateSummaryBadge("미선택");
            _summaryMatchBadge = CreateSummaryBadge("대기");
            _summaryModeBadge = CreateSummaryBadge("감지 방식 없음");
            badges.Controls.Add(_summaryOperatingBadge);
            badges.Controls.Add(_summaryMatchBadge);
            badges.Controls.Add(_summaryModeBadge);
            textStack.Controls.Add(badges, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.AutoSize = false;
            buttons.Height = 34;
            buttons.WrapContents = false;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.Margin = new Padding(0, 7, 0, 0);
            buttons.BackColor = UiSurfaceMuted;

            Button saveSelectedButton = CreateButton("저장");
            SetFixedButtonSize(saveSelectedButton, 74, 30);
            saveSelectedButton.Click += delegate { SaveFromUi(); };
            buttons.Controls.Add(saveSelectedButton);

            Button operateButton = CreateButton("운영하기");
            SetFixedButtonSize(operateButton, 82, 30);
            operateButton.Click += delegate { SetSelectedZoneOperating(true); };
            buttons.Controls.Add(operateButton);

            Button stopOperatingButton = CreateButton("운영 중지");
            SetFixedButtonSize(stopOperatingButton, 82, 30);
            stopOperatingButton.Click += delegate { SetSelectedZoneOperating(false); };
            buttons.Controls.Add(stopOperatingButton);

            Button testConditionButton = CreateButton("테스트해보기");
            SetFixedButtonSize(testConditionButton, 94, 30);
            testConditionButton.Click += delegate { TestSelectedZoneCondition(); };
            buttons.Controls.Add(testConditionButton);

            Button testActionsButton = CreateButton("동작 테스트");
            SetFixedButtonSize(testActionsButton, 88, 30);
            testActionsButton.Click += delegate { TestSelectedZoneActions(); };
            buttons.Controls.Add(testActionsButton);

            Button openConfigButton = CreateButton("설정 폴더");
            SetFixedButtonSize(openConfigButton, 82, 30);
            openConfigButton.Click += delegate { OpenConfigFolder(); };
            buttons.Controls.Add(openConfigButton);

            summary.Controls.Add(textStack, 0, 0);
            summary.Controls.Add(buttons, 0, 1);
            return summary;
        }

        private TableLayoutPanel CreateDetailTable()
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.ColumnCount = 2;
            table.BackColor = UiSurface;
            table.Padding = new Padding(2, 0, 6, 10);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        private TabPage CreateDetailTabPage(string title, TableLayoutPanel table)
        {
            TabPage page = new TabPage(title);
            page.BackColor = UiSurface;
            page.ForeColor = UiText;

            Panel scroller = new Panel();
            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.BackColor = UiSurface;
            scroller.Padding = new Padding(0, 5, 0, 0);
            scroller.Controls.Add(table);
            page.Controls.Add(scroller);
            return page;
        }

        private Label CreateSummaryBadge(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Height = 24;
            label.Width = Math.Max(82, TextRenderer.MeasureText(text, Font).Width + 22);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Margin = new Padding(0, 0, 6, 0);
            label.Padding = new Padding(8, 0, 8, 0);
            label.BorderStyle = BorderStyle.FixedSingle;
            label.Tag = "SummaryBadge";
            SetSummaryBadge(label, text, UiSurface, UiTextMuted);
            return label;
        }

        private void SetSummaryBadge(Label label, string text, Color background, Color foreground)
        {
            if (label == null)
            {
                return;
            }

            label.Text = text;
            label.Width = Math.Max(82, TextRenderer.MeasureText(text, Font).Width + 22);
            label.BackColor = background;
            label.ForeColor = foreground;
        }

        private void UpdateSelectedZoneSummary()
        {
            if (_selectedZoneSummaryLabel == null)
            {
                return;
            }

            ZoneRule zone = GetSelectedZone();
            if (zone == null)
            {
                _selectedZoneSummaryLabel.Text = "위치를 선택하세요";
                _selectedZoneMetaLabel.Text = "등록된 위치를 선택하면 감지 조건과 실행 동작이 여기에 요약됩니다.";
                SetSummaryBadge(_summaryOperatingBadge, "미선택", UiSurface, UiTextMuted);
                SetSummaryBadge(_summaryMatchBadge, "대기", UiSurface, UiTextMuted);
                SetSummaryBadge(_summaryModeBadge, "감지 방식 없음", UiSurface, UiTextMuted);
                return;
            }

            string name = _loadingSelection ? zone.Name : (string.IsNullOrWhiteSpace(_zoneNameText.Text) ? zone.Name : _zoneNameText.Text.Trim());
            _selectedZoneSummaryLabel.Text = name;
            _selectedZoneMetaLabel.Text = BuildZoneActionSummary(zone);

            bool matched = IsZoneActive(zone);
            SetSummaryBadge(
                _summaryOperatingBadge,
                zone.Enabled ? "운영 중" : "미운영",
                zone.Enabled ? UiAccent : UiSurface,
                zone.Enabled ? Color.White : UiTextMuted);
            SetSummaryBadge(
                _summaryMatchBadge,
                matched ? "조건 일치" : "대기",
                matched ? UiAccentSoft : UiSurface,
                matched ? UiAccentDark : UiTextMuted);
            SetSummaryBadge(_summaryModeBadge, BuildZoneModeSummary(zone), UiSurface, UiText);
        }

        private string BuildZoneModeSummary(ZoneRule zone)
        {
            if (zone == null)
            {
                return "감지 방식 없음";
            }

            if (zone.UseCoordinates)
            {
                return "좌표 " + zone.RadiusMeters + "m";
            }

            int wifiCount = zone.NearbySsids == null ? 0 : zone.NearbySsids.Count(s => !string.IsNullOrWhiteSpace(s));
            return wifiCount == 0 ? "Wi-Fi 미설정" : "Wi-Fi " + wifiCount + "개";
        }

        private string BuildZoneActionSummary(ZoneRule zone)
        {
            if (zone == null)
            {
                return "";
            }

            List<string> actions = new List<string>();
            if (zone.ConnectWifiEnabled.GetValueOrDefault(false))
            {
                actions.Add("Wi-Fi 연결");
            }
            if (!string.IsNullOrWhiteSpace(zone.AudioAction) && !string.Equals(zone.AudioAction, "None", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(string.Equals(zone.AudioAction, "Mute", StringComparison.OrdinalIgnoreCase) ? "음소거" : "음소거 해제");
            }
            if (zone.ChromeUrls != null && zone.ChromeUrls.Any(u => !string.IsNullOrWhiteSpace(u)))
            {
                actions.Add("링크 " + zone.ChromeUrls.Count(u => !string.IsNullOrWhiteSpace(u)) + "개");
            }
            if (zone.AppLaunches != null && zone.AppLaunches.Any(a => !string.IsNullOrWhiteSpace(a)))
            {
                actions.Add("앱 " + zone.AppLaunches.Count(a => !string.IsNullOrWhiteSpace(a)) + "개");
            }
            if (zone.Commands != null && zone.Commands.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                actions.Add("명령 " + zone.Commands.Count(c => !string.IsNullOrWhiteSpace(c)) + "개");
            }

            List<string> schedules = new List<string>();
            if (zone.RunOnceAtStartup.GetValueOrDefault(true))
            {
                schedules.Add("시작 1회");
            }
            if (zone.MonitoringEnabled.GetValueOrDefault(false))
            {
                schedules.Add("지속 " + Math.Max(5, zone.ScanIntervalSeconds) + "초");
            }

            string prefix = zone.Enabled ? "자동 실행 준비됨" : "운영 중지됨";
            if (schedules.Count > 0)
            {
                prefix += " · " + string.Join("/", schedules.ToArray());
            }
            return actions.Count == 0 ? prefix + " · 실행 동작 없음" : prefix + " · " + string.Join(", ", actions.ToArray());
        }

        private void TestSelectedZoneActions()
        {
            CaptureCurrentZone();
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, "'" + selected.Name + "'의 동작을 지금 실행할까요?", "동작 테스트", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                TriggerZone(selected.Clone(), "동작 테스트");
            }
        }

        private void OpenConfigFolder()
        {
            if (!Directory.Exists(ConfigStore.ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigStore.ConfigDirectory);
            }
            Process.Start("explorer.exe", ConfigStore.ConfigDirectory);
        }

        private void ApplyTheme(Control root)
        {
            ApplyThemeToControl(root);

            foreach (Control child in root.Controls)
            {
                ApplyTheme(child);
            }
        }

        private void ApplyThemeToControl(Control control)
        {
            if (control is Label)
            {
                Label label = (Label)control;
                string tag = Convert.ToString(label.Tag);
                if (string.Equals(tag, "SectionHeader", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                if (string.Equals(tag, "SummaryBadge", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                label.BackColor = Color.Transparent;
                if (string.Equals(tag, "Muted", StringComparison.OrdinalIgnoreCase))
                {
                    label.ForeColor = UiTextMuted;
                }
                else if (string.Equals(tag, "AccentTitle", StringComparison.OrdinalIgnoreCase))
                {
                    label.ForeColor = UiAccentDark;
                }
                else
                {
                    label.ForeColor = UiText;
                }
            }
            else if (control is TextBox)
            {
                TextBox textBox = (TextBox)control;
                textBox.BackColor = UiSurface;
                textBox.ForeColor = UiText;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is CheckBox)
            {
                CheckBox checkBox = (CheckBox)control;
                checkBox.ForeColor = UiText;
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.BackColor = Color.Transparent;
            }
            else if (control is ComboBox)
            {
                ComboBox comboBox = (ComboBox)control;
                comboBox.BackColor = UiSurface;
                comboBox.ForeColor = UiText;
                comboBox.FlatStyle = FlatStyle.Flat;
            }
            else if (control is NumericUpDown)
            {
                NumericUpDown numeric = (NumericUpDown)control;
                numeric.BackColor = UiSurface;
                numeric.ForeColor = UiText;
                numeric.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ListBox)
            {
                ListBox listBox = (ListBox)control;
                listBox.BackColor = UiSurface;
                listBox.ForeColor = UiText;
                listBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is TabPage)
            {
                control.BackColor = UiSurface;
                control.ForeColor = UiText;
            }
            else if (control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel || control is SplitterPanel)
            {
                if (control.BackColor == SystemColors.Control)
                {
                    control.BackColor = UiBackground;
                }

                control.ForeColor = UiText;
            }
        }

        private void StyleLogBox()
        {
            if (_logText == null)
            {
                return;
            }

            _logText.BackColor = UiLogBackground;
            _logText.ForeColor = UiLogText;
            _logText.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
            _logText.BorderStyle = BorderStyle.None;
            _logText.Padding = new Padding(8);
        }

        private Button CreateButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(74, 30);
            button.Padding = new Padding(10, 3, 10, 3);
            button.Margin = new Padding(4);
            button.Cursor = Cursors.Hand;
            StyleButton(button, ResolveButtonTone(text));
            return button;
        }

        private void SetFixedButtonSize(Button button, int width, int height)
        {
            if (button == null)
            {
                return;
            }

            Size measured = TextRenderer.MeasureText(button.Text ?? "", button.Font ?? Font);
            width = Math.Max(width, measured.Width + 34);
            button.AutoSize = false;
            button.Size = new Size(width, height);
            button.MinimumSize = new Size(width, height);
        }

        private ButtonTone ResolveButtonTone(string text)
        {
            string value = text ?? "";
            if (value.IndexOf("삭제", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("제거", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("운영 안함", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("운영 중지", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ButtonTone.Danger;
            }

            if (value.IndexOf("저장", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("운영하기", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ButtonTone.Primary;
            }

            return ButtonTone.Default;
        }

        private void StyleButton(Button button, ButtonTone tone)
        {
            if (button == null)
            {
                return;
            }

            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.Font = new Font(Font.FontFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);

            if (tone == ButtonTone.Primary)
            {
                button.BackColor = UiAccent;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = UiAccentDark;
                button.FlatAppearance.MouseOverBackColor = UiAccentDark;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(12, 68, 51);
            }
            else if (tone == ButtonTone.Danger)
            {
                button.BackColor = UiSurface;
                button.ForeColor = UiDanger;
                button.FlatAppearance.BorderColor = Color.FromArgb(222, 190, 184);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(252, 237, 233);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(244, 217, 211);
            }
            else
            {
                button.BackColor = UiSurface;
                button.ForeColor = UiText;
                button.FlatAppearance.BorderColor = UiBorder;
                button.FlatAppearance.MouseOverBackColor = UiSurfaceMuted;
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 231, 221);
            }
        }

        private void DrawDetailTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count)
            {
                return;
            }

            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = tabs.GetTabRect(e.Index);
            bounds.Inflate(-2, -2);

            Color background = selected ? UiAccent : UiSurfaceMuted;
            Color foreground = selected ? Color.White : UiTextMuted;
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            using (Pen pen = new Pen(selected ? UiAccentDark : UiBorder))
            {
                e.Graphics.DrawRectangle(pen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
            }

            TextRenderer.DrawText(
                e.Graphics,
                tabs.TabPages[e.Index].Text,
                Font,
                bounds,
                foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawZoneTab(object sender, DrawItemEventArgs e)
        {
            TabControl tabs = sender as TabControl;
            if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count)
            {
                return;
            }

            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = tabs.GetTabRect(e.Index);
            bounds.Inflate(-2, -2);

            Color background = selected ? UiAccentDark : UiSurfaceMuted;
            Color foreground = selected ? Color.White : UiTextMuted;
            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, bounds);
            }

            using (Pen pen = new Pen(selected ? UiAccentDark : UiBorder))
            {
                e.Graphics.DrawRectangle(pen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
            }

            TextRenderer.DrawText(
                e.Graphics,
                tabs.TabPages[e.Index].Text,
                Font,
                bounds,
                foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private ListBox CreateZoneListBox()
        {
            ListBox listBox = new ListBox();
            listBox.Dock = DockStyle.Fill;
            listBox.IntegralHeight = false;
            listBox.DrawMode = DrawMode.OwnerDrawFixed;
            listBox.ItemHeight = 44;
            listBox.BorderStyle = BorderStyle.FixedSingle;
            listBox.BackColor = UiSurface;
            listBox.ForeColor = UiText;
            listBox.SelectedIndexChanged += ZoneListSelectedIndexChanged;
            listBox.DrawItem += DrawZoneListItem;
            return listBox;
        }

        private void DrawZoneListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            ListBox list = (ListBox)sender;
            ZoneListItem item = list.Items[e.Index] as ZoneListItem;
            if (item == null)
            {
                e.DrawBackground();
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool matched = IsZoneActive(item.Zone);
            bool operating = item.Zone.Enabled;
            Color background = selected
                ? UiAccentDark
                : matched
                    ? UiAccentSoft
                    : operating
                        ? UiSurface
                        : UiSurfaceMuted;
            Color foreground = selected ? Color.White : UiText;
            Color metaColor = selected ? Color.FromArgb(226, 239, 230) : UiTextMuted;

            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            if (matched && !selected)
            {
                using (SolidBrush accentBrush = new SolidBrush(UiAccent))
                {
                    e.Graphics.FillRectangle(accentBrush, e.Bounds.Left, e.Bounds.Top, 4, e.Bounds.Height);
                }
            }

            using (Pen linePen = new Pen(UiBorder))
            {
                e.Graphics.DrawLine(linePen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }

            Rectangle textRect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top + 6, e.Bounds.Width - 18, 17);
            Rectangle metaRect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top + 24, e.Bounds.Width - 18, 15);
            TextRenderer.DrawText(e.Graphics, item.Zone.Name, Font, textRect, foreground, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(e.Graphics, item.MetadataText, Font, metaRect, metaColor, TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private Label CreateInlineLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = UiTextMuted;
            label.Margin = new Padding(8, 9, 4, 4);
            label.Tag = "Muted";
            return label;
        }

        private Size GetChipSize(string text, Font font)
        {
            Size measured = TextRenderer.MeasureText(text ?? "", font ?? Font);
            int width = Math.Max(116, Math.Min(240, measured.Width + 28));
            return new Size(width, 30);
        }

        private FlowLayoutPanel CreateChipPanel(int height)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.AutoScroll = true;
            panel.WrapContents = true;
            panel.Height = height;
            panel.BackColor = UiSurfaceMuted;
            panel.Padding = new Padding(7, 6, 7, 5);
            panel.Margin = new Padding(0, 0, 0, 6);
            return panel;
        }

        private Button CreateValueChip(string value, string text, bool selected)
        {
            Button chip = new Button();
            chip.Text = text;
            chip.Tag = value;
            chip.AutoSize = false;
            chip.Size = GetValueChipSize(text, chip.Font);
            chip.Margin = new Padding(4, 3, 4, 3);
            chip.Padding = new Padding(8, 2, 8, 2);
            chip.TextAlign = ContentAlignment.MiddleCenter;
            chip.Cursor = Cursors.Hand;
            StyleChip(chip, selected);
            if (_toolTip != null)
            {
                _toolTip.SetToolTip(chip, value);
            }
            return chip;
        }

        private Size GetValueChipSize(string text, Font font)
        {
            Size measured = TextRenderer.MeasureText(text ?? "", font ?? Font);
            int width = Math.Max(104, Math.Min(360, measured.Width + 30));
            return new Size(width, 30);
        }

        private void StyleChip(Button chip, bool selected)
        {
            chip.FlatStyle = FlatStyle.Flat;
            chip.UseVisualStyleBackColor = false;
            if (selected)
            {
                chip.BackColor = UiAccent;
                chip.ForeColor = Color.White;
                chip.FlatAppearance.BorderColor = UiAccentDark;
                chip.FlatAppearance.MouseOverBackColor = UiAccentDark;
                chip.FlatAppearance.MouseDownBackColor = Color.FromArgb(12, 68, 51);
            }
            else
            {
                chip.BackColor = UiSurface;
                chip.ForeColor = UiText;
                chip.FlatAppearance.BorderColor = UiBorder;
                chip.FlatAppearance.MouseOverBackColor = UiSurfaceMuted;
                chip.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 231, 221);
            }
        }

        private string ShortenUrlForChip(string value)
        {
            string url = value ?? "";
            try
            {
                Uri uri = new Uri(url);
                string host = uri.Host.Replace("www.", "");
                string path = uri.AbsolutePath.Trim('/');
                if (path.Length == 0)
                {
                    return host;
                }

                string first = path.Split('/')[0];
                return host + " · " + ShortenText(first, 18);
            }
            catch
            {
                return ShortenText(url, 34);
            }
        }

        private static string ShortenText(string value, int maxLength)
        {
            string text = (value ?? "").Trim();
            if (text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, Math.Max(1, maxLength - 1)) + "…";
        }

        private void StyleToggleButton(CheckBox toggle)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.FlatStyle = FlatStyle.Flat;
            toggle.UseVisualStyleBackColor = false;
            toggle.Padding = new Padding(8, 3, 8, 3);
            toggle.MinimumSize = new Size(72, 28);
            toggle.TextAlign = ContentAlignment.MiddleCenter;
            toggle.Cursor = Cursors.Hand;

            if (toggle.Checked)
            {
                toggle.BackColor = UiAccent;
                toggle.ForeColor = Color.White;
                toggle.FlatAppearance.BorderColor = UiAccentDark;
                toggle.FlatAppearance.MouseOverBackColor = UiAccentDark;
                toggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(12, 68, 51);
            }
            else
            {
                toggle.BackColor = UiSurface;
                toggle.ForeColor = UiText;
                toggle.FlatAppearance.BorderColor = UiBorder;
                toggle.FlatAppearance.MouseOverBackColor = UiSurfaceMuted;
                toggle.FlatAppearance.MouseDownBackColor = Color.FromArgb(224, 231, 221);
            }
        }

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

            _wifiChoicesPanel.Controls.Clear();

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

            _connectWifiChoicesPanel.Controls.Clear();
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
            _chromeUrlChipsPanel.Controls.Clear();
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
            _chromeUrlChipsPanel.Controls.Clear();
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

            _appSearchResultsPanel.Controls.Clear();
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
            _appLaunchChipsPanel.Controls.Clear();
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
                AppLauncher.LaunchApp(target, AppendLog);
                ShowTrayNotification("앱 실행", BuildLaunchNotificationText(target));
            }
            catch (Exception ex)
            {
                AppendLog("앱 실행 테스트 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "앱 실행 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowAppWatchPicker()
        {
            using (AppPickerForm picker = new AppPickerForm())
            {
                if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedTargets.Count > 0)
                {
                    SetAppWatchTarget(picker.SelectedTargets[0]);
                }
            }
        }

        private void BrowseAppWatchFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "감시할 앱 선택";
                dialog.Filter = "실행 파일 또는 바로가기|*.exe;*.lnk;*.bat;*.cmd|모든 파일|*.*";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    SetAppWatchTarget(dialog.FileName);
                }
            }
        }

        private void SetAppWatchTarget(string target)
        {
            if (_appWatchTargetText == null)
            {
                return;
            }

            _appWatchTargetText.Text = (target ?? "").Trim();
            FillAppWatchProcessNameFromTarget(false);
            CaptureGlobalSettings();
        }

        private void FillAppWatchProcessNameFromTarget(bool replaceExisting)
        {
            if (_appWatchTargetText == null || _appWatchProcessText == null)
            {
                return;
            }

            if (!replaceExisting && !string.IsNullOrWhiteSpace(_appWatchProcessText.Text))
            {
                return;
            }

            string inferred = AppWatchdog.InferProcessName(_appWatchTargetText.Text);
            if (!string.IsNullOrWhiteSpace(inferred))
            {
                _appWatchProcessText.Text = inferred;
            }
        }

        private void TestAppWatchLaunchTarget()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            ZoneRule selected = GetSelectedZone();
            string target = selected == null ? "" : selected.AppWatchLaunchTarget ?? "";
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(this, "실행할 앱을 먼저 입력하거나 선택하세요.", "실행 테스트", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                AppLauncher.LaunchApp(target, AppendLog);
                ShowTrayNotification("앱 감시 실행 테스트", BuildLaunchNotificationText(target));
            }
            catch (Exception ex)
            {
                AppendLog("앱 감시 실행 테스트 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "실행 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void TestAppWatchStatusOnly()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            ZoneRule selected = GetSelectedZone();
            RunAppWatchCheck(selected == null ? null : selected.Clone(), false, "앱 감시 상태 테스트", true);
        }

        private void TestAppWatchRestart()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            ZoneRule selected = GetSelectedZone();
            RunAppWatchCheck(selected == null ? null : selected.Clone(), true, "앱 감시 재실행 테스트", true);
        }

        private void UpdateAppWatchIntervalLimits()
        {
            if (_appWatchIntervalInput == null)
            {
                return;
            }

            string unit = ReadAppWatchIntervalUnitSelection();
            decimal max = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? 168 : 10080;
            _appWatchIntervalInput.Maximum = max;
            if (_appWatchIntervalInput.Value > max)
            {
                _appWatchIntervalInput.Value = max;
            }
        }

        private void SetAppWatchIntervalUnitSelection(string unit)
        {
            if (_appWatchIntervalUnitCombo == null)
            {
                return;
            }

            _appWatchIntervalUnitCombo.SelectedItem = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? "시간" : "분";
            if (_appWatchIntervalUnitCombo.SelectedIndex < 0)
            {
                _appWatchIntervalUnitCombo.SelectedIndex = 0;
            }
            UpdateAppWatchIntervalLimits();
        }

        private string ReadAppWatchIntervalUnitSelection()
        {
            string selected = _appWatchIntervalUnitCombo == null ? "" : Convert.ToString(_appWatchIntervalUnitCombo.SelectedItem);
            return string.Equals(selected, "시간", StringComparison.OrdinalIgnoreCase) ? "Hours" : "Minutes";
        }

        private static int GetAppWatchIntervalMilliseconds(int value, string unit)
        {
            long multiplier = string.Equals(unit, "Hours", StringComparison.OrdinalIgnoreCase) ? 3600000L : 60000L;
            long milliseconds = Math.Max(1, value) * multiplier;
            if (milliseconds > int.MaxValue)
            {
                return int.MaxValue;
            }

            return Convert.ToInt32(milliseconds);
        }

        private void RunAppWatchCheck(ZoneRule zone, bool launchIfMissing, string reason, bool showMessage)
        {
            CaptureGlobalSettings();
            if (zone == null)
            {
                string message = "앱 감시를 확인할 위치를 먼저 선택하세요.";
                UpdateAppWatchStatusLabel(message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            RunAppWatchChecks(new List<ZoneRule> { zone }, launchIfMissing, reason, showMessage);
        }

        private void RunDueAppWatchChecks(bool force, string reason)
        {
            DateTime now = DateTime.UtcNow;
            List<ZoneRule> dueZones = new List<ZoneRule>();
            foreach (ZoneRule zone in _config.Zones.Where(z => z.Enabled && z.AppWatchEnabled.GetValueOrDefault(false)))
            {
                zone.Normalize();
                DateTime last;
                int interval = GetAppWatchIntervalMilliseconds(zone.AppWatchIntervalValue, zone.AppWatchIntervalUnit);
                bool due = force
                    || !_lastAppWatchChecks.TryGetValue(zone.Id, out last)
                    || (now - last).TotalMilliseconds >= interval;

                if (due)
                {
                    _lastAppWatchChecks[zone.Id] = now;
                    dueZones.Add(zone.Clone());
                }
            }

            if (dueZones.Count > 0)
            {
                RunAppWatchChecks(dueZones, true, reason, false);
            }
        }

        private void RunAppWatchChecks(List<ZoneRule> zones, bool launchIfMissing, string reason, bool showMessage)
        {
            if (zones == null || zones.Count == 0)
            {
                return;
            }

            ZoneRule firstZone = zones[0];
            string processName = AppWatchdog.NormalizeProcessName(firstZone.AppWatchProcessName);
            string launchTarget = firstZone.AppWatchLaunchTarget ?? "";

            if (zones.Count == 1 && string.IsNullOrWhiteSpace(processName))
            {
                string message = "확인할 프로세스 이름을 입력하세요.";
                UpdateAppWatchStatusLabel(message);
                AppendLog(reason + " 실패(" + firstZone.Name + "): " + message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (zones.Count == 1 && launchIfMissing && string.IsNullOrWhiteSpace(launchTarget))
            {
                string message = "다시 실행할 앱 대상을 입력하세요.";
                UpdateAppWatchStatusLabel(message);
                AppendLog(reason + " 실패(" + firstZone.Name + "): " + message);
                if (showMessage)
                {
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }

            if (_appWatchInProgress)
            {
                AppendLog(reason + " 건너뜀: 이전 확인이 아직 진행 중입니다.");
                return;
            }

            _appWatchInProgress = true;
            UpdateAppWatchStatusLabel(reason + " 중입니다...");

            Task.Factory.StartNew(delegate
            {
                List<AppWatchZoneResult> results = new List<AppWatchZoneResult>();
                foreach (ZoneRule zone in zones)
                {
                    zone.Normalize();
                    string zoneProcessName = AppWatchdog.NormalizeProcessName(zone.AppWatchProcessName);
                    string zoneLaunchTarget = zone.AppWatchLaunchTarget ?? "";
                    AppWatchZoneResult zoneResult = new AppWatchZoneResult
                    {
                        ZoneId = zone.Id,
                        ZoneName = zone.Name,
                        LaunchTarget = zoneLaunchTarget
                    };

                    try
                    {
                        if (string.IsNullOrWhiteSpace(zoneProcessName))
                        {
                            zoneResult.Error = "확인할 프로세스 이름을 입력하세요.";
                        }
                        else if (launchIfMissing && string.IsNullOrWhiteSpace(zoneLaunchTarget))
                        {
                            zoneResult.Error = "다시 실행할 앱 대상을 입력하세요.";
                        }
                        else
                        {
                            zoneResult.Result = launchIfMissing
                                ? AppWatchdog.EnsureRunning(zoneProcessName, zoneLaunchTarget, SafeLog)
                                : AppWatchdog.Check(zoneProcessName);
                        }
                    }
                    catch (Exception ex)
                    {
                        zoneResult.Error = ex.Message;
                    }

                    results.Add(zoneResult);
                }

                return results;
            }).ContinueWith(delegate(Task<List<AppWatchZoneResult>> task)
            {
                _appWatchInProgress = false;

                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 앱 감시 오류입니다." : task.Exception.GetBaseException().Message;
                    UpdateAppWatchStatusLabel(message);
                    AppendLog(reason + " 실패: " + message);
                    if (showMessage)
                    {
                        MessageBox.Show(this, message, reason + " 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                }

                List<AppWatchZoneResult> results = task.Result ?? new List<AppWatchZoneResult>();
                foreach (AppWatchZoneResult zoneResult in results)
                {
                    string summary = string.IsNullOrWhiteSpace(zoneResult.Error)
                        ? zoneResult.Result.Summary
                        : zoneResult.Error;

                    AppendLog(reason + " 결과(" + zoneResult.ZoneName + "): " + summary);
                    if (string.Equals(_currentZoneId, zoneResult.ZoneId, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateAppWatchStatusLabel(summary);
                    }

                    if (zoneResult.Result != null && zoneResult.Result.LaunchAttempted)
                    {
                        ShowTrayNotification("앱 감시 재실행", zoneResult.ZoneName + " · " + BuildLaunchNotificationText(zoneResult.LaunchTarget));
                    }
                }

                if (showMessage)
                {
                    AppWatchZoneResult firstResult = results.FirstOrDefault();
                    string message = firstResult == null
                        ? "앱 감시 결과가 없습니다."
                        : string.IsNullOrWhiteSpace(firstResult.Error) ? firstResult.Result.Summary : firstResult.Error;
                    bool ok = firstResult != null && firstResult.Result != null && firstResult.Result.IsRunning;
                    MessageBox.Show(this, message, reason, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void UpdateAppWatchStatusLabel(string text)
        {
            if (_appWatchStatusLabel != null)
            {
                _appWatchStatusLabel.Text = text;
            }
        }

        private void ShowTrayNotification(string title, string message)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string, string>(ShowTrayNotification), title, message);
                }
                catch
                {
                }
                return;
            }

            if (_trayIcon == null)
            {
                return;
            }

            try
            {
                _trayIcon.ShowBalloonTip(2500, title, message, ToolTipIcon.Info);
            }
            catch
            {
            }
        }

        private static string BuildLaunchNotificationText(string target)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return "등록된 프로그램을 실행했습니다.";
            }

            string display = TryGetAppsFolderDisplayName(value);
            if (string.IsNullOrWhiteSpace(display))
            {
                display = value;
                try
                {
                    if (display.IndexOf(Path.DirectorySeparatorChar) >= 0 || display.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(display);
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            display = fileName;
                        }
                    }
                }
                catch
                {
                }
            }

            return ShortenText(display, 80) + " 실행됨";
        }

        private void ShowCommandHelp()
        {
            string text =
                "고급 명령어는 위치에 진입했을 때 위에서 아래로 한 줄씩 실행됩니다." + Environment.NewLine + Environment.NewLine +
                "실행 방식" + Environment.NewLine +
                "- 각 줄은 cmd.exe /c 로 실행됩니다." + Environment.NewLine +
                "- 경로에 공백이 있으면 따옴표로 감싸주세요." + Environment.NewLine +
                "- 앱 실행이나 Chrome 링크로 표현하기 어려운 작업만 여기에 넣는 것을 권장합니다." + Environment.NewLine + Environment.NewLine +
                "예시" + Environment.NewLine +
                "notepad.exe" + Environment.NewLine +
                "explorer.exe C:\\Users" + Environment.NewLine +
                "start \"\" \"C:\\path\\file.txt\"" + Environment.NewLine +
                "powershell -NoProfile -Command \"Write-Output hello\"";

            MessageBox.Show(this, text, "고급 명령어 도움말", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void InsertCommandExample()
        {
            string example =
                "notepad.exe" + Environment.NewLine +
                "explorer.exe C:\\Users";

            if (string.IsNullOrWhiteSpace(_commandsText.Text))
            {
                _commandsText.Text = example;
            }
            else
            {
                _commandsText.AppendText(Environment.NewLine + example);
            }

            _commandsText.Focus();
            _commandsText.SelectionStart = _commandsText.TextLength;
            CaptureCurrentZone();
        }

        private Button CreateAppLaunchChip(string target)
        {
            Button chip = CreateValueChip(target, ShortenAppTargetForChip(target), string.Equals(_selectedAppLaunch, target, StringComparison.OrdinalIgnoreCase));
            chip.Click += delegate
            {
                _selectedAppLaunch = Convert.ToString(chip.Tag);
                RenderAppLaunchChips();
            };
            return chip;
        }

        private string ShortenAppTargetForChip(string target)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return "";
            }

            string appsFolderName = TryGetAppsFolderDisplayName(value);
            if (!string.IsNullOrWhiteSpace(appsFolderName))
            {
                return ShortenText(appsFolderName, 34);
            }

            try
            {
                if (value.IndexOf(Path.DirectorySeparatorChar) >= 0 || value.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(value);
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        return ShortenText(fileName, 34);
                    }
                }
            }
            catch
            {
            }

            return ShortenText(value, 34);
        }

        private static string TryGetAppsFolderDisplayName(string target)
        {
            string value = (target ?? "").Trim();
            const string prefix = @"shell:AppsFolder\";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string appId = value.Substring(prefix.Length);
            int bang = appId.IndexOf('!');
            if (bang > 0)
            {
                appId = appId.Substring(0, bang);
            }

            int underscore = appId.IndexOf('_');
            if (underscore > 0)
            {
                appId = appId.Substring(0, underscore);
            }

            int dot = appId.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < appId.Length)
            {
                appId = appId.Substring(dot + 1);
            }

            return appId.Trim();
        }

        private void RenderAppLaunchChips()
        {
            if (_appLaunchChipsPanel == null)
            {
                return;
            }

            List<string> apps = GetAppLaunches();
            _appLaunchChipsPanel.Controls.Clear();
            if (apps.Count == 0)
            {
                _selectedAppLaunch = null;
                AddEmptyChipHint(_appLaunchChipsPanel, "등록된 앱 없음");
                return;
            }

            if (string.IsNullOrWhiteSpace(_selectedAppLaunch) || !apps.Any(app => string.Equals(app, _selectedAppLaunch, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedAppLaunch = apps[0];
            }

            foreach (string app in apps)
            {
                _appLaunchChipsPanel.Controls.Add(CreateAppLaunchChip(app));
            }
        }

        private void AddEmptyChipHint(FlowLayoutPanel panel, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = UiTextMuted;
            label.Tag = "Muted";
            label.Margin = new Padding(8, 8, 8, 4);
            panel.Controls.Add(label);
        }

        private static string NormalizeUrlForDisplay(string value)
        {
            string url = (value ?? "").Trim();
            if (url.Length == 0)
            {
                return "";
            }

            if (url.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                url = "https://" + url;
            }

            return url;
        }

        private void KeepDetailPaneReadable(SplitContainer split)
        {
            const int preferredLeftWidth = 320;
            const int minimumLeftWidth = 300;
            const int minimumDetailWidth = 560;

            if (split == null || split.Width <= 0)
            {
                return;
            }

            int availableWidth = split.Width - split.SplitterWidth;
            int maxLeftWidth = availableWidth - minimumDetailWidth;
            if (maxLeftWidth < minimumLeftWidth)
            {
                maxLeftWidth = Math.Max(minimumLeftWidth, availableWidth - 420);
            }

            int target = Math.Min(preferredLeftWidth, maxLeftWidth);
            target = Math.Max(minimumLeftWidth, target);

            if (split.Panel2.Width >= minimumDetailWidth &&
                split.SplitterDistance >= minimumLeftWidth &&
                split.SplitterDistance <= maxLeftWidth)
            {
                return;
            }

            if (split.SplitterDistance != target)
            {
                split.SplitterDistance = target;
            }
        }

        private void AddRow(string labelText, Control control)
        {
            AddRowTo(_conditionTable, labelText, control);
        }

        private void AddRowTo(TableLayoutPanel table, string labelText, Control control)
        {
            if (table == null || control == null)
            {
                return;
            }

            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Font = new Font(Font.FontFamily, 8.75F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = UiTextMuted;
            label.Margin = new Padding(0, 7, 10, 5);
            label.TextAlign = ContentAlignment.TopLeft;
            label.Tag = "Muted";
            table.Controls.Add(label, 0, row);

            control.Margin = new Padding(0, 3, 0, 5);
            table.Controls.Add(control, 1, row);
        }

        private void AddSectionHeader(string text)
        {
            AddSectionHeaderTo(_conditionTable, text);
        }

        private void AddSectionHeaderTo(TableLayoutPanel table, string text)
        {
            if (table == null)
            {
                return;
            }

            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label label = new Label();
            label.Text = text;
            label.Font = new Font(Font.FontFamily, 9.75F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = UiAccentDark;
            label.AutoSize = false;
            label.Height = 28;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.BackColor = UiAccentSoft;
            label.Margin = new Padding(0, 9, 0, 6);
            label.Padding = new Padding(10, 0, 0, 0);
            label.Tag = "SectionHeader";

            table.Controls.Add(label, 0, row);
            table.SetColumnSpan(label, 2);
        }

        private void ConfigureTray()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("열기", null, delegate { ShowMainWindow(); });
            menu.Items.Add("테스트해보기", null, delegate { TestSelectedZoneCondition(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, delegate
            {
                _allowExit = true;
                Close();
            });

            _trayIcon = new NotifyIcon();
            _trayIcon.Icon = AppIcons.GetAppIcon();
            _trayIcon.Text = "위치 자동 실행";
            _trayIcon.Visible = true;
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += delegate { ShowMainWindow(); };
        }

        private void BindConfigToControls()
        {
            _loadingSelection = true;
            _startupCheck.Checked = StartupManager.IsEnabled();
            _startMinimizedCheck.Checked = _config.StartMinimized;
            _loadingSelection = false;

            string selectedId = _config.Zones.Count > 0 ? _config.Zones[0].Id : null;
            BindZoneList(selectedId);
            AppendLog("설정을 불러왔습니다: " + ConfigStore.ConfigPath);
        }

        private void BindZoneList(string selectedId)
        {
            _loadingSelection = true;

            ListBox activeBefore = GetCurrentZoneList();
            _allZoneList.Items.Clear();
            _activeZoneList.Items.Clear();
            _inactiveZoneList.Items.Clear();

            int activeCount = 0;
            int inactiveCount = 0;

            foreach (ZoneRule zone in _config.Zones)
            {
                ZoneListItem item = new ZoneListItem(zone, this);
                _allZoneList.Items.Add(item);

                if (zone.Enabled)
                {
                    _activeZoneList.Items.Add(new ZoneListItem(zone, this));
                    activeCount++;
                }
                else
                {
                    _inactiveZoneList.Items.Add(new ZoneListItem(zone, this));
                    inactiveCount++;
                }
            }

            _allZonesTab.Text = "전체 " + _config.Zones.Count;
            _activeZonesTab.Text = "운영 중 " + activeCount;
            _inactiveZonesTab.Text = "미운영 " + inactiveCount;

            if (!string.IsNullOrEmpty(selectedId))
            {
                SelectZoneInAvailableList(selectedId, activeBefore);
            }

            if (GetSelectedZoneFromLists() == null && _allZoneList.Items.Count > 0)
            {
                _allZoneList.SelectedIndex = 0;
            }

            _loadingSelection = false;
            LoadSelectedZoneToControls();
        }

        private void ZoneListSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingSelection)
            {
                return;
            }

            CaptureCurrentZone();
            ListBox source = sender as ListBox;
            ZoneListItem selected = source == null ? null : source.SelectedItem as ZoneListItem;
            if (selected != null)
            {
                _currentZoneId = selected.Zone.Id;
                SyncZoneSelectionAcrossLists(source, _currentZoneId);
            }
            LoadSelectedZoneToControls();
        }

        private ListBox GetCurrentZoneList()
        {
            if (_zoneTabs == null || _zoneTabs.SelectedTab == null)
            {
                return _allZoneList;
            }

            if (_zoneTabs.SelectedTab == _activeZonesTab)
            {
                return _activeZoneList;
            }

            if (_zoneTabs.SelectedTab == _inactiveZonesTab)
            {
                return _inactiveZoneList;
            }

            return _allZoneList;
        }

        private void SelectZoneInAvailableList(string zoneId, ListBox preferredList)
        {
            if (TrySelectZoneInList(preferredList, zoneId))
            {
                return;
            }

            if (TrySelectZoneInList(GetCurrentZoneList(), zoneId))
            {
                return;
            }

            ZoneRule zone = FindZone(zoneId);
            if (zone != null && zone.Enabled && TrySelectZoneInList(_activeZoneList, zoneId))
            {
                _zoneTabs.SelectedTab = _activeZonesTab;
                return;
            }

            if (TrySelectZoneInList(_inactiveZoneList, zoneId))
            {
                _zoneTabs.SelectedTab = _inactiveZonesTab;
                return;
            }

            TrySelectZoneInList(_allZoneList, zoneId);
        }

        private bool TrySelectZoneInList(ListBox list, string zoneId)
        {
            if (list == null || string.IsNullOrEmpty(zoneId))
            {
                return false;
            }

            for (int i = 0; i < list.Items.Count; i++)
            {
                ZoneListItem item = list.Items[i] as ZoneListItem;
                if (item != null && string.Equals(item.Zone.Id, zoneId, StringComparison.OrdinalIgnoreCase))
                {
                    list.SelectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void SyncZoneSelectionAcrossLists(ListBox source, string zoneId)
        {
            _loadingSelection = true;
            ClearSelectionUnless(_allZoneList, source);
            ClearSelectionUnless(_activeZoneList, source);
            ClearSelectionUnless(_inactiveZoneList, source);
            TrySelectZoneInList(_allZoneList == source ? null : _allZoneList, zoneId);
            TrySelectZoneInList(_activeZoneList == source ? null : _activeZoneList, zoneId);
            TrySelectZoneInList(_inactiveZoneList == source ? null : _inactiveZoneList, zoneId);
            _loadingSelection = false;
        }

        private void ClearSelectionUnless(ListBox list, ListBox source)
        {
            if (list != null && list != source)
            {
                list.ClearSelected();
            }
        }

        private void SetDetailAreaEnabled(bool enabled)
        {
            SetChildControlsEnabled(_conditionTable, enabled);
            SetChildControlsEnabled(_actionTable, enabled);
        }

        private static void SetChildControlsEnabled(Control root, bool enabled)
        {
            if (root == null)
            {
                return;
            }

            foreach (Control child in root.Controls)
            {
                child.Enabled = enabled;
            }
        }

        private void LoadSelectedZoneToControls()
        {
            ZoneRule zone = GetSelectedZone();
            _loadingSelection = true;

            if (zone == null)
            {
                _currentZoneId = null;
                SetDetailAreaEnabled(false);
                _zoneEnabledCheck.Checked = false;
                _runOnceStartupCheck.Checked = false;
                _monitoringCheck.Checked = false;
                _intervalInput.Value = 30;
                _zoneNameText.Text = "";
                _useCoordinatesCheck.Checked = false;
                _latitudeText.Text = "";
                _longitudeText.Text = "";
                _radiusInput.Value = 200;
                RenderWifiChoiceButtons(new List<string>(), _lastVisibleNetworks);
                _requireAllSsidsCheck.Checked = false;
                _connectWifiCheck.Checked = false;
                _connectProfileText.Text = "";
                _connectSsidText.Text = "";
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                SetAudioActionSelection("None");
                SetChromeUrls(new List<string>());
                SetAppLaunches(new List<string>());
                _appWatchEnabledCheck.Checked = false;
                _appWatchTargetText.Text = "";
                _appWatchProcessText.Text = "";
                SetAppWatchIntervalUnitSelection("Minutes");
                _appWatchIntervalInput.Value = 5;
                UpdateAppWatchStatusLabel("아직 확인 전입니다.");
                _commandsText.Text = "";
            }
            else
            {
                zone.Normalize();
                _currentZoneId = zone.Id;
                SetDetailAreaEnabled(true);
                _zoneEnabledCheck.Checked = zone.Enabled;
                _runOnceStartupCheck.Checked = zone.RunOnceAtStartup.GetValueOrDefault(true);
                _monitoringCheck.Checked = zone.MonitoringEnabled.GetValueOrDefault(false);
                _intervalInput.Value = Math.Max(_intervalInput.Minimum, Math.Min(_intervalInput.Maximum, zone.ScanIntervalSeconds));
                _zoneNameText.Text = zone.Name;
                _useCoordinatesCheck.Checked = zone.UseCoordinates;
                _latitudeText.Text = FormatCoordinate(zone.Latitude);
                _longitudeText.Text = FormatCoordinate(zone.Longitude);
                _radiusInput.Value = Math.Max(_radiusInput.Minimum, Math.Min(_radiusInput.Maximum, zone.RadiusMeters));
                RenderWifiChoiceButtons(zone.NearbySsids, _lastVisibleNetworks);
                _requireAllSsidsCheck.Checked = zone.RequireAllSsids;
                _connectWifiCheck.Checked = zone.ConnectWifiEnabled.GetValueOrDefault(false);
                _connectProfileText.Text = zone.ConnectProfile;
                _connectSsidText.Text = zone.ConnectSsid;
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                SetAudioActionSelection(zone.AudioAction);
                SetChromeUrls(zone.ChromeUrls);
                SetAppLaunches(zone.AppLaunches);
                _appWatchEnabledCheck.Checked = zone.AppWatchEnabled.GetValueOrDefault(false);
                _appWatchTargetText.Text = zone.AppWatchLaunchTarget ?? "";
                _appWatchProcessText.Text = zone.AppWatchProcessName ?? "";
                SetAppWatchIntervalUnitSelection(zone.AppWatchIntervalUnit);
                _appWatchIntervalInput.Value = Math.Max(_appWatchIntervalInput.Minimum, Math.Min(_appWatchIntervalInput.Maximum, zone.AppWatchIntervalValue));
                UpdateAppWatchStatusLabel("아직 확인 전입니다.");
                _commandsText.Text = JoinLines(zone.Commands);
            }

            _loadingSelection = false;
            SetCoordinateInputsEnabled();
            UpdateSelectedZoneSummary();
        }

        private void CaptureCurrentZone()
        {
            if (_loadingSelection || string.IsNullOrEmpty(_currentZoneId))
            {
                return;
            }

            ZoneRule zone = FindZone(_currentZoneId);
            if (zone == null)
            {
                return;
            }

            zone.Enabled = _zoneEnabledCheck.Checked;
            zone.RunOnceAtStartup = _runOnceStartupCheck.Checked;
            zone.MonitoringEnabled = _monitoringCheck.Checked;
            zone.ScanIntervalSeconds = Convert.ToInt32(_intervalInput.Value);
            zone.Name = string.IsNullOrWhiteSpace(_zoneNameText.Text) ? "이름 없는 위치" : _zoneNameText.Text.Trim();
            zone.UseCoordinates = _useCoordinatesCheck.Checked;
            zone.Latitude = ParseCoordinate(_latitudeText.Text, zone.Latitude);
            zone.Longitude = ParseCoordinate(_longitudeText.Text, zone.Longitude);
            zone.RadiusMeters = Convert.ToInt32(_radiusInput.Value);
            zone.NearbySsids = GetSelectedWifiSsids();
            zone.RequireAllSsids = _requireAllSsidsCheck.Checked;
            zone.ConnectWifiEnabled = _connectWifiCheck.Checked;
            zone.ConnectProfile = _connectProfileText.Text.Trim();
            zone.ConnectSsid = _connectSsidText.Text.Trim();
            zone.AudioAction = ReadAudioActionSelection();
            if (string.IsNullOrWhiteSpace(zone.AudioAction))
            {
                zone.AudioAction = "None";
            }
            zone.ChromeUrls = GetChromeUrls();
            zone.AppLaunches = GetAppLaunches();
            zone.AppWatchEnabled = _appWatchEnabledCheck.Checked;
            zone.AppWatchLaunchTarget = _appWatchTargetText.Text.Trim();
            zone.AppWatchProcessName = _appWatchProcessText.Text.Trim();
            zone.AppWatchIntervalValue = Convert.ToInt32(_appWatchIntervalInput.Value);
            zone.AppWatchIntervalUnit = ReadAppWatchIntervalUnitSelection();
            zone.Commands = SplitLines(_commandsText.Text);
            UpdateSelectedZoneSummary();
        }

        private void CaptureGlobalSettings()
        {
            if (_loadingSelection)
            {
                return;
            }

            _config.MonitoringEnabled = HasContinuousMonitoringZones();
            _config.RunOnceAtStartup = HasStartupRunOnceZones();
            _config.ScanIntervalSeconds = GetShortestContinuousScanIntervalSeconds();
            _config.StartMinimized = _startMinimizedCheck.Checked;
            _config.AppWatchEnabled = HasAppWatchZones();
            ZoneRule firstAppWatchZone = _config.Zones.FirstOrDefault(z => z.Enabled && z.AppWatchEnabled.GetValueOrDefault(false));
            _config.AppWatchLaunchTarget = firstAppWatchZone == null ? "" : firstAppWatchZone.AppWatchLaunchTarget;
            _config.AppWatchProcessName = firstAppWatchZone == null ? "" : firstAppWatchZone.AppWatchProcessName;
            _config.AppWatchIntervalValue = firstAppWatchZone == null ? 5 : firstAppWatchZone.AppWatchIntervalValue;
            _config.AppWatchIntervalUnit = firstAppWatchZone == null ? "Minutes" : firstAppWatchZone.AppWatchIntervalUnit;
        }

        private static void RestoreDetectionCondition(ZoneRule target, ZoneRule source)
        {
            if (target == null || source == null)
            {
                return;
            }

            target.UseCoordinates = source.UseCoordinates;
            target.Latitude = source.Latitude;
            target.Longitude = source.Longitude;
            target.RadiusMeters = source.RadiusMeters;
            target.NearbySsids = source.NearbySsids == null ? new List<string>() : new List<string>(source.NearbySsids);
            target.RequireAllSsids = source.RequireAllSsids;
        }

        private void SetCoordinateInputsEnabled()
        {
            bool enabled = _useCoordinatesCheck != null && _useCoordinatesCheck.Checked;
            if (_latitudeText != null)
            {
                _latitudeText.Enabled = enabled;
                _latitudeText.BackColor = enabled ? UiSurface : UiSurfaceMuted;
                _latitudeText.ForeColor = enabled ? UiText : UiTextMuted;
            }
            if (_longitudeText != null)
            {
                _longitudeText.Enabled = enabled;
                _longitudeText.BackColor = enabled ? UiSurface : UiSurfaceMuted;
                _longitudeText.ForeColor = enabled ? UiText : UiTextMuted;
            }
            if (_radiusInput != null)
            {
                _radiusInput.Enabled = enabled;
                _radiusInput.BackColor = enabled ? UiSurface : UiSurfaceMuted;
                _radiusInput.ForeColor = enabled ? UiText : UiTextMuted;
            }
        }

        private void FillSelectedZoneFromCurrentLocation()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            AppendLog("현재 Windows 위치를 읽는 중입니다...");
            Task.Factory.StartNew(delegate
            {
                return LocationLocator.GetCurrentLocation();
            }).ContinueWith(delegate(Task<LocationReadResult> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 위치 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("위치 읽기 실패: " + message);
                    MessageBox.Show(this, message, "위치 읽기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                LocationReadResult result = task.Result;
                if (!result.HasLocation)
                {
                    string message = string.IsNullOrWhiteSpace(result.Error) ? "Windows 위치를 사용할 수 없습니다." : result.Error;
                    AppendLog("위치 읽기 실패: " + message);
                    MessageBox.Show(this, message, "위치 사용 불가", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _useCoordinatesCheck.Checked = true;
                _latitudeText.Text = FormatCoordinate(result.Location.Latitude);
                _longitudeText.Text = FormatCoordinate(result.Location.Longitude);
                if (_radiusInput.Value < 100)
                {
                    _radiusInput.Value = 200;
                }

                CaptureCurrentZone();
                _currentLocationLabel.Text = FormatLocation(result.Location);
                AppendLog("현재 좌표를 위치에 적용했습니다: " + selected.Name);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void CreateZoneFromCurrentLocation()
        {
            CaptureCurrentZone();
            ZoneRule zone = ZoneRule.CreateDefault("현재 위치");
            zone.NearbySsids.Clear();
            _config.Zones.Add(zone);
            BindZoneList(zone.Id);
            AppendLog("현재 좌표로 새 위치를 등록하는 중입니다...");

            Task.Factory.StartNew(delegate
            {
                return LocationLocator.GetCurrentLocation();
            }).ContinueWith(delegate(Task<LocationReadResult> task)
            {
                ZoneRule created = FindZone(zone.Id);
                if (created == null)
                {
                    return;
                }

                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 위치 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("현재 위치 등록 실패: " + message);
                    return;
                }

                LocationReadResult result = task.Result;
                if (!result.HasLocation)
                {
                    string message = string.IsNullOrWhiteSpace(result.Error) ? "Windows 위치를 사용할 수 없습니다." : result.Error;
                    AppendLog("현재 위치 등록 실패: " + message);
                    return;
                }

                created.UseCoordinates = true;
                created.Latitude = result.Location.Latitude;
                created.Longitude = result.Location.Longitude;
                created.RadiusMeters = Math.Max(200, created.RadiusMeters);
                BindZoneList(created.Id);
                _currentLocationLabel.Text = FormatLocation(result.Location);
                SaveFromUi();
                AppendLog("현재 위치가 등록되었습니다: " + created.Name);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void FillSelectedZoneFromVisibleNetworks()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            AppendLog("현재 보이는 Wi-Fi를 확인하는 중입니다...");
            Task.Factory.StartNew(delegate
            {
                return WifiLocator.GetVisibleNetworks(true);
            }).ContinueWith(delegate(Task<List<WifiNetwork>> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 Wi-Fi 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("Wi-Fi 확인 실패: " + message);
                    MessageBox.Show(this, message, "Wi-Fi 확인 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                List<WifiNetwork> networks = task.Result
                    .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                    .OrderByDescending(n => n.SignalQuality)
                    .ThenBy(n => n.Ssid)
                    .Take(5)
                    .ToList();

                if (networks.Count == 0)
                {
                    AppendLog("보이는 Wi-Fi가 없습니다.");
                    MessageBox.Show(this, "현재 보이는 Wi-Fi가 없습니다.", "Wi-Fi 없음", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _useCoordinatesCheck.Checked = false;
                _lastVisibleNetworks = networks;
                RenderWifiChoiceButtons(networks.Select(n => n.Ssid), _lastVisibleNetworks);
                RenderConnectWifiTargetButtons(_lastVisibleNetworks);
                _requireAllSsidsCheck.Checked = false;
                if (_connectWifiCheck.Checked && string.IsNullOrWhiteSpace(_connectProfileText.Text))
                {
                    SetConnectWifiTarget(networks[0].Ssid);
                }
                _visibleNetworksLabel.Text = string.Join(", ", networks.Select(n => n.Ssid + " " + n.SignalQuality + "%").ToArray());
                CaptureCurrentZone();
                AppendLog("현재 보이는 Wi-Fi를 위치 감지 조건에 적용했습니다: " + selected.Name);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SaveFromUi()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();

            if (HasStartupRunOnceZones() && !_startupCheck.Checked)
            {
                _startupCheck.Checked = true;
            }

            try
            {
                ConfigStore.Save(_config);
                StartupManager.SetEnabled(_startupCheck.Checked, _config.StartMinimized);
                ResetScanTimer();
                ResetAppWatchTimer();
                BindZoneList(_currentZoneId);
                AppendLog("저장했습니다.");
            }
            catch (Exception ex)
            {
                AppendLog("저장 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveSelectedZone()
        {
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this, "'" + selected.Name + "' 위치를 삭제할까요?", "위치 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }

            _config.Zones.Remove(selected);
            _insideZones.Remove(selected.Id);
            _currentZoneId = null;
            BindZoneList(_config.Zones.Count > 0 ? _config.Zones[0].Id : null);
            AppendLog("위치를 삭제했습니다: " + selected.Name);
        }

        private void DuplicateSelectedZone()
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();

            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                MessageBox.Show(this, "복제할 위치를 먼저 선택하세요.", "위치 복제", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ZoneRule copy = selected.Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = BuildDuplicateZoneName(selected.Name);
            copy.Enabled = false;
            copy.AppWatchEnabled = false;
            copy.Normalize();

            _config.Zones.Add(copy);
            CaptureGlobalSettings();
            ResetScanTimer();
            BindZoneList(copy.Id);
            _zoneTabs.SelectedTab = _inactiveZonesTab;
            TrySelectZoneInList(_inactiveZoneList, copy.Id);
            AppendLog("위치를 복제했습니다: " + selected.Name + " -> " + copy.Name + " (미운영)");
        }

        private string BuildDuplicateZoneName(string sourceName)
        {
            string baseName = string.IsNullOrWhiteSpace(sourceName) ? "이름 없는 위치" : sourceName.Trim();
            string first = baseName + " 복사본";
            if (!_config.Zones.Any(z => string.Equals(z.Name, first, StringComparison.OrdinalIgnoreCase)))
            {
                return first;
            }

            for (int i = 2; i < 1000; i++)
            {
                string candidate = first + " " + i;
                if (!_config.Zones.Any(z => string.Equals(z.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    return candidate;
                }
            }

            return first + " " + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private ZoneRule GetSelectedZone()
        {
            ZoneRule selected = GetSelectedZoneFromLists();
            if (selected != null)
            {
                return selected;
            }

            return string.IsNullOrWhiteSpace(_currentZoneId) ? null : FindZone(_currentZoneId);
        }

        private ZoneRule GetSelectedZoneFromLists()
        {
            ZoneListItem currentTabItem = GetCurrentZoneList() == null ? null : GetCurrentZoneList().SelectedItem as ZoneListItem;
            if (currentTabItem != null)
            {
                return currentTabItem.Zone;
            }

            ZoneListItem allItem = _allZoneList == null ? null : _allZoneList.SelectedItem as ZoneListItem;
            if (allItem != null)
            {
                return allItem.Zone;
            }

            ZoneListItem activeItem = _activeZoneList == null ? null : _activeZoneList.SelectedItem as ZoneListItem;
            if (activeItem != null)
            {
                return activeItem.Zone;
            }

            ZoneListItem inactiveItem = _inactiveZoneList == null ? null : _inactiveZoneList.SelectedItem as ZoneListItem;
            return inactiveItem == null ? null : inactiveItem.Zone;
        }

        private ZoneRule FindZone(string zoneId)
        {
            return _config.Zones.FirstOrDefault(z => string.Equals(z.Id, zoneId, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsZoneActive(ZoneRule zone)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.Id))
            {
                return false;
            }

            bool active;
            return _insideZones.TryGetValue(zone.Id, out active) && active;
        }

        private bool HasContinuousMonitoringZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.MonitoringEnabled.GetValueOrDefault(false));
        }

        private bool HasStartupRunOnceZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.RunOnceAtStartup.GetValueOrDefault(true));
        }

        private bool HasAppWatchZones()
        {
            return _config.Zones.Any(z => z.Enabled && z.AppWatchEnabled.GetValueOrDefault(false));
        }

        private int GetShortestContinuousScanIntervalSeconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled && z.MonitoringEnabled.GetValueOrDefault(false))
                .Select(z => z.ScanIntervalSeconds < 5 ? 30 : z.ScanIntervalSeconds)
                .ToList();

            return intervals.Count == 0 ? 30 : intervals.Min();
        }

        private int GetShortestAppWatchIntervalMilliseconds()
        {
            List<int> intervals = _config.Zones
                .Where(z => z.Enabled && z.AppWatchEnabled.GetValueOrDefault(false))
                .Select(z => GetAppWatchIntervalMilliseconds(z.AppWatchIntervalValue, z.AppWatchIntervalUnit))
                .ToList();

            return intervals.Count == 0 ? GetAppWatchIntervalMilliseconds(5, "Minutes") : intervals.Min();
        }

        private static bool IsZoneEligibleForScan(ZoneRule zone, bool startupOnly)
        {
            if (zone == null || !zone.Enabled)
            {
                return false;
            }

            return startupOnly
                ? zone.RunOnceAtStartup.GetValueOrDefault(true)
                : zone.MonitoringEnabled.GetValueOrDefault(false);
        }

        private void InvalidateZoneLists()
        {
            if (_allZoneList != null)
            {
                _allZoneList.Invalidate();
            }
            if (_activeZoneList != null)
            {
                _activeZoneList.Invalidate();
            }
            if (_inactiveZoneList != null)
            {
                _inactiveZoneList.Invalidate();
            }
        }

        private void ResetScanTimer()
        {
            _scanTimer.Stop();
            _scanTimer.Tick -= ScanTimerTick;
            _scanTimer.Interval = Math.Max(5, GetShortestContinuousScanIntervalSeconds()) * 1000;
            _scanTimer.Tick += ScanTimerTick;
            if (HasContinuousMonitoringZones())
            {
                _scanTimer.Start();
            }
        }

        private void ResetAppWatchTimer()
        {
            _appWatchTimer.Stop();
            if (HasAppWatchZones())
            {
                _appWatchTimer.Interval = GetShortestAppWatchIntervalMilliseconds();
                _appWatchTimer.Start();
            }
        }

        private void ScanTimerTick(object sender, EventArgs e)
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            if (HasContinuousMonitoringZones())
            {
                StartScan(false, false);
            }
        }

        private void AppWatchTimerTick(object sender, EventArgs e)
        {
            CaptureCurrentZone();
            CaptureGlobalSettings();
            if (HasAppWatchZones())
            {
                RunDueAppWatchChecks(false, "앱 감시");
            }
        }

        private void StartStartupRetrySequence()
        {
            _startupRetryAttemptsTotal = 8;
            _startupRetryAttemptsRemaining = _startupRetryAttemptsTotal;
            _startupRetryActive = true;
            _lastScanHadActiveZone = false;
            _startupRetryTimer.Stop();
            AppendLog("Windows 시작 실행: 부팅 초기 네트워크/위치 준비를 확인합니다.");
            RunStartupRetryAttempt();
        }

        private void StartupRetryTimerTick(object sender, EventArgs e)
        {
            RunStartupRetryAttempt();
        }

        private void RunStartupRetryAttempt()
        {
            if (!_startupRetryActive)
            {
                _startupRetryTimer.Stop();
                return;
            }

            if (_scanInProgress)
            {
                return;
            }

            if (!HasStartupRunOnceZones())
            {
                StopStartupRetry("시작 시 1회 실행 대상 위치가 없어 부팅 초기 확인을 종료합니다.");
                return;
            }

            if (_lastScanHadActiveZone)
            {
                StopStartupRetry("활성 위치가 인식되어 부팅 초기 확인을 종료합니다.");
                return;
            }

            if (_startupRetryAttemptsRemaining <= 0)
            {
                StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
                return;
            }

            int attemptNumber = _startupRetryAttemptsTotal - _startupRetryAttemptsRemaining + 1;
            _startupRetryAttemptsRemaining--;
            AppendLog("부팅 초기 확인 " + attemptNumber + "/" + _startupRetryAttemptsTotal);
            StartScan(true, true);

            if (_startupRetryAttemptsRemaining > 0)
            {
                _startupRetryTimer.Start();
            }
        }

        private void StopStartupRetry(string reason)
        {
            _startupRetryTimer.Stop();
            _startupRetryActive = false;
            AppendLog(reason);
        }

        private void StartScan(bool forceScan, bool startupOnly)
        {
            if (_scanInProgress)
            {
                return;
            }

            _scanInProgress = true;
            AppendLog(startupOnly
                ? "시작 시 1회 실행 조건을 확인하는 중입니다..."
                : forceScan ? "Wi-Fi와 위치를 확인하는 중입니다..." : "위치 조건을 확인하는 중입니다...");

            Task.Factory.StartNew(delegate
            {
                return CreateScanSnapshot(forceScan, _config.Zones.Any(z => z.Enabled && z.UseCoordinates));
            }).ContinueWith(delegate(Task<ScanSnapshot> task)
            {
                _scanInProgress = false;

                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 확인 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("위치 확인 실패: " + message);
                    if (_startupRetryActive && _startupRetryAttemptsRemaining <= 0)
                    {
                        StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
                    }
                    return;
                }

                ProcessScanResult(task.Result, startupOnly);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void SetSelectedZoneOperating(bool operating)
        {
            ZoneRule conditionSnapshot = null;
            ZoneRule selectedBeforeCapture = GetSelectedZone();
            if (selectedBeforeCapture != null)
            {
                conditionSnapshot = selectedBeforeCapture.Clone();
            }

            CaptureCurrentZone();
            CaptureGlobalSettings();

            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                MessageBox.Show(this, "운영 상태를 바꿀 위치를 먼저 선택하세요.", operating ? "운영하기" : "운영 안함", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (conditionSnapshot != null && string.Equals(conditionSnapshot.Id, selected.Id, StringComparison.OrdinalIgnoreCase))
            {
                RestoreDetectionCondition(selected, conditionSnapshot);
            }

            selected.Enabled = operating;
            if (_zoneEnabledCheck != null)
            {
                _zoneEnabledCheck.Checked = operating;
            }
            CaptureGlobalSettings();

            if (!operating)
            {
                _insideZones.Remove(selected.Id);
            }

            try
            {
                ConfigStore.Save(_config);
                StartupManager.SetEnabled(_startupCheck.Checked, _config.StartMinimized);
                ResetScanTimer();
                ResetAppWatchTimer();
                BindZoneList(selected.Id);

                if (operating)
                {
                    _zoneTabs.SelectedTab = _activeZonesTab;
                    TrySelectZoneInList(_activeZoneList, selected.Id);
                    AppendLog("운영을 시작했습니다: " + selected.Name);
                }
                else
                {
                    _zoneTabs.SelectedTab = _inactiveZonesTab;
                    TrySelectZoneInList(_inactiveZoneList, selected.Id);
                    AppendLog("운영을 중지했습니다: " + selected.Name);
                }
            }
            catch (Exception ex)
            {
                AppendLog("운영 상태 저장 실패: " + ex.Message);
                MessageBox.Show(this, ex.Message, "운영 상태 저장 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TestSelectedZoneCondition()
        {
            CaptureCurrentZone();
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            ZoneRule snapshotZone = selected.Clone();
            AppendLog("테스트해보기 중입니다: " + snapshotZone.Name);

            Task.Factory.StartNew(delegate
            {
                return CreateScanSnapshot(true, snapshotZone.UseCoordinates);
            }).ContinueWith(delegate(Task<ScanSnapshot> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 테스트 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("테스트해보기 실패: " + message);
                    MessageBox.Show(this, message, "테스트해보기 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ScanContext context = UpdateScanStatus(task.Result);
                bool matches = ZoneMatches(snapshotZone, context.VisibleSsids, context.CurrentLocation);
                _activeZonesLabel.Text = matches ? snapshotZone.Name : "없음";
                UpdateSelectedZoneSummary();

                string resultText = matches
                    ? "'" + snapshotZone.Name + "' 위치 조건이 현재 PC 상태와 일치합니다."
                    : "'" + snapshotZone.Name + "' 위치 조건이 현재 PC 상태와 일치하지 않습니다.";

                AppendLog("테스트해보기 결과: " + resultText);
                MessageBox.Show(this, resultText, "테스트해보기", MessageBoxButtons.OK, matches ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void TestSelectedWifiConnection()
        {
            CaptureCurrentZone();
            ZoneRule selected = GetSelectedZone();
            if (selected == null)
            {
                return;
            }

            string profile = selected.ConnectProfile == null ? "" : selected.ConnectProfile.Trim();
            string ssid = string.IsNullOrWhiteSpace(selected.ConnectSsid) ? profile : selected.ConnectSsid.Trim();
            if (string.IsNullOrWhiteSpace(profile))
            {
                MessageBox.Show(this, "연결할 Wi-Fi 프로필을 먼저 선택하거나 입력하세요.", "Wi-Fi 연결 테스트", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                this,
                "'" + ssid + "' Wi-Fi 연결을 지금 시도할까요?",
                "Wi-Fi 연결 테스트",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes)
            {
                return;
            }

            AppendLog("Wi-Fi 연결 테스트 중입니다: " + ssid);
            Task.Factory.StartNew(delegate
            {
                return WifiActions.Connect(profile, ssid);
            }).ContinueWith(delegate(Task<CommandResult> task)
            {
                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 Wi-Fi 연결 오류입니다." : task.Exception.GetBaseException().Message;
                    AppendLog("Wi-Fi 연결 테스트 실패: " + message);
                    MessageBox.Show(this, message, "Wi-Fi 연결 테스트 실패", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                AppendLog("Wi-Fi 연결 테스트 결과: " + task.Result.Summary);
                MessageBox.Show(this, task.Result.Summary, "Wi-Fi 연결 테스트 결과", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private ScanSnapshot CreateScanSnapshot(bool forceScan, bool requestLocation)
        {
            ScanSnapshot snapshot = new ScanSnapshot();

            try
            {
                snapshot.Networks = WifiLocator.GetVisibleNetworks(forceScan);
            }
            catch (Exception ex)
            {
                snapshot.Networks = new List<WifiNetwork>();
                snapshot.WifiError = ex.Message;
            }

            snapshot.LocationResult = requestLocation
                ? LocationLocator.GetCurrentLocation()
                : LocationReadResult.NotRequested();

            return snapshot;
        }

        private ScanContext UpdateScanStatus(ScanSnapshot snapshot)
        {
            List<WifiNetwork> networks = snapshot.Networks ?? new List<WifiNetwork>();
            List<WifiNetwork> ordered = networks
                .Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
                .OrderByDescending(n => n.SignalQuality)
                .ThenBy(n => n.Ssid)
                .ToList();

            if (!string.IsNullOrWhiteSpace(snapshot.WifiError))
            {
                AppendLog("Wi-Fi 확인 실패: " + snapshot.WifiError);
            }

            if (ordered.Count == 0)
            {
                _visibleNetworksLabel.Text = "보이는 Wi-Fi가 없습니다.";
            }
            else
            {
                _visibleNetworksLabel.Text = string.Join(", ", ordered.Take(12).Select(n => n.Ssid + " " + n.SignalQuality + "%").ToArray());
            }

            _lastVisibleNetworks = ordered;
            ZoneRule selectedZone = GetSelectedZone();
            IEnumerable<string> selectedWifi = selectedZone == null ? GetSelectedWifiSsids() : selectedZone.NearbySsids;
            RenderWifiChoiceButtons(selectedWifi, _lastVisibleNetworks);
            RenderConnectWifiTargetButtons(_lastVisibleNetworks);

            HashSet<string> visibleSsids = new HashSet<string>(ordered.Select(n => n.Ssid), StringComparer.OrdinalIgnoreCase);
            LocationInfo currentLocation = null;
            if (snapshot.LocationResult != null && snapshot.LocationResult.HasLocation)
            {
                currentLocation = snapshot.LocationResult.Location;
                _currentLocationLabel.Text = FormatLocation(currentLocation);
            }
            else if (snapshot.LocationResult != null && snapshot.LocationResult.WasRequested)
            {
                _currentLocationLabel.Text = "사용 불가: " + snapshot.LocationResult.Error;
                AppendLog("위치 사용 불가: " + snapshot.LocationResult.Error);
            }
            else
            {
                _currentLocationLabel.Text = "좌표 감지 위치가 켜져 있지 않습니다.";
            }

            return new ScanContext
            {
                VisibleSsids = visibleSsids,
                CurrentLocation = currentLocation,
                VisibleNetworks = ordered
            };
        }

        private void ProcessScanResult(ScanSnapshot snapshot, bool startupOnly)
        {
            ScanContext context = UpdateScanStatus(snapshot);
            HashSet<string> visibleSsids = context.VisibleSsids;
            LocationInfo currentLocation = context.CurrentLocation;

            List<string> activeZoneNames = new List<string>();
            bool zoneStateChanged = false;
            bool hadEligibleActiveZone = false;

            foreach (ZoneRule zone in _config.Zones)
            {
                zone.Normalize();
                bool eligible = IsZoneEligibleForScan(zone, startupOnly);
                bool near = zone.Enabled && ZoneMatches(zone, visibleSsids, currentLocation);
                bool wasInside = _insideZones.ContainsKey(zone.Id) && _insideZones[zone.Id];

                if (near)
                {
                    activeZoneNames.Add(zone.Name);
                }

                if (near && eligible)
                {
                    hadEligibleActiveZone = true;
                }

                if (eligible && near && !wasInside)
                {
                    _insideZones[zone.Id] = true;
                    zoneStateChanged = true;
                    TriggerZone(zone.Clone(), startupOnly ? "시작 시 1회 실행" : "위치 진입");
                }
                else if (!near && wasInside)
                {
                    _insideZones[zone.Id] = false;
                    zoneStateChanged = true;
                    AppendLog("위치에서 벗어났습니다: " + zone.Name);
                }
            }

            _activeZonesLabel.Text = activeZoneNames.Count == 0
                ? "없음"
                : string.Join(", ", activeZoneNames.ToArray());
            if (startupOnly)
            {
                _lastScanHadActiveZone = hadEligibleActiveZone;
            }

            if (zoneStateChanged)
            {
                BindZoneList(_currentZoneId);
            }
            else
            {
                InvalidateZoneLists();
            }

            UpdateSelectedZoneSummary();

            if (startupOnly && _startupRetryActive && _lastScanHadActiveZone)
            {
                StopStartupRetry("활성 위치가 인식되어 부팅 초기 확인을 종료합니다.");
            }
            else if (startupOnly && _startupRetryActive && _startupRetryAttemptsRemaining <= 0 && !_scanInProgress)
            {
                StopStartupRetry("활성 위치를 찾지 못해 부팅 초기 확인을 종료합니다.");
            }
        }

        private bool ZoneMatches(ZoneRule zone, HashSet<string> visibleSsids, LocationInfo currentLocation)
        {
            if (zone.UseCoordinates)
            {
                if (currentLocation == null)
                {
                    return false;
                }

                double distanceMeters = GeoMath.DistanceMeters(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    zone.Latitude,
                    zone.Longitude);

                return distanceMeters <= zone.RadiusMeters;
            }

            List<string> wanted = zone.NearbySsids
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (wanted.Count == 0)
            {
                return false;
            }

            if (zone.RequireAllSsids)
            {
                return wanted.All(visibleSsids.Contains);
            }

            return wanted.Any(visibleSsids.Contains);
        }

        private void TriggerZone(ZoneRule zone, string reason)
        {
            AppendLog(reason + ": " + zone.Name);
            ShowTrayNotification("위치 자동 실행", zone.Name + " 동작을 실행했습니다.");

            Task.Factory.StartNew(delegate
            {
                ZoneExecutor.Execute(zone, SafeLog);
            });
        }

        private void SafeLog(string message)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                }
                catch
                {
                }
            }
            else
            {
                AppendLog(message);
            }
        }

        private void AppendLog(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine;
            if (_recentLogLabel != null)
            {
                _recentLogLabel.Text = message;
            }

            if (_logText == null)
            {
                return;
            }

            _logText.AppendText(line);

            if (_logText.TextLength > 80000)
            {
                string trimmed = _logText.Text.Substring(_logText.TextLength - 50000);
                _logText.Text = trimmed;
                _logText.SelectionStart = _logText.TextLength;
                _logText.ScrollToCaret();
            }
        }

        private static List<string> SplitLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text.Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        private static string JoinLines(IEnumerable<string> values)
        {
            if (values == null)
            {
                return "";
            }

            return string.Join(Environment.NewLine, values.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());
        }

        private static double ParseCoordinate(string text, double fallback)
        {
            double value;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return fallback;
        }

        private static string FormatCoordinate(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatLocation(LocationInfo location)
        {
            if (location == null)
            {
                return "Unavailable.";
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:0.######}, {1:0.######} (accuracy {2:0} m)",
                location.Latitude,
                location.Longitude,
                location.AccuracyMeters);
        }

        private static bool IsKnownAudioAction(string value)
        {
            return string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Mute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Unmute", StringComparison.OrdinalIgnoreCase);
        }

        private void SetAudioActionSelection(string value)
        {
            if (string.Equals(value, "Mute", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "음소거", StringComparison.OrdinalIgnoreCase))
            {
                _audioActionCombo.SelectedItem = "음소거";
            }
            else if (string.Equals(value, "Unmute", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "음소거 해제", StringComparison.OrdinalIgnoreCase))
            {
                _audioActionCombo.SelectedItem = "음소거 해제";
            }
            else
            {
                _audioActionCombo.SelectedItem = "안 함";
            }
        }

        private string ReadAudioActionSelection()
        {
            string selected = Convert.ToString(_audioActionCombo.SelectedItem);
            if (string.Equals(selected, "음소거", StringComparison.OrdinalIgnoreCase))
            {
                return "Mute";
            }

            if (string.Equals(selected, "음소거 해제", StringComparison.OrdinalIgnoreCase))
            {
                return "Unmute";
            }

            return "None";
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (_startMinimizedRequested)
            {
                Hide();
            }

            if (_startedFromWindowsStartup && HasStartupRunOnceZones())
            {
                BeginInvoke(new Action(delegate
                {
                    StartStartupRetrySequence();
                }));
            }
            else if (HasContinuousMonitoringZones())
            {
                BeginInvoke(new Action(delegate { StartScan(false, false); }));
            }

            if (HasAppWatchZones())
            {
                BeginInvoke(new Action(delegate { RunDueAppWatchChecks(true, "앱 감시 시작 확인"); }));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _trayIcon.ShowBalloonTip(2000, "위치 자동 실행", "트레이에서 계속 실행 중입니다.", ToolTipIcon.Info);
                return;
            }

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }

            base.OnFormClosing(e);
        }

        private sealed class ZoneListItem
        {
            public ZoneRule Zone { get; private set; }
            private readonly MainForm _owner;

            public ZoneListItem(ZoneRule zone, MainForm owner)
            {
                Zone = zone;
                _owner = owner;
            }

            public string MetadataText
            {
                get
                {
                    string state = Zone.Enabled ? "운영 중" : "미운영";
                    string match = _owner.IsZoneActive(Zone) ? "조건 일치" : "대기";
                    string mode = Zone.UseCoordinates ? "좌표 " + Zone.RadiusMeters + "m" : "Wi-Fi";
                    List<string> schedules = new List<string>();
                    if (Zone.RunOnceAtStartup.GetValueOrDefault(true))
                    {
                        schedules.Add("시작 1회");
                    }
                    if (Zone.MonitoringEnabled.GetValueOrDefault(false))
                    {
                        schedules.Add("지속 " + Math.Max(5, Zone.ScanIntervalSeconds) + "초");
                    }
                    string schedule = schedules.Count == 0 ? "자동 실행 없음" : string.Join("/", schedules.ToArray());
                    return state + " · " + match + " · " + mode + " · " + schedule;
                }
            }

            public override string ToString()
            {
                return Zone.Name + " · " + MetadataText;
            }
        }
    }

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
            _resultList.DrawMode = DrawMode.OwnerDrawFixed;
            _resultList.ItemHeight = 48;
            _resultList.SelectionMode = SelectionMode.MultiExtended;
            _resultList.Margin = new Padding(0, 0, 0, 12);
            _resultList.DrawItem += DrawCandidateItem;
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

        private void DrawCandidateItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _resultList.Items.Count)
            {
                return;
            }

            AppSearchCandidate candidate = _resultList.Items[e.Index] as AppSearchCandidate;
            if (candidate == null)
            {
                e.DrawBackground();
                return;
            }

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? Color.FromArgb(20, 91, 69) : Color.FromArgb(253, 253, 249);
            Color nameColor = selected ? Color.White : Color.FromArgb(35, 45, 47);
            Color targetColor = selected ? Color.FromArgb(226, 239, 230) : Color.FromArgb(97, 111, 103);

            using (SolidBrush brush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            Rectangle nameRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 6, e.Bounds.Width - 20, 18);
            Rectangle targetRect = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 25, e.Bounds.Width - 20, 17);
            string target = BuildCandidateTargetText(candidate);

            using (Font bold = new Font(Font, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, candidate.Name, bold, nameRect, nameColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            TextRenderer.DrawText(e.Graphics, target, Font, targetRect, targetColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.DrawFocusRectangle();
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

    internal sealed class WifiNetwork
    {
        public string Ssid { get; set; }
        public string ProfileName { get; set; }
        public int SignalQuality { get; set; }
        public bool Connectable { get; set; }
    }

    internal sealed class AppSearchCandidate
    {
        public string Name { get; set; }
        public string Target { get; set; }
        public string AppId { get; set; }
        public string Source { get; set; }
        public string SearchText { get; set; }
    }

    internal sealed class ScanSnapshot
    {
        public List<WifiNetwork> Networks { get; set; }
        public string WifiError { get; set; }
        public LocationReadResult LocationResult { get; set; }
    }

    internal sealed class ScanContext
    {
        public HashSet<string> VisibleSsids { get; set; }
        public LocationInfo CurrentLocation { get; set; }
        public List<WifiNetwork> VisibleNetworks { get; set; }
    }

    internal sealed class LocationInfo
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AccuracyMeters { get; set; }
    }

    internal sealed class LocationReadResult
    {
        public bool WasRequested { get; set; }
        public LocationInfo Location { get; set; }
        public string Error { get; set; }

        public bool HasLocation
        {
            get { return Location != null; }
        }

        public static LocationReadResult NotRequested()
        {
            return new LocationReadResult
            {
                WasRequested = false,
                Location = null,
                Error = ""
            };
        }
    }

    internal static class LocationLocator
    {
        public static LocationReadResult GetCurrentLocation()
        {
            LocationReadResult result = new LocationReadResult { WasRequested = true, Error = "" };

            string script = @"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Devices.Geolocation.Geolocator,Windows.Devices.Geolocation,ContentType=WindowsRuntime] | Out-Null

function Await-WinRtOperation($operation, [Type]$resultType) {
    $method = [System.WindowsRuntimeSystemExtensions].GetMethods() |
        Where-Object {
            $_.Name -eq 'AsTask' -and
            $_.IsGenericMethodDefinition -and
            $_.GetParameters().Length -eq 1 -and
            $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
        } |
        Select-Object -First 1

    if ($null -eq $method) {
        throw 'Windows 위치 API helper를 찾을 수 없습니다.'
    }

    $task = $method.MakeGenericMethod($resultType).Invoke($null, @($operation))
    $task.Wait() | Out-Null
    return $task.Result
}

try {
    $accessOperation = [Windows.Devices.Geolocation.Geolocator]::RequestAccessAsync()
    $access = Await-WinRtOperation $accessOperation ([Windows.Devices.Geolocation.GeolocationAccessStatus])
    if ($access -ne [Windows.Devices.Geolocation.GeolocationAccessStatus]::Allowed) {
        throw ('Windows 위치 권한 상태: ' + $access + '. Windows 설정에서 위치 서비스와 데스크톱 앱 위치 접근을 켜세요.')
    }
} catch [System.Management.Automation.MethodException] {
    # Older Windows builds do not expose RequestAccessAsync; GetGeopositionAsync will report availability.
}

$locator = New-Object Windows.Devices.Geolocation.Geolocator
$locator.DesiredAccuracy = [Windows.Devices.Geolocation.PositionAccuracy]::Default
$position = Await-WinRtOperation $locator.GetGeopositionAsync() ([Windows.Devices.Geolocation.Geoposition])
$basic = $position.Coordinate.Point.Position
$accuracy = $position.Coordinate.Accuracy
$culture = [Globalization.CultureInfo]::InvariantCulture
[Console]::WriteLine(('{0}|{1}|{2}' -f $basic.Latitude.ToString($culture), $basic.Longitude.ToString($culture), $accuracy.ToString($culture)))
";

            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
            CommandResult command = CommandRunner.Run(
                powershellPath,
                "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                25000);

            if (command.TimedOut)
            {
                result.Error = "위치 요청 시간이 초과되었습니다.";
                return result;
            }

            if (command.ExitCode != 0)
            {
                result.Error = FirstNonEmpty(command.Error, command.Output, "위치 명령 실행에 실패했습니다.");
                return result;
            }

            string line = FirstNonEmpty(command.Output, "", "");
            string[] parts = line.Split('|');
            if (parts.Length < 2)
            {
                result.Error = "위치 명령 결과 형식이 예상과 다릅니다.";
                return result;
            }

            double latitude;
            double longitude;
            double accuracy;
            if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
                !double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
            {
                result.Error = "위치 좌표를 해석할 수 없습니다.";
                return result;
            }

            if (parts.Length < 3 || !double.TryParse(parts[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out accuracy))
            {
                accuracy = 0;
            }

            result.Location = new LocationInfo
            {
                Latitude = latitude,
                Longitude = longitude,
                AccuracyMeters = accuracy
            };
            return result;
        }

        private static string FirstNonEmpty(string first, string second, string fallback)
        {
            string line = FirstLine(first);
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }

            line = FirstLine(second);
            return string.IsNullOrWhiteSpace(line) ? fallback : line;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            return text.Replace("\r", "\n")
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .FirstOrDefault(s => s.Length > 0) ?? "";
        }
    }

    internal static class GeoMath
    {
        public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            const double earthRadiusMeters = 6371000.0;
            double lat1 = ToRadians(latitude1);
            double lat2 = ToRadians(latitude2);
            double deltaLat = ToRadians(latitude2 - latitude1);
            double deltaLon = ToRadians(longitude2 - longitude1);

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)
                + Math.Cos(lat1) * Math.Cos(lat2)
                * Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusMeters * c;
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }

    internal static class WifiLocator
    {
        public static List<WifiNetwork> GetVisibleNetworks(bool forceScan)
        {
            IntPtr handle = IntPtr.Zero;
            IntPtr interfacesPtr = IntPtr.Zero;
            List<WifiNetwork> networks = new List<WifiNetwork>();
            Dictionary<string, WifiNetwork> bestBySsid = new Dictionary<string, WifiNetwork>(StringComparer.OrdinalIgnoreCase);

            try
            {
                uint negotiated;
                int result = NativeMethods.WlanOpenHandle(2, IntPtr.Zero, out negotiated, out handle);
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                result = NativeMethods.WlanEnumInterfaces(handle, IntPtr.Zero, out interfacesPtr);
                if (result != 0)
                {
                    throw new Win32Exception(result);
                }

                int interfaceCount = Marshal.ReadInt32(interfacesPtr, 0);
                long listIterator = interfacesPtr.ToInt64() + 8;
                int interfaceSize = Marshal.SizeOf(typeof(NativeMethods.WLAN_INTERFACE_INFO));

                List<NativeMethods.WLAN_INTERFACE_INFO> interfaces = new List<NativeMethods.WLAN_INTERFACE_INFO>();
                for (int i = 0; i < interfaceCount; i++)
                {
                    IntPtr itemPtr = new IntPtr(listIterator + (i * interfaceSize));
                    NativeMethods.WLAN_INTERFACE_INFO wlanInterface =
                        (NativeMethods.WLAN_INTERFACE_INFO)Marshal.PtrToStructure(itemPtr, typeof(NativeMethods.WLAN_INTERFACE_INFO));
                    interfaces.Add(wlanInterface);
                }

                if (forceScan)
                {
                    foreach (NativeMethods.WLAN_INTERFACE_INFO wlanInterface in interfaces)
                    {
                        Guid interfaceGuid = wlanInterface.InterfaceGuid;
                        NativeMethods.WlanScan(handle, ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    }

                    Thread.Sleep(1300);
                }

                foreach (NativeMethods.WLAN_INTERFACE_INFO wlanInterface in interfaces)
                {
                    IntPtr networksPtr = IntPtr.Zero;
                    try
                    {
                        Guid interfaceGuid = wlanInterface.InterfaceGuid;
                        result = NativeMethods.WlanGetAvailableNetworkList(handle, ref interfaceGuid, 0, IntPtr.Zero, out networksPtr);
                        if (result != 0)
                        {
                            continue;
                        }

                        int networkCount = Marshal.ReadInt32(networksPtr, 0);
                        long networkIterator = networksPtr.ToInt64() + 8;
                        int networkSize = Marshal.SizeOf(typeof(NativeMethods.WLAN_AVAILABLE_NETWORK));

                        for (int i = 0; i < networkCount; i++)
                        {
                            IntPtr itemPtr = new IntPtr(networkIterator + (i * networkSize));
                            NativeMethods.WLAN_AVAILABLE_NETWORK network =
                                (NativeMethods.WLAN_AVAILABLE_NETWORK)Marshal.PtrToStructure(itemPtr, typeof(NativeMethods.WLAN_AVAILABLE_NETWORK));

                            string ssid = NativeMethods.SsidToString(network.dot11Ssid);
                            if (string.IsNullOrWhiteSpace(ssid))
                            {
                                continue;
                            }

                            WifiNetwork visible = new WifiNetwork
                            {
                                Ssid = ssid,
                                ProfileName = network.strProfileName ?? "",
                                SignalQuality = Convert.ToInt32(Math.Min(100, network.wlanSignalQuality)),
                                Connectable = network.bNetworkConnectable
                            };

                            WifiNetwork existing;
                            if (!bestBySsid.TryGetValue(visible.Ssid, out existing) || existing.SignalQuality < visible.SignalQuality)
                            {
                                bestBySsid[visible.Ssid] = visible;
                            }
                        }
                    }
                    finally
                    {
                        if (networksPtr != IntPtr.Zero)
                        {
                            NativeMethods.WlanFreeMemory(networksPtr);
                        }
                    }
                }
            }
            finally
            {
                if (interfacesPtr != IntPtr.Zero)
                {
                    NativeMethods.WlanFreeMemory(interfacesPtr);
                }

                if (handle != IntPtr.Zero)
                {
                    NativeMethods.WlanCloseHandle(handle, IntPtr.Zero);
                }
            }

            networks.AddRange(bestBySsid.Values);
            return networks;
        }
    }

    internal sealed class AppWatchCheckResult
    {
        public string ProcessName { get; set; }
        public int ProcessCount { get; set; }
        public bool IsRunning { get; set; }
        public bool LaunchAttempted { get; set; }
        public string Summary { get; set; }
    }

    internal sealed class AppWatchZoneResult
    {
        public string ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string LaunchTarget { get; set; }
        public AppWatchCheckResult Result { get; set; }
        public string Error { get; set; }
    }

    internal static class AppWatchdog
    {
        public static string InferProcessName(string target)
        {
            return NormalizeProcessName(target);
        }

        public static string NormalizeProcessName(string value)
        {
            string name = (value ?? "").Trim().Trim('"');
            if (name.Length == 0)
            {
                return "";
            }

            string appsFolderName = TryGetAppsFolderProcessName(name);
            if (!string.IsNullOrWhiteSpace(appsFolderName))
            {
                return appsFolderName;
            }

            try
            {
                string fileName = Path.GetFileName(name);
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    name = fileName;
                }
            }
            catch
            {
            }

            string extension = "";
            try
            {
                extension = Path.GetExtension(name);
            }
            catch
            {
            }

            if (string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".lnk", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    name = Path.GetFileNameWithoutExtension(name);
                }
                catch
                {
                }
            }

            return name.Trim();
        }

        private static string TryGetAppsFolderProcessName(string target)
        {
            string value = (target ?? "").Trim();
            const string prefix = @"shell:AppsFolder\";
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string appId = value.Substring(prefix.Length);
            int bang = appId.IndexOf('!');
            if (bang > 0)
            {
                appId = appId.Substring(0, bang);
            }

            int underscore = appId.IndexOf('_');
            if (underscore > 0)
            {
                appId = appId.Substring(0, underscore);
            }

            int dot = appId.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < appId.Length)
            {
                appId = appId.Substring(dot + 1);
            }

            return appId.Trim();
        }

        public static AppWatchCheckResult Check(string processName)
        {
            string normalized = NormalizeProcessName(processName);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new InvalidOperationException("확인할 프로세스 이름이 비어 있습니다.");
            }

            Process[] processes = Process.GetProcessesByName(normalized);
            try
            {
                int count = processes == null ? 0 : processes.Length;
                bool isRunning = count > 0;
                return new AppWatchCheckResult
                {
                    ProcessName = normalized,
                    ProcessCount = count,
                    IsRunning = isRunning,
                    LaunchAttempted = false,
                    Summary = isRunning
                        ? "실행 중: " + normalized + " (" + count + "개)"
                        : "실행 중 아님: " + normalized
                };
            }
            finally
            {
                if (processes != null)
                {
                    foreach (Process process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        public static AppWatchCheckResult EnsureRunning(string processName, string launchTarget, Action<string> log)
        {
            AppWatchCheckResult current = Check(processName);
            if (current.IsRunning)
            {
                return current;
            }

            string target = (launchTarget ?? "").Trim();
            if (target.Length == 0)
            {
                throw new InvalidOperationException("다시 실행할 앱 대상이 비어 있습니다.");
            }

            if (log != null)
            {
                log("앱 감시: 꺼진 상태라 다시 실행합니다: " + current.ProcessName);
            }

            AppLauncher.LaunchApp(target, log ?? delegate { });
            Thread.Sleep(2000);

            AppWatchCheckResult afterLaunch = Check(processName);
            afterLaunch.LaunchAttempted = true;
            afterLaunch.Summary = afterLaunch.IsRunning
                ? "다시 실행됨: " + afterLaunch.ProcessName + " (" + afterLaunch.ProcessCount + "개)"
                : "실행 요청 완료, 아직 프로세스 미확인: " + afterLaunch.ProcessName;
            return afterLaunch;
        }
    }

    internal static class AppLauncher
    {
        private static readonly object AppIndexLock = new object();
        private static List<AppSearchCandidate> _installedAppIndex;

        public static void OpenChromeUrl(string url, Action<string> log)
        {
            OpenChromeUrls(new List<string> { url }, log);
        }

        public static void OpenChromeUrls(IEnumerable<string> urls, Action<string> log)
        {
            List<string> normalizedUrls = (urls ?? new List<string>())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(NormalizeUrl)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedUrls.Count == 0)
            {
                return;
            }

            string chromePath = FindChromePath();

            if (!string.IsNullOrWhiteSpace(chromePath) && File.Exists(chromePath))
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = chromePath;
                startInfo.Arguments = string.Join(" ", normalizedUrls.Select(QuoteArgument).ToArray());
                startInfo.UseShellExecute = false;
                Process.Start(startInfo);
                log("Chrome 탭 실행 성공: " + normalizedUrls.Count + "개");
                foreach (string normalizedUrl in normalizedUrls)
                {
                    log("Chrome 탭: " + normalizedUrl);
                }
                return;
            }

            foreach (string normalizedUrl in normalizedUrls)
            {
                StartShellTarget(normalizedUrl);
                log("브라우저 탭 실행 성공: " + normalizedUrl);
            }
        }

        public static void LaunchApp(string target, Action<string> log)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return;
            }

            if (string.Equals(value, "ChatGPT", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchStartMenuApp("ChatGPT") || TryLaunchCommonPath(GetLocalPath(@"Programs\ChatGPT\ChatGPT.exe")) || TryStartShellTarget("chatgpt://"))
                {
                    log("앱 실행 성공: ChatGPT");
                }
                else
                {
                    log("앱 실행 실패: ChatGPT 앱을 찾지 못했습니다. 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (string.Equals(value, "Obsidian", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchStartMenuApp("Obsidian")
                    || TryLaunchCommonPath(GetLocalPath(@"Programs\Obsidian\Obsidian.exe"))
                    || TryLaunchCommonPath(GetLocalPath(@"Obsidian\Obsidian.exe"))
                    || TryStartShellTarget("obsidian://"))
                {
                    log("앱 실행 성공: Obsidian");
                }
                else
                {
                    log("앱 실행 실패: Obsidian 앱을 찾지 못했습니다. 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (string.Equals(value, "Teams", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Microsoft Teams", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchMicrosoftTeams())
                {
                    log("앱 실행 성공: Microsoft Teams");
                }
                else
                {
                    log("앱 실행 실패: Microsoft Teams 앱을 찾지 못했습니다. 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (string.Equals(value, "Docker", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Docker Desktop", StringComparison.OrdinalIgnoreCase))
            {
                if (TryLaunchStartMenuApp("Docker Desktop")
                    || TryLaunchCommonPath(GetProgramFilesPath(@"Docker\Docker\Docker Desktop.exe"))
                    || TryLaunchCommonPath(GetProgramFilesX86Path(@"Docker\Docker\Docker Desktop.exe")))
                {
                    log("앱 실행 성공: Docker Desktop");
                }
                else
                {
                    log("앱 실행 실패: Docker Desktop 앱을 찾지 못했습니다. Docker Desktop이 설치되어 있거나 시작 메뉴에 보여야 합니다.");
                }
                return;
            }

            if (File.Exists(value) || Directory.Exists(value) || LooksLikeShellTarget(value))
            {
                if (TryLaunchTargetWithFallback(value))
                {
                    log("앱 실행 성공: " + value);
                }
                else
                {
                    log("앱 실행 실패: 앱을 찾았지만 실행하지 못했습니다: " + value);
                }
                return;
            }

            if (TryLaunchStartMenuApp(value))
            {
                log("앱 실행 성공: " + value);
                return;
            }

            if (TryStartShellTarget(value))
            {
                log("앱 실행 성공: " + value);
                return;
            }

            log("앱 실행 실패: 앱을 찾지 못했습니다: " + value);
        }

        public static List<AppSearchCandidate> FindInstalledApps(string query, int limit)
        {
            return FindInstalledApps(query, limit, false);
        }

        public static List<AppSearchCandidate> FindInstalledApps(string query, int limit, bool includeAllWhenEmpty)
        {
            string needle = (query ?? "").Trim();

            return GetInstalledAppIndex(false)
                .Where(c => MatchesAppCandidate(c, needle, includeAllWhenEmpty))
                .OrderBy(c => RankAppCandidate(c, needle))
                .ThenBy(c => c.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, limit))
                .ToList();
        }

        public static void RefreshInstalledAppIndex()
        {
            lock (AppIndexLock)
            {
                _installedAppIndex = BuildInstalledAppIndex();
            }
        }

        private static List<AppSearchCandidate> GetInstalledAppIndex(bool forceRefresh)
        {
            lock (AppIndexLock)
            {
                if (forceRefresh || _installedAppIndex == null)
                {
                    _installedAppIndex = BuildInstalledAppIndex();
                }

                return _installedAppIndex.ToList();
            }
        }

        private static List<AppSearchCandidate> BuildInstalledAppIndex()
        {
            List<AppSearchCandidate> candidates = new List<AppSearchCandidate>();
            AddStartApps(candidates);
            AddStartMenuShortcuts(candidates);

            return candidates
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Target))
                .GroupBy(c => c.Target, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Target, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddStartApps(List<AppSearchCandidate> candidates)
        {
            try
            {
                string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
Get-StartApps | ForEach-Object {
    $name = [string]$_.Name
    $appId = [string]$_.AppID
    if (-not [string]::IsNullOrWhiteSpace($name)) {
        [Console]::WriteLine(($name.Replace('|', ' ') + '|' + $appId.Replace('|', ' ')))
    }
}
";
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                CommandResult result = CommandRunner.Run(
                    powershellPath,
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    8000);

                if (result.TimedOut || result.ExitCode != 0)
                {
                    return;
                }

                foreach (string line in (result.Output ?? "").Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.TrimStart().StartsWith("#<", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] parts = line.Split(new[] { '|' }, 2);
                    string name = parts.Length > 0 ? parts[0].Trim() : "";
                    string appId = parts.Length > 1 ? parts[1].Trim() : "";
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        string target = string.IsNullOrWhiteSpace(appId) ? name : @"shell:AppsFolder\" + appId;
                        candidates.Add(new AppSearchCandidate
                        {
                            Name = name,
                            Target = target,
                            AppId = appId,
                            Source = "시작 앱",
                            SearchText = name + " " + appId
                        });
                    }
                }
            }
            catch
            {
            }
        }

        private static void AddStartMenuShortcuts(List<AppSearchCandidate> candidates)
        {
            string[] roots = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs")
            };

            foreach (string root in roots)
            {
                try
                {
                    if (!Directory.Exists(root))
                    {
                        continue;
                    }

                    foreach (string shortcut in Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileNameWithoutExtension(shortcut);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            candidates.Add(new AppSearchCandidate
                            {
                                Name = name,
                                Target = shortcut,
                                AppId = "",
                                Source = "바로가기",
                                SearchText = name + " " + shortcut
                            });
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static bool MatchesAppCandidate(AppSearchCandidate candidate, string query, bool includeAllWhenEmpty)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Name))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return includeAllWhenEmpty || IsCommonSuggestion(candidate.Name);
            }

            return candidate.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrWhiteSpace(candidate.Target) && candidate.Target.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(candidate.AppId) && candidate.AppId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrWhiteSpace(candidate.SearchText) && candidate.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsCommonSuggestion(string name)
        {
            string[] preferred = { "ChatGPT", "Codex", "Claude", "Cursor", "Obsidian", "Docker", "Chrome", "Notepad", "PowerShell", "Visual Studio Code" };
            return preferred.Any(p => name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static int RankAppCandidate(AppSearchCandidate candidate, string query)
        {
            string name = candidate == null ? "" : candidate.Name;
            if (string.IsNullOrWhiteSpace(query))
            {
                return IsCommonSuggestion(name) ? 0 : 10;
            }

            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.AppId) && candidate.AppId.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }

            return 3;
        }

        private static string NormalizeUrl(string value)
        {
            string url = (value ?? "").Trim();
            if (url.IndexOf("://", StringComparison.Ordinal) < 0)
            {
                url = "https://" + url;
            }
            return url;
        }

        private static string FindChromePath()
        {
            string[] candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
            };

            return candidates.FirstOrDefault(File.Exists) ?? "";
        }

        private static bool TryLaunchStartMenuApp(string appName)
        {
            try
            {
                string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$name = __APP_NAME__
$app = Get-StartApps |
    Where-Object { $_.Name -eq $name -or $_.Name -like ('*' + $name + '*') } |
    Select-Object -First 1
if ($null -eq $app) { exit 3 }
Start-Process explorer.exe -ArgumentList ('shell:AppsFolder\' + $app.AppID)
";
                script = script.Replace("__APP_NAME__", PowerShellStringLiteral(appName));
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                string powershellPath = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                CommandResult result = CommandRunner.Run(
                    powershellPath,
                    "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                    12000);
                return !result.TimedOut && result.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLaunchMicrosoftTeams()
        {
            return TryLaunchStartMenuApp("Microsoft Teams")
                || TryLaunchStartMenuApp("Teams")
                || TryStartAppsFolderTarget(@"shell:AppsFolder\MSTeams_8wekyb3d8bbwe!MSTeams")
                || TryStartShellTarget(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Microsoft Teams.lnk"))
                || TryStartShellTarget(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Microsoft Teams.lnk"));
        }

        private static bool TryLaunchCommonPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            return TryLaunchTargetWithFallback(path);
        }

        private static string GetLocalPath(string relativePath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relativePath);
        }

        private static string GetProgramFilesPath(string relativePath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), relativePath);
        }

        private static string GetProgramFilesX86Path(string relativePath)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), relativePath);
        }

        private static bool TryStartShellTarget(string target)
        {
            try
            {
                StartShellTarget(target);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryLaunchTargetWithFallback(string target)
        {
            string value = (target ?? "").Trim();
            if (value.Length == 0)
            {
                return false;
            }

            if (value.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
            {
                return TryStartAppsFolderTarget(value) || TryStartShellTarget(value);
            }

            if (TryStartShellTarget(value))
            {
                return true;
            }

            if (IsShortcutPath(value))
            {
                string shortcutName = "";
                try
                {
                    shortcutName = Path.GetFileNameWithoutExtension(value);
                }
                catch
                {
                }

                ShortcutInfo shortcut = ReadShortcut(value);
                if (IsTeamsShortcut(shortcutName, shortcut))
                {
                    return TryLaunchMicrosoftTeams();
                }

                if (!string.IsNullOrWhiteSpace(shortcutName) && TryLaunchStartMenuApp(shortcutName))
                {
                    return true;
                }

                if (shortcut != null && !string.IsNullOrWhiteSpace(shortcut.TargetPath) && File.Exists(shortcut.TargetPath))
                {
                    return TryStartShellTarget(shortcut.TargetPath);
                }
            }

            return false;
        }

        private static bool TryStartAppsFolderTarget(string target)
        {
            return TryStartProcess(Path.Combine(Environment.SystemDirectory, "explorer.exe"), target);
        }

        private static bool TryStartProcess(string fileName, string arguments)
        {
            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = fileName;
                startInfo.Arguments = QuoteArgument(arguments);
                startInfo.UseShellExecute = false;
                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void StartShellTarget(string target)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = target;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
        }

        private static bool LooksLikeShellTarget(string target)
        {
            return target.IndexOf("://", StringComparison.Ordinal) > 0
                || target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("ms-", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsShortcutPath(string target)
        {
            try
            {
                return string.Equals(Path.GetExtension(target), ".lnk", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTeamsShortcut(string shortcutName, ShortcutInfo shortcut)
        {
            string name = shortcutName ?? "";
            string target = shortcut == null ? "" : shortcut.TargetPath ?? "";
            return name.IndexOf("Teams", StringComparison.OrdinalIgnoreCase) >= 0
                || target.IndexOf("MSTeams", StringComparison.OrdinalIgnoreCase) >= 0
                || target.IndexOf("ms-teams", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static ShortcutInfo ReadShortcut(string shortcutPath)
        {
            object shell = null;
            object shortcut = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null)
                {
                    return null;
                }

                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember(
                    "CreateShortcut",
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                if (shortcut == null)
                {
                    return null;
                }

                Type shortcutType = shortcut.GetType();
                return new ShortcutInfo
                {
                    TargetPath = Convert.ToString(shortcutType.InvokeMember(
                        "TargetPath",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        shortcut,
                        null)),
                    Arguments = Convert.ToString(shortcutType.InvokeMember(
                        "Arguments",
                        System.Reflection.BindingFlags.GetProperty,
                        null,
                        shortcut,
                        null))
                };
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut))
                {
                    Marshal.ReleaseComObject(shortcut);
                }
                if (shell != null && Marshal.IsComObject(shell))
                {
                    Marshal.ReleaseComObject(shell);
                }
            }
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string PowerShellStringLiteral(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private sealed class ShortcutInfo
        {
            public string TargetPath { get; set; }
            public string Arguments { get; set; }
        }
    }

    internal static class ZoneExecutor
    {
        public static void Execute(ZoneRule zone, Action<string> log)
        {
            log("동작 실행 시작: " + zone.Name);
            bool wifiConnectRequested = zone.ConnectWifiEnabled.GetValueOrDefault(false);
            bool wifiConnectSucceeded = !wifiConnectRequested;

            try
            {
                if (wifiConnectRequested)
                {
                    if (string.IsNullOrWhiteSpace(zone.ConnectProfile))
                    {
                        log("Wi-Fi 연결 실패: 연결 프로필이 비어 있습니다.");
                    }
                    else
                    {
                        string ssid = string.IsNullOrWhiteSpace(zone.ConnectSsid) ? zone.ConnectProfile : zone.ConnectSsid;
                        CommandResult connectResult = WifiActions.Connect(zone.ConnectProfile, ssid);
                        wifiConnectSucceeded = connectResult.Succeeded;
                        log((connectResult.Succeeded ? "Wi-Fi 연결 성공: " : "Wi-Fi 연결 실패: ") + ssid + " -> " + connectResult.Summary);
                        if (connectResult.Succeeded)
                        {
                            Thread.Sleep(2000);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                wifiConnectSucceeded = false;
                log("Wi-Fi 연결 실패: " + ex.Message);
            }

            List<string> chromeUrls = zone.ChromeUrls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();
            if (chromeUrls.Count > 0)
            {
                if (wifiConnectRequested && !wifiConnectSucceeded)
                {
                    log("Chrome 탭 실행 건너뜀: Wi-Fi 연결이 성공하지 않았습니다.");
                }
                else
                {
                    try
                    {
                        AppLauncher.OpenChromeUrls(chromeUrls, log);
                    }
                    catch (Exception ex)
                    {
                        log("Chrome 실행 실패: " + ex.Message);
                    }
                }
            }

            try
            {
                if (string.Equals(zone.AudioAction, "Mute", StringComparison.OrdinalIgnoreCase))
                {
                    AudioController.SetMute(true);
                    log("소리 동작 성공: 음소거");
                }
                else if (string.Equals(zone.AudioAction, "Unmute", StringComparison.OrdinalIgnoreCase))
                {
                    AudioController.SetMute(false);
                    log("소리 동작 성공: 음소거 해제");
                }
            }
            catch (Exception ex)
            {
                log("소리 동작 실패: " + ex.Message);
            }

            foreach (string app in zone.AppLaunches.Where(a => !string.IsNullOrWhiteSpace(a)))
            {
                try
                {
                    AppLauncher.LaunchApp(app, log);
                }
                catch (Exception ex)
                {
                    log("앱 실행 실패: " + app + " -> " + ex.Message);
                }
            }

            foreach (string command in zone.Commands.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                try
                {
                    CommandResult result = CommandRunner.Run("cmd.exe", "/c " + command, 20000);
                    log((result.Succeeded ? "명령어 성공: " : "명령어 실패: ") + command + " -> " + result.Summary);
                }
                catch (Exception ex)
                {
                    log("명령어 실패: " + command + " -> " + ex.Message);
                }
            }

            log("동작 실행 종료: " + zone.Name);
        }
    }

    internal static class WifiActions
    {
        public static CommandResult Connect(string profileName, string ssid)
        {
            string arguments = "wlan connect name=" + Quote(profileName);
            if (!string.IsNullOrWhiteSpace(ssid))
            {
                arguments += " ssid=" + Quote(ssid);
            }

            return CommandRunner.Run(Path.Combine(Environment.SystemDirectory, "netsh.exe"), arguments, 15000);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }

    internal sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }

        public bool Succeeded
        {
            get { return !TimedOut && ExitCode == 0; }
        }

        public string Summary
        {
            get
            {
                if (TimedOut)
                {
                    return "시간 초과";
                }

                string text = FirstMeaningfulLine(Output);
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = FirstMeaningfulLine(Error);
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    text = "종료 코드 " + ExitCode;
                }

                return "종료 코드 " + ExitCode + " / " + text;
            }
        }

        private static string FirstMeaningfulLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            string[] lines = text.Replace("\r", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed.Length > 160 ? trimmed.Substring(0, 160) : trimmed;
                }
            }

            return "";
        }
    }

    internal static class CommandRunner
    {
        public static CommandResult Run(string fileName, string arguments, int timeoutMilliseconds)
        {
            CommandResult result = new CommandResult { ExitCode = -1, Output = "", Error = "" };

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = fileName;
                startInfo.Arguments = arguments;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.CreateNoWindow = true;
                startInfo.StandardOutputEncoding = Encoding.Default;
                startInfo.StandardErrorEncoding = Encoding.Default;

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        result.Error = "프로세스를 시작하지 못했습니다.";
                        return result;
                    }

                    if (!process.WaitForExit(timeoutMilliseconds))
                    {
                        result.TimedOut = true;
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }
                    }

                    result.Output = process.StandardOutput.ReadToEnd();
                    result.Error = process.StandardError.ReadToEnd();
                    if (!result.TimedOut)
                    {
                        result.ExitCode = process.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }
    }

    internal static class StartupManager
    {
        private const string ShortcutName = "WinZoneTrigger.lnk";
        private const string TaskName = "WinZoneTrigger";
        private const string RunValueName = "WinZoneTrigger";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool IsEnabled()
        {
            return IsScheduledTaskEnabled() || IsRunKeyEnabled() || File.Exists(GetShortcutPath());
        }

        public static void SetEnabled(bool enabled, bool startMinimized)
        {
            if (!enabled)
            {
                DeleteScheduledTask();
                DeleteRunKey();
                DeleteStartupShortcut();
                return;
            }

            DeleteStartupShortcut();
            DeleteRunKey();

            try
            {
                CreateScheduledTask(startMinimized);
                return;
            }
            catch
            {
                WriteRunKey(startMinimized);
            }
        }

        private static bool IsScheduledTaskEnabled()
        {
            CommandResult result = RunSchtasks("/Query /TN " + Quote(TaskName));
            return result.Succeeded;
        }

        private static void CreateScheduledTask(bool startMinimized)
        {
            string taskRun = BuildStartupCommand(startMinimized);
            string schtasksArguments =
                "/Create /F /SC ONLOGON /TN " + Quote(TaskName) +
                " /TR " + Quote(taskRun) +
                " /RL LIMITED";

            CommandResult result = RunSchtasks(schtasksArguments);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("작업 스케줄러 등록 실패: " + result.Summary);
            }
        }

        private static void DeleteScheduledTask()
        {
            RunSchtasks("/Delete /F /TN " + Quote(TaskName));
        }

        private static CommandResult RunSchtasks(string arguments)
        {
            return CommandRunner.Run(Path.Combine(Environment.SystemDirectory, "schtasks.exe"), arguments, 15000);
        }

        private static void DeleteStartupShortcut()
        {
            string path = GetShortcutPath();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static bool IsRunKeyEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                object value = key == null ? null : key.GetValue(RunValueName);
                return value != null && Convert.ToString(value).IndexOf(Application.ExecutablePath, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static void WriteRunKey(bool startMinimized)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("시작 실행 레지스트리를 열 수 없습니다.");
                }

                key.SetValue(RunValueName, BuildStartupCommand(startMinimized), RegistryValueKind.String);
            }
        }

        private static void DeleteRunKey()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        private static string BuildStartupCommand(bool startMinimized)
        {
            return Quote(Application.ExecutablePath) + (startMinimized ? " --startup --minimized" : " --startup");
        }

        private static string GetShortcutPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }
    }

    internal static class AudioController
    {
        public static void SetMute(bool mute)
        {
            object enumeratorObject = null;
            IMMDevice device = null;
            object volumeObject = null;

            try
            {
                enumeratorObject = new MMDeviceEnumerator();
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)enumeratorObject;
                int hr = enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out device);
                Marshal.ThrowExceptionForHR(hr);

                Guid endpointVolumeGuid = typeof(IAudioEndpointVolume).GUID;
                hr = device.Activate(ref endpointVolumeGuid, 23, IntPtr.Zero, out volumeObject);
                Marshal.ThrowExceptionForHR(hr);

                IAudioEndpointVolume volume = (IAudioEndpointVolume)volumeObject;
                volume.SetMute(mute, Guid.Empty);
            }
            finally
            {
                if (volumeObject != null)
                {
                    Marshal.ReleaseComObject(volumeObject);
                }

                if (device != null)
                {
                    Marshal.ReleaseComObject(device);
                }

                if (enumeratorObject != null)
                {
                    Marshal.ReleaseComObject(enumeratorObject);
                }
            }
        }

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private sealed class MMDeviceEnumerator
        {
        }

        private enum EDataFlow
        {
            eRender = 0,
            eCapture = 1,
            eAll = 2
        }

        private enum ERole
        {
            eConsole = 0,
            eMultimedia = 1,
            eCommunications = 2
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            int RegisterEndpointNotificationCallback(IntPtr pClient);
            int UnregisterEndpointNotificationCallback(IntPtr pClient);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            int GetState(out int pdwState);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
            int GetMute(out bool pbMute);
            int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            int VolumeStepUp(Guid pguidEventContext);
            int VolumeStepDown(Guid pguidEventContext);
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("wlanapi.dll")]
        public static extern int WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll")]
        public static extern int WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        public static extern int WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll")]
        public static extern int WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);

        [DllImport("wlanapi.dll")]
        public static extern int WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

        [DllImport("wlanapi.dll")]
        public static extern void WlanFreeMemory(IntPtr pMemory);

        public static string SsidToString(DOT11_SSID ssid)
        {
            if (ssid.ucSSID == null || ssid.uSSIDLength == 0)
            {
                return "";
            }

            int length = Convert.ToInt32(Math.Min(ssid.uSSIDLength, 32));
            byte[] bytes = new byte[length];
            Array.Copy(ssid.ucSSID, bytes, length);
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }

        public enum WLAN_INTERFACE_STATE
        {
            wlan_interface_state_not_ready = 0,
            wlan_interface_state_connected = 1,
            wlan_interface_state_ad_hoc_network_formed = 2,
            wlan_interface_state_disconnecting = 3,
            wlan_interface_state_disconnected = 4,
            wlan_interface_state_associating = 5,
            wlan_interface_state_discovering = 6,
            wlan_interface_state_authenticating = 7
        }

        public enum DOT11_BSS_TYPE
        {
            dot11_BSS_type_infrastructure = 1,
            dot11_BSS_type_independent = 2,
            dot11_BSS_type_any = 3
        }

        public enum DOT11_PHY_TYPE
        {
            dot11_phy_type_unknown = 0,
            dot11_phy_type_any = 0,
            dot11_phy_type_fhss = 1,
            dot11_phy_type_dsss = 2,
            dot11_phy_type_irbaseband = 3,
            dot11_phy_type_ofdm = 4,
            dot11_phy_type_hrdsss = 5,
            dot11_phy_type_erp = 6,
            dot11_phy_type_ht = 7,
            dot11_phy_type_vht = 8,
            dot11_phy_type_IHV_start = unchecked((int)0x80000000),
            dot11_phy_type_IHV_end = unchecked((int)0xffffffff)
        }

        public enum DOT11_AUTH_ALGORITHM
        {
            DOT11_AUTH_ALGO_80211_OPEN = 1,
            DOT11_AUTH_ALGO_80211_SHARED_KEY = 2,
            DOT11_AUTH_ALGO_WPA = 3,
            DOT11_AUTH_ALGO_WPA_PSK = 4,
            DOT11_AUTH_ALGO_WPA_NONE = 5,
            DOT11_AUTH_ALGO_RSNA = 6,
            DOT11_AUTH_ALGO_RSNA_PSK = 7,
            DOT11_AUTH_ALGO_WPA3 = 8
        }

        public enum DOT11_CIPHER_ALGORITHM
        {
            DOT11_CIPHER_ALGO_NONE = 0x00,
            DOT11_CIPHER_ALGO_WEP40 = 0x01,
            DOT11_CIPHER_ALGO_TKIP = 0x02,
            DOT11_CIPHER_ALGO_CCMP = 0x04,
            DOT11_CIPHER_ALGO_WEP104 = 0x05,
            DOT11_CIPHER_ALGO_BIP = 0x06,
            DOT11_CIPHER_ALGO_GCMP = 0x08,
            DOT11_CIPHER_ALGO_WPA_USE_GROUP = 0x100,
            DOT11_CIPHER_ALGO_RSN_USE_GROUP = 0x100,
            DOT11_CIPHER_ALGO_WEP = 0x101
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strInterfaceDescription;

            public WLAN_INTERFACE_STATE isState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DOT11_SSID
        {
            public uint uSSIDLength;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WLAN_AVAILABLE_NETWORK
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;

            public DOT11_SSID dot11Ssid;
            public DOT11_BSS_TYPE dot11BssType;
            public uint uNumberOfBssids;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bNetworkConnectable;

            public uint wlanNotConnectableReason;
            public uint uNumberOfPhyTypes;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public DOT11_PHY_TYPE[] dot11PhyTypes;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bMorePhyTypes;

            public uint wlanSignalQuality;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bSecurityEnabled;

            public DOT11_AUTH_ALGORITHM dot11DefaultAuthAlgorithm;
            public DOT11_CIPHER_ALGORITHM dot11DefaultCipherAlgorithm;
            public uint dwFlags;
            public uint dwReserved;
        }
    }
}
