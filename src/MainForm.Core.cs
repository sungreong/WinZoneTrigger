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
        private readonly bool _startMinimizedRequested;
        private readonly bool _startedFromWindowsStartup;
        private readonly bool _automationEnabled;
        private readonly Dictionary<string, bool> _insideZones;
        private readonly Dictionary<string, DateTime> _lastAppWatchChecks;
        private readonly Dictionary<string, string> _lastAppWatchStatusTexts;
        private readonly System.Windows.Forms.Timer _scanTimer;
        private readonly System.Windows.Forms.Timer _startupRetryTimer;
        private readonly System.Windows.Forms.Timer _appWatchTimer;
        private readonly System.Windows.Forms.Timer _logRefreshTimer;
        private AppConfig _config;
        private bool _loadingSelection;
        private bool _isExiting;
        private bool _scanInProgress;
        private bool _appWatchInProgress;
        private bool _startupRetryActive;
        private int _startupRetryAttemptsRemaining;
        private int _startupRetryAttemptsTotal;
        private bool _lastScanHadActiveZone;
        private int _appWatchRunVersion;
        private DateTime? _appWatchTimerStartedAtLocal;
        private string _currentZoneId;

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
        private FlowLayoutPanel _appWatchItemsPanel;
        private string _selectedAppWatchItemId;
        private CheckBox _appWatchEnabledCheck;
        private CheckBox _appWatchRequireWindowCheck;
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

        public MainForm(bool startMinimizedRequested, bool startedFromWindowsStartup, bool automationEnabled)
        {
            _startMinimizedRequested = startMinimizedRequested;
            _startedFromWindowsStartup = startedFromWindowsStartup;
            _automationEnabled = automationEnabled;
            _insideZones = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _lastAppWatchChecks = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            _lastAppWatchStatusTexts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _lastVisibleNetworks = new List<WifiNetwork>();
            _config = ConfigStore.Load();
            _scanTimer = new System.Windows.Forms.Timer();
            _startupRetryTimer = new System.Windows.Forms.Timer();
            _appWatchTimer = new System.Windows.Forms.Timer();
            _logRefreshTimer = new System.Windows.Forms.Timer();
            _startupRetryTimer.Interval = 15000;
            _startupRetryTimer.Tick += StartupRetryTimerTick;
            _appWatchTimer.Tick += AppWatchTimerTick;
            _logRefreshTimer.Interval = 5000;
            _logRefreshTimer.Tick += LogRefreshTimerTick;

            InitializeComponent();
            ConfigureTray();
            BindConfigToControls();
            ResetScanTimer();
            ResetAppWatchTimer();
            _logRefreshTimer.Start();
        }

    }
}
