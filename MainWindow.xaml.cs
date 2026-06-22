using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Input;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ArtaleProBuff
{
    public partial class MainWindow : FluentWindow
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_ACTIVATE = 0x0006;
        private const int WA_ACTIVE = 1;
        private const int WM_HOTKEY = 0x0312;
        
        private const int HOTKEY_START_ID = 9001;
        private const int HOTKEY_STOP_ID = 9002;
        private const int HOTKEY_RESET_ID = 9003;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        [DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);
        
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBmp, uint nFlags);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int x, int y, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);
        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFOHEADER lpbmi, uint uUsage);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private static readonly Dictionary<string, byte> VK_CODES = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            { "f1", 0x70 }, { "f2", 0x71 }, { "f3", 0x72 }, { "f4", 0x73 }, { "f5", 0x74 }, { "f6", 0x75 }, { "f7", 0x76 }, { "f8", 0x77 }, { "f9", 0x78 }, { "f10", 0x79 }, { "f11", 0x7A }, { "f12", 0x7B },
            { "0", 0x30 }, { "1", 0x31 }, { "2", 0x32 }, { "3", 0x33 }, { "4", 0x34 }, { "5", 0x35 }, { "6", 0x36 }, { "7", 0x37 }, { "8", 0x38 }, { "9", 0x39 },
            { "a", 0x41 }, { "b", 0x42 }, { "c", 0x43 }, { "d", 0x44 }, { "e", 0x45 }, { "f", 0x46 }, { "g", 0x47 }, { "h", 0x48 }, { "i", 0x49 }, { "j", 0x4A }, { "k", 0x4B }, { "l", 0x4C }, { "m", 0x4D }, { "n", 0x4E }, { "o", 0x4F }, { "p", 0x50 }, { "q", 0x51 }, { "r", 0x52 }, { "s", 0x53 }, { "t", 0x54 }, { "u", 0x55 }, { "v", 0x56 }, { "w", 0x57 }, { "x", 0x58 }, { "y", 0x59 }, { "z", 0x5A },
            { "space", 0x20 }, { "enter", 0x0D }, { "shift", 0x10 }, { "ctrl", 0x11 }, { "alt", 0x12 },
            { "left", 0x25 }, { "up", 0x26 }, { "right", 0x27 }, { "down", 0x28 }
        };

        private static byte GetVkCode(string keyStr)
        {
            if (string.IsNullOrEmpty(keyStr)) return 0;
            string k = keyStr.Trim().ToLower();
            if (VK_CODES.TryGetValue(k, out byte vk)) return vk;
            if (k.Length == 1) return (byte)char.ToUpper(k[0]);
            return 0;
        }

        private static bool IsExtendedKey(byte vk)
        {
            return vk >= 0x25 && vk <= 0x28; // arrows
        }

        private static void PostMessageToAll(IntPtr parentHwnd, uint msg, IntPtr wparam, IntPtr lparam)
        {
            if (parentHwnd == IntPtr.Zero) return;
            PostMessage(parentHwnd, WM_ACTIVATE, (IntPtr)WA_ACTIVE, IntPtr.Zero);
            PostMessage(parentHwnd, msg, wparam, lparam);
            
            EnumChildWindows(parentHwnd, (hwnd, lp) =>
            {
                PostMessage(hwnd, WM_ACTIVATE, (IntPtr)WA_ACTIVE, IntPtr.Zero);
                PostMessage(hwnd, msg, wparam, lparam);
                return true;
            }, IntPtr.Zero);
        }

        private void ReleaseKeySafe(string key)
        {
            byte vk = GetVkCode(key);
            if (vk == 0) return;
            
            // Foreground UP via SendInput (游戏兼容性更好)
            uint scan = MapVirtualKey(vk, 0);
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)scan,
                        dwFlags = KEYEVENTF_KEYUP | KEYEVENTF_SCANCODE | (IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            
            // Background UP (使用缓存的状态，避免跨线程访问UI)
            if (_isBgMode)
            {
                IntPtr hwnd = _cachedBgHwnd;
                if (hwnd != IntPtr.Zero)
                {
                    uint scanCode = MapVirtualKey(vk, 0);
                    uint isExtended = IsExtendedKey(vk) ? 1u : 0u;
                    IntPtr lparamUp = (IntPtr)(1 | (scanCode << 16) | (isExtended << 24) | (1u << 30) | (1u << 31));
                    PostMessageToAll(hwnd, WM_KEYUP, (IntPtr)vk, lparamUp);
                }
            }
        }

        private static List<string> GetVisibleWindows()
        {
            var list = new List<string>();
            EnumWindows((hwnd, lp) =>
            {
                if (IsWindowVisible(hwnd))
                {
                    int length = GetWindowTextLength(hwnd);
                    if (length > 0)
                    {
                        var sb = new StringBuilder(length + 1);
                        GetWindowText(hwnd, sb, sb.Capacity);
                        string title = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(title))
                        {
                            list.Add(title);
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
            
            var ignore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Program Manager", "Settings", "Microsoft Text Input Application", "NVIDIA GeForce Overlay"
            };
            
            return list.Where(t => !ignore.Contains(t))
                       .Distinct()
                       .OrderBy(t => t)
                       .ToList();
        }

        private IntPtr GetTargetHwnd()
        {
            string title = "";
            UpdateUi(() => title = comboBgWindow.Text.Trim());
            
            if (string.IsNullOrEmpty(title)) return IntPtr.Zero;
            
            IntPtr hwnd = FindWindow(null, title);
            if (hwnd != IntPtr.Zero) return hwnd;
            
            IntPtr foundHwnd = IntPtr.Zero;
            EnumWindows((h, lp) =>
            {
                if (IsWindowVisible(h))
                {
                    int len = GetWindowTextLength(h);
                    if (len > 0)
                    {
                        var sb = new StringBuilder(len + 1);
                        GetWindowText(h, sb, sb.Capacity);
                        string wTitle = sb.ToString();
                        if (wTitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundHwnd = h;
                            return false;
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);
            
            return foundHwnd;
        }

        private static BitmapSource? CaptureExpRegion(IntPtr hwnd, int cropX, int cropY, int cropW, int cropH)
        {
            if (IsIconic(hwnd)) return null;
            if (!GetClientRect(hwnd, out RECT rect)) return null;
            
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return null;
            
            int x1, y1, regionW, regionH;
            if (cropW > 0 && cropH > 0)
            {
                x1 = cropX;
                y1 = cropY;
                regionW = cropW;
                regionH = cropH;
            }
            else
            {
                // Fallback to default MapleStory Worlds experience region (53.3% to 66.5% width, 93.1% to 99.4% height)
                x1 = (int)(0.533 * w);
                y1 = (int)(0.931 * h);
                regionW = (int)(0.665 * w) - x1;
                regionH = (int)(0.994 * h) - y1;
            }
            
            if (regionW <= 0 || regionH <= 0) return null;
            
            IntPtr clientDC = GetDC(hwnd);
            if (clientDC == IntPtr.Zero) return null;
            
            IntPtr memDC = CreateCompatibleDC(clientDC);
            IntPtr hbitmap = CreateCompatibleBitmap(clientDC, regionW, regionH);
            IntPtr oldBmp = SelectObject(memDC, hbitmap);
            
            bool success = false;
            try
            {
                // Create a temporary full-size DC/Bitmap to capture via PrintWindow
                IntPtr fullMemDC = CreateCompatibleDC(clientDC);
                IntPtr hFullBmp = CreateCompatibleBitmap(clientDC, w, h);
                IntPtr oldFullBmp = SelectObject(fullMemDC, hFullBmp);
                
                // PW_CLIENTONLY = 1, PW_RENDERFULLCONTENT = 2. Combined = 3.
                if (PrintWindow(hwnd, fullMemDC, 3))
                {
                    // Crop the specific region from the captured full client area
                    success = BitBlt(memDC, 0, 0, regionW, regionH, fullMemDC, x1, y1, 0x00CC0020);
                }
                
                SelectObject(fullMemDC, oldFullBmp);
                DeleteObject(hFullBmp);
                DeleteDC(fullMemDC);
            }
            catch
            {
                success = false;
            }
            
            // Fallback to direct BitBlt if PrintWindow fails
            if (!success)
            {
                success = BitBlt(memDC, 0, 0, regionW, regionH, clientDC, x1, y1, 0x00CC0020);
            }
            
            BitmapSource? bmp = null;
            if (success)
            {
                int stride = ((regionW * 3) + 3) & ~3;
                BITMAPINFOHEADER bih = new BITMAPINFOHEADER();
                bih.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bih.biWidth = regionW;
                bih.biHeight = -regionH;
                bih.biPlanes = 1;
                bih.biBitCount = 24;
                bih.biCompression = 0;
                bih.biSizeImage = (uint)(stride * regionH);
                
                byte[] buffer = new byte[bih.biSizeImage];
                GetDIBits(memDC, hbitmap, 0, (uint)regionH, buffer, ref bih, 0);
                
                bmp = BitmapSource.Create(regionW, regionH, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null, buffer, stride);
                bmp.Freeze();
            }
            
            SelectObject(memDC, oldBmp);
            DeleteObject(hbitmap);
            DeleteDC(memDC);
            ReleaseDC(hwnd, clientDC);
            
            return bmp;
        }

        private static void CloseTargetWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            PostMessage(hwnd, 0x0010, IntPtr.Zero, IntPtr.Zero); // WM_CLOSE
            
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0)
            {
                try
                {
                    using (var proc = Process.GetProcessById((int)pid))
                    {
                        proc.Kill();
                    }
                }
                catch { }
            }
        }

        private static double GetTime()
        {
            return (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
        }

        // Project State
        private AppConfig _config = new AppConfig();
        private ObservableCollection<BuffCardViewModel> _cards = new ObservableCollection<BuffCardViewModel>();
        private ObservableCollection<PatrolGroupViewModel> _patrolGroups = new ObservableCollection<PatrolGroupViewModel>();
        
        private bool _isRunningGlobal = false;
        private BuffCardViewModel _exclusiveCard = null;
        private bool _isPatrolRunning = false;
        private CancellationTokenSource _patrolCts = null;
        private CancellationTokenSource? _globalMonitorCts = null;
        
        // Thread-safe cached state (避免跨线程访问UI控件)
        private volatile bool _isBgMode = false;
        private IntPtr _cachedBgHwnd = IntPtr.Zero;
        private readonly Random _globalRand = new Random();
        
        // 全局临时暂停（经验监测超时时使用）
        private volatile bool _isGloballyPaused = false;
        // 巡逻移动中暂停其他技能（巡逻移动中为true，且开启了chkPatrolPauseOthers）
        private volatile bool _isPatrolMoving = false;
        private volatile bool _shouldPauseOthersDuringPatrol = false;
        
        // 经验监测的裁剪区域 (0表示默认位置)
        private int _cropX = 0;
        private int _cropY = 0;
        private int _cropW = 0;
        private int _cropH = 0;
        private double? _lastParsedExp = null;
        private double? _initialExpValue = null;
        private double _accumulatedExpGained = 0;
        private bool _initialExpIsPercent = false;
        private DateTime _lastCalibrationTime = DateTime.MinValue;
        private readonly List<(DateTime Time, double Value)> _expHistory = new List<(DateTime Time, double Value)>();
        private double _totalGrindingSeconds = 0;
        
        private IntPtr _hwnd = IntPtr.Zero;
        private HwndSource _hwndSource = null;

        public bool IsPatrolRunning
        {
            get => _isPatrolRunning;
            set => _isPatrolRunning = value;
        }

        public MainWindow()
        {
            InitializeComponent();
            
            // Set bindings
            listBuffCards.ItemsSource = _cards;
            listPatrolGroups.ItemsSource = _patrolGroups;
            
            // Hook boss hunt map text changed
            comboBossHuntMap.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(ComboBossHuntMap_TextChanged));
            
            // Load configuration
            LoadSettings();
            
            // Setup titlebar
            // (Window property is handled automatically by WPF UI v4)
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            var helper = new WindowInteropHelper(this);
            _hwnd = helper.Handle;
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource.AddHook(HwndHook);
            
            RegisterHotKey(_hwnd, HOTKEY_START_ID, 0, 0x78); // F9
            RegisterHotKey(_hwnd, HOTKEY_STOP_ID, 0, 0x79);  // F10
            RegisterHotKey(_hwnd, HOTKEY_RESET_ID, 0, 0x7A); // F11
            
            StartGlobalMonitorLoop();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            StopGlobalMonitorLoop();
            
            if (_hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_START_ID);
                UnregisterHotKey(_hwnd, HOTKEY_STOP_ID);
                UnregisterHotKey(_hwnd, HOTKEY_RESET_ID);
            }
            StopAll();
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_START_ID)
                {
                    StartAll();
                    handled = true;
                }
                else if (id == HOTKEY_STOP_ID)
                {
                    StopAll();
                    handled = true;
                }
                else if (id == HOTKEY_RESET_ID)
                {
                    ResetBossHuntCount();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void UpdateUi(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        private void MessageBoxShow(string title, string content)
        {
            UpdateUi(() =>
            {
                System.Windows.MessageBox.Show(this, content, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void LoadSettings()
        {
            _config = ConfigHelper.Load();
            
            if (_config.presets != null)
            {
                foreach (var preset in _config.presets.Values)
                {
                    if (preset.cards == null)
                    {
                        preset.cards = new List<BuffCardViewModel>();
                    }
                    if (preset.patrol_groups == null)
                    {
                        preset.patrol_groups = new List<PatrolGroupViewModel>();
                    }
                    foreach (var g in preset.patrol_groups)
                    {
                        g.InitializeStepsFromLegacy();
                    }
                }
            }
            
            txtGlobalDelay.Text = _config.global_delay;
            switchBgMode.IsChecked = _config.bg_enabled;
            comboBgWindow.Text = _config.bg_title;
            
            switchExpMonitor.IsChecked = _config.exp_enabled;
            switchExpCloseGame.IsChecked = _config.exp_close_game;
            txtExpTimeout.Text = _config.exp_time;
            
            _cropX = _config.exp_crop_x;
            _cropY = _config.exp_crop_y;
            _cropW = _config.exp_crop_w;
            _cropH = _config.exp_crop_h;
            
            txtPatrolFluct.Text = _config.patrol_fluct;
            
            _cards.Clear();
            if (_config.cards != null)
            {
                foreach (var c in _config.cards)
                {
                    _cards.Add(c);
                }
            }
            
            _patrolGroups.Clear();
            if (_config.patrol_groups != null)
            {
                foreach (var g in _config.patrol_groups)
                {
                    g.InitializeStepsFromLegacy();
                    _patrolGroups.Add(g);
                }
            }
            
            if (_cards.Count == 0)
            {
                _cards.Add(new BuffCardViewModel { Key = "f5", IntervalText = "175", FluctuationText = "10" });
            }
            if (_patrolGroups.Count == 0)
            {
                var newGroup = new PatrolGroupViewModel { RightTimeText = "2.0", LeftTimeText = "2.0", MidPauseTimeText = "0.0", IntervalAfterText = "5.0" };
                newGroup.InitializeStepsFromLegacy();
                _patrolGroups.Add(newGroup);
            }
            
            // Themes loading
            string initialTheme = _config.theme ?? "dark";
            ApplyThemeSetting(initialTheme);
            
            // Refresh lists
            comboPresets.ItemsSource = _config.presets.Keys.ToList();
            if (_config.presets.Count > 0)
            {
                comboPresets.SelectedIndex = 0;
            }
            
            // Initialize boss hunt map database
            if (_config.boss_hunt_map_exp == null)
            {
                _config.boss_hunt_map_exp = new Dictionary<string, double>();
            }
            RefreshBossHuntMapsCombo("");
            
            ToggleBgFields();
            ToggleExpFields();
        }

        private void SaveSettings(bool showPrompt = true)
        {
            _config.global_delay = txtGlobalDelay.Text.Trim();
            _config.bg_enabled = switchBgMode.IsChecked == true;
            _config.bg_title = comboBgWindow.Text.Trim();
            
            _config.exp_enabled = switchExpMonitor.IsChecked == true;
            _config.exp_close_game = switchExpCloseGame.IsChecked == true;
            _config.exp_time = txtExpTimeout.Text.Trim();
            
            _config.exp_crop_x = _cropX;
            _config.exp_crop_y = _cropY;
            _config.exp_crop_w = _cropW;
            _config.exp_crop_h = _cropH;
            
            _config.patrol_pause_others = false;
            _config.patrol_fluct = txtPatrolFluct.Text.Trim();
            
            _config.cards = CloneCards(_cards.ToList());
            _config.patrol_groups = ClonePatrolGroups(_patrolGroups.ToList());
            
            ConfigHelper.Save(_config);
            
            if (showPrompt)
            {
                System.Windows.MessageBox.Show(this, "配置已成功保存！\n下次启动将自动加载当前设置。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ApplyThemeSetting(string themeName)
        {
            if (themeName == "light")
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Light);
                btnThemeToggle.Content = "☀️ 浅色模式";
                _config.theme = "light";
            }
            else
            {
                ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                btnThemeToggle.Content = "🌙 深色模式";
                _config.theme = "dark";
            }
        }

        private void ToggleBgFields()
        {
            if (switchBgMode.IsChecked == true)
            {
                panelBgWindow.Visibility = Visibility.Visible;
            }
            else
            {
                panelBgWindow.Visibility = Visibility.Collapsed;
            }
        }

        private void ToggleExpFields()
        {
            if (switchExpMonitor.IsChecked == true)
            {
                panelExpFields.Visibility = Visibility.Visible;
                txtExpStatus.Text = "就绪";
            }
            else
            {
                panelExpFields.Visibility = Visibility.Collapsed;
                txtExpStatus.Text = "监测关闭";
            }
        }

        private void StartAll()
        {
            if (_isRunningGlobal) return;
            
            // 缓存后台模式状态（避免后台线程跨线程访问UI控件导致静默异常）
            _isBgMode = switchBgMode.IsChecked == true;
            
            if (_isBgMode)
            {
                IntPtr hwnd = GetTargetHwnd();
                if (hwnd == IntPtr.Zero)
                {
                    System.Windows.MessageBox.Show(this, $"未找到标题为 '{comboBgWindow.Text}' 的窗口！\n请确保窗口已打开，或在后台模式下配置正确的窗口标题。", "未找到指定窗口", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _cachedBgHwnd = hwnd;
            }
            else
            {
                _cachedBgHwnd = IntPtr.Zero;
            }
            
            _isGloballyPaused = false;
            _isPatrolMoving = false;
            _shouldPauseOthersDuringPatrol = false;
            
            _isRunningGlobal = true;
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            
            double globalDelay = 0;
            double.TryParse(txtGlobalDelay.Text, out globalDelay);
            
            // Start regular key cards
            foreach (var card in _cards)
            {
                StartCardLogic(card, globalDelay);
            }
            
            // Start Patrol Groups loop
            if (switchPatrolMode.IsChecked == true)
            {
                StartPatrolLogic();
            }
            
            // Reset experience monitor calibration variables on start
            if (switchExpMonitor.IsChecked == true)
            {
                _lastParsedExp = null;
                _initialExpValue = null;
                _accumulatedExpGained = 0;
                _initialExpIsPercent = false;
                _lastCalibrationTime = DateTime.Now;
                _expHistory.Clear();
            }
        }

        private void StopAll()
        {
            if (!_isRunningGlobal) return;
            _isRunningGlobal = false;
            
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            
            // Cancel all cards
            foreach (var card in _cards)
            {
                if (card.Cts != null)
                {
                    card.Cts.Cancel();
                    card.Cts.Dispose();
                    card.Cts = null;
                }
                ReleaseKeySafe(card.Key);
            }
            _exclusiveCard = null;
            
            // Cancel patrol
            if (_patrolCts != null)
            {
                _patrolCts.Cancel();
                _patrolCts.Dispose();
                _patrolCts = null;
            }
            
            // Release patrol movement keys
            ReleaseKeySafe("right");
            ReleaseKeySafe("left");
            
            // 重置缓存的后台状态与暂停状态
            _isBgMode = false;
            _cachedBgHwnd = IntPtr.Zero;
            _isGloballyPaused = false;
            _isPatrolMoving = false;
            
            txtPatrolStatus.Text = "已停止";
            txtExpStatus.Text = "正常监控中";
            if (txtExpStatusBH != null) txtExpStatusBH.Text = "正常监控中";
            foreach (var g in _patrolGroups) g.Status = "等待运行";
        }

        // Macro Logic Implementations
        private void PressKeySafe(string key)
        {
            byte vk = GetVkCode(key);
            if (vk == 0) return;
            
            // Foreground DOWN via SendInput (游戏兼容性更好)
            uint scan = MapVirtualKey(vk, 0);
            var input = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = (ushort)scan,
                        dwFlags = KEYEVENTF_SCANCODE | (IsExtendedKey(vk) ? KEYEVENTF_EXTENDEDKEY : 0),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
            
            // Background DOWN (使用缓存的状态，避免跨线程访问UI)
            if (_isBgMode)
            {
                IntPtr hwnd = _cachedBgHwnd;
                if (hwnd != IntPtr.Zero)
                {
                    uint scanCode = MapVirtualKey(vk, 0);
                    uint isExtended = IsExtendedKey(vk) ? 1u : 0u;
                    IntPtr lparamDown = (IntPtr)(1 | (scanCode << 16) | (isExtended << 24));
                    PostMessageToAll(hwnd, WM_KEYDOWN, (IntPtr)vk, lparamDown);
                }
            }
        }

        private async Task TriggerKeyAsync(string key, double intervalSec)
        {
            // 动态按键持续时间：短间隔时缩短按压以支持快速连点（与老版本一致）
            int durationMs;
            if (intervalSec < 0.15)
            {
                durationMs = _globalRand.Next(10, 30);   // 10-30ms 快速连点
            }
            else
            {
                durationMs = _globalRand.Next(80, 180);  // 80-180ms 正常按键
            }
            PressKeySafe(key);
            await Task.Delay(durationMs);
            ReleaseKeySafe(key);
        }

        private void StartCardLogic(BuffCardViewModel card, double globalDelay)
        {
            if (card.Cts != null)
            {
                card.Cts.Cancel();
                card.Cts.Dispose();
            }
            card.Cts = new CancellationTokenSource();
            var token = card.Cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    card.Status = "等待延迟...";
                    card.ProgressMax = 100;
                    card.ProgressValue = 0;

                    // Initial delay
                    if (globalDelay > 0)
                    {
                        await Task.Delay((int)(globalDelay * 1000), token);
                    }

                    // Parse interval
                    double intervalSec = 60;
                    double.TryParse(card.IntervalText, out intervalSec);
                    double fluctuationSec = 0;
                    double.TryParse(card.FluctuationText, out fluctuationSec);

                    Random rand = new Random();

                    while (!token.IsCancellationRequested)
                    {
                        if (!card.IsActive)
                        {
                            card.Status = "未启用";
                            card.ProgressValue = 0;
                            await Task.Delay(1000, token);
                            continue;
                        }

                        // Wait if there is an exclusive card active, or globally paused, or patrol moving (if enabled)
                        while ((_exclusiveCard != null && _exclusiveCard != card || _isGloballyPaused || (_isPatrolMoving && _shouldPauseOthersDuringPatrol)) && !token.IsCancellationRequested)
                        {
                            await Task.Delay(100, token);
                        }
                        
                        if (_isGloballyPaused)
                        {
                            await Task.Delay(100, token);
                            continue;
                        }

                        if (card.IsExclusive)
                        {
                            _exclusiveCard = card;
                            ReleaseKeySafe("right");
                            ReleaseKeySafe("left");
                        }

                        card.Status = $"触发按键 {card.Key}";
                        card.ProgressValue = card.ProgressMax;

                        if (card.IsLongPress)
                        {
                            double holdTime = 5;
                            double.TryParse(card.HoldTimeText, out holdTime);
                            PressKeySafe(card.Key);
                            await Task.Delay((int)(holdTime * 1000), token);
                            ReleaseKeySafe(card.Key);
                        }
                        else
                        {
                            await TriggerKeyAsync(card.Key, intervalSec);
                        }

                        if (card.IsExclusive && _exclusiveCard == card)
                        {
                            _exclusiveCard = null;
                        }

                        // Calculate next interval
                        double nextInterval = intervalSec;
                        if (fluctuationSec > 0)
                        {
                            double maxDeviation = intervalSec * (fluctuationSec / 100.0);
                            double dev = (rand.NextDouble() * 2 - 1) * maxDeviation;
                            nextInterval += dev;
                            if (nextInterval < 0.01) nextInterval = 0.01;
                        }

                        card.Status = "等待下次触发...";
                        card.VariationText = $"{nextInterval:F1}秒";
                        card.VariationColor = "#0078D4"; // Fluent Blue

                        // Progress Countdown loop
                        double totalMs = nextInterval * 1000;
                        double elapsed = 0;
                        // 根据间隔自动调整更新步长：短间隔用更短的步长
                        double step = totalMs < 200 ? Math.Max(totalMs, 10) : 100;
                        card.ProgressMax = (int)Math.Max(totalMs, 1);

                        while (elapsed < totalMs)
                        {
                            token.ThrowIfCancellationRequested();
                            int delayMs = (int)Math.Min(step, totalMs - elapsed);
                            if (delayMs <= 0) break;
                            await Task.Delay(delayMs, token);
                            elapsed += delayMs;
                            card.ProgressValue = (int)(totalMs - elapsed);
                            double remaining = (totalMs - elapsed) / 1000.0;
                            card.Status = remaining >= 0.1 ? $"下次触发: {remaining:F1}秒" : "即将触发...";
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    card.Status = "已停止";
                    card.ProgressValue = 0;
                }
                catch (Exception ex)
                {
                    card.Status = $"出错: {ex.Message}";
                    card.ProgressValue = 0;
                }
                finally
                {
                    if (card.IsExclusive && _exclusiveCard == card)
                    {
                        _exclusiveCard = null;
                    }
                }
            }, token);
        }

        private async Task PatrolDelayAsync(string? heldKey, double durationSec, CancellationToken token, bool allowInterrupt)
        {
            double elapsed = 0;
            double step = 0.05; // 50ms resolution

            while (elapsed < durationSec)
            {
                token.ThrowIfCancellationRequested();

                if (allowInterrupt)
                {
                    var due = GetDueTimedGroup();
                    if (due != null)
                    {
                        throw new PatrolInterruptException();
                    }
                }

                if (_exclusiveCard != null)
                {
                    if (heldKey != null)
                    {
                        ReleaseKeySafe(heldKey);
                    }

                    while (_exclusiveCard != null)
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(100, token);
                    }

                    if (heldKey != null)
                    {
                        PressKeySafe(heldKey);
                    }
                }

                await Task.Delay(50, token);
                elapsed += step;
            }
        }

        private class PatrolInterruptException : Exception { }

        private PatrolGroupViewModel? GetDueTimedGroup()
        {
            var now = DateTime.Now;
            foreach (var g in _patrolGroups)
            {
                if (g.IsActive && g.IsTimedLoop)
                {
                    if (now >= g.NextRunTime)
                    {
                        return g;
                    }
                }
            }
            return null;
        }

        private async Task RunDueTimedGroupsAsync(CancellationToken token)
        {
            while (true)
            {
                var dueGroup = GetDueTimedGroup();
                if (dueGroup == null) break;

                int loopCount = 1;
                int.TryParse(dueGroup.LoopCountText, out loopCount);
                if (loopCount <= 0) loopCount = 1;

                for (int i = 0; i < loopCount; i++)
                {
                    token.ThrowIfCancellationRequested();
                    
                    while (_isGloballyPaused && !token.IsCancellationRequested)
                    {
                        await Task.Delay(200, token);
                    }
                    token.ThrowIfCancellationRequested();

                    dueGroup.Status = $"定时执行 ({i + 1}/{loopCount})";
                    await RunSingleGroupSequenceAsync(dueGroup, token, allowInterrupt: false);
                }

                double intervalSec = 30;
                double.TryParse(dueGroup.LoopIntervalText, out intervalSec);
                if (intervalSec <= 0) intervalSec = 30;

                dueGroup.NextRunTime = DateTime.Now.AddSeconds(intervalSec);
                dueGroup.Status = $"等待下次定时";
            }
        }

        private async Task RunSingleGroupSequenceAsync(PatrolGroupViewModel g, CancellationToken token, bool allowInterrupt)
        {
            Random rand = new Random();

            double fluctPercent = 0;
            string fluctText = "";
            UpdateUi(() => fluctText = txtPatrolFluct.Text);
            double.TryParse(fluctText, out fluctPercent);

            Func<double, double> applyFluct = (val) =>
            {
                if (val <= 0 || fluctPercent <= 0) return val;
                double maxDeviation = val * (fluctPercent / 100.0);
                double dev = (rand.NextDouble() * 2 - 1) * maxDeviation;
                double finalVal = val + dev;
                return finalVal < 0.01 ? 0.01 : finalVal;
            };

            var stepsSnapshot = g.Steps.ToList();
            int stepIndex = 1;
            foreach (var step in stepsSnapshot)
            {
                token.ThrowIfCancellationRequested();
                while (_isGloballyPaused && !token.IsCancellationRequested)
                {
                    await Task.Delay(200, token);
                }
                token.ThrowIfCancellationRequested();

                double duration = 0;
                double.TryParse(step.DurationText, out duration);
                double actualDuration = applyFluct(duration);

                double pauseAfter = 0;
                double.TryParse(step.PauseAfterText, out pauseAfter);
                double actualPauseAfter = applyFluct(pauseAfter);

                string dir = step.Direction;
                if (actualDuration > 0)
                {
                    if (dir == "右")
                    {
                        UpdateUi(() => txtPatrolStatus.Text = "巡逻中");
                        g.Status = $"({stepIndex}/{stepsSnapshot.Count}) 向右 {actualDuration:F1}秒";
                        _shouldPauseOthersDuringPatrol = g.PauseOthersDuringMove;
                        _isPatrolMoving = true;
                        PressKeySafe("right");
                        try
                        {
                            await PatrolDelayAsync("right", actualDuration, token, allowInterrupt);
                        }
                        finally
                        {
                            ReleaseKeySafe("right");
                            _isPatrolMoving = false;
                            _shouldPauseOthersDuringPatrol = false;
                        }
                    }
                    else if (dir == "左")
                    {
                        UpdateUi(() => txtPatrolStatus.Text = "巡逻中");
                        g.Status = $"({stepIndex}/{stepsSnapshot.Count}) 向左 {actualDuration:F1}秒";
                        _shouldPauseOthersDuringPatrol = g.PauseOthersDuringMove;
                        _isPatrolMoving = true;
                        PressKeySafe("left");
                        try
                        {
                            await PatrolDelayAsync("left", actualDuration, token, allowInterrupt);
                        }
                        finally
                        {
                            ReleaseKeySafe("left");
                            _isPatrolMoving = false;
                            _shouldPauseOthersDuringPatrol = false;
                        }
                    }
                }

                if (actualPauseAfter > 0)
                {
                    token.ThrowIfCancellationRequested();
                    g.Status = $"({stepIndex}/{stepsSnapshot.Count}) 停留 {actualPauseAfter:F1}秒";
                    await PatrolDelayAsync(null, actualPauseAfter, token, allowInterrupt);
                }

                stepIndex++;
            }

            double interval = 0;
            double.TryParse(g.IntervalAfterText, out interval);
            double actualInterval = applyFluct(interval);
            if (actualInterval > 0)
            {
                g.Status = $"组后停留 {actualInterval:F1}秒";
                await PatrolDelayAsync(null, actualInterval, token, allowInterrupt);
            }

            g.Status = g.IsTimedLoop ? "等待下次定时" : "等待下次循环";
        }

        private void StartPatrolLogic()
        {
            if (_patrolCts != null)
            {
                _patrolCts.Cancel();
                _patrolCts.Dispose();
            }
            _patrolCts = new CancellationTokenSource();
            var token = _patrolCts.Token;

            txtPatrolStatus.Text = "运行中";

            // Initialize next run times for active timed groups
            var now = DateTime.Now;
            foreach (var g in _patrolGroups)
            {
                if (g.IsActive && g.IsTimedLoop)
                {
                    double intervalSec = 30;
                    double.TryParse(g.LoopIntervalText, out intervalSec);
                    if (intervalSec <= 0) intervalSec = 30;
                    g.NextRunTime = now.AddSeconds(intervalSec);
                    g.Status = $"已排程 ({intervalSec}秒)";
                }
                else
                {
                    g.Status = "等待运行";
                }
            }

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (_isGloballyPaused)
                        {
                            UpdateUi(() => txtPatrolStatus.Text = "暂停中 (全局暂停)");
                            await Task.Delay(500, token);
                            continue;
                        }

                        // 1. Check and run any due timed groups
                        await RunDueTimedGroupsAsync(token);

                        // 2. Get active base groups (non-timed loops)
                        var baseGroups = _patrolGroups.Where(g => g.IsActive && !g.IsTimedLoop).ToList();
                        if (baseGroups.Count == 0)
                        {
                            UpdateUi(() => txtPatrolStatus.Text = "仅定时组，等待中...");
                            await Task.Delay(500, token);
                            continue;
                        }

                        // Run base groups sequentially
                        foreach (var bg in baseGroups)
                        {
                            token.ThrowIfCancellationRequested();
                            while (_isGloballyPaused && !token.IsCancellationRequested)
                            {
                                await Task.Delay(200, token);
                            }
                            token.ThrowIfCancellationRequested();

                            try
                            {
                                await RunSingleGroupSequenceAsync(bg, token, allowInterrupt: true);
                            }
                            catch (PatrolInterruptException)
                            {
                                // Interrupted! Clean up (already handled by finally, but ensure keys released)
                                ReleaseKeySafe("right");
                                ReleaseKeySafe("left");
                                _isPatrolMoving = false;

                                // Run due timed groups immediately
                                await RunDueTimedGroupsAsync(token);

                                // Break current base group loop to restart sequence
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    UpdateUi(() => txtPatrolStatus.Text = $"错误: {ex.Message}");
                }
                finally
                {
                    _isPatrolMoving = false;
                    _shouldPauseOthersDuringPatrol = false;
                    ReleaseKeySafe("right");
                    ReleaseKeySafe("left");
                    foreach (var g in _patrolGroups)
                    {
                        g.Status = "已停止";
                    }
                }
            }, token);
        }

        private void StartGlobalMonitorLoop()
        {
            StopGlobalMonitorLoop();
            _globalMonitorCts = new CancellationTokenSource();
            var token = _globalMonitorCts.Token;

            // Initialize baseline variables
            _lastParsedExp = null;
            _initialExpValue = null;
            _accumulatedExpGained = 0;
            _initialExpIsPercent = false;
            _lastCalibrationTime = DateTime.Now;
            _expHistory.Clear();

            Task.Run(async () =>
            {
                int staticSec = 0;
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1500, token);

                        IntPtr hwnd = GetTargetHwnd();
                        if (hwnd == IntPtr.Zero)
                        {
                            UpdateUi(() => {
                                txtExpStatus.Text = "未找到目标窗口";
                                if (txtExpStatusBH != null) txtExpStatusBH.Text = "未找到目标窗口";
                            });
                            
                            // If global macro is running and monitor is enabled, check timeout
                            if (_isRunningGlobal && switchExpMonitor.IsChecked == true)
                            {
                                double expTimeout = 15;
                                UpdateUi(() => double.TryParse(txtExpTimeout.Text, out expTimeout));
                                staticSec += 2;
                                if (staticSec >= expTimeout)
                                {
                                    HandleExpTimeout(hwnd);
                                    break;
                                }
                            }
                            continue;
                        }

                        if (IsIconic(hwnd))
                        {
                            UpdateUi(() => {
                                txtExpStatus.Text = "窗口最小化 (暂停计数)";
                                if (txtExpStatusBH != null) txtExpStatusBH.Text = "窗口最小化 (暂停计数)";
                            });
                            staticSec = 0;
                            continue;
                        }

                        BitmapSource? bmp = CaptureExpRegion(hwnd, _cropX, _cropY, _cropW, _cropH);
                        if (bmp == null)
                        {
                            UpdateUi(() => {
                                txtExpStatus.Text = "捕获画面失败 (暂停计数)";
                                if (txtExpStatusBH != null) txtExpStatusBH.Text = "捕获画面失败 (暂停计数)";
                            });
                            staticSec = 0;
                            continue;
                        }

                        if (_isRunningGlobal && !_isGloballyPaused)
                        {
                            _totalGrindingSeconds += 1.5;
                        }

                        // Perform OCR on the cropped bitmap
                        string ocrText = await OcrBitmapAsync(bmp);
                        double? currentVal = ParseExpValue(ocrText);
                        bool changed = false;

                        // Calculate growth rates & update UI
                        double rateMin = 0;
                        double rate10Min = 0;
                        double totalGained = 0;
                        bool isPercent = ocrText.Contains("%");

                        if (currentVal.HasValue)
                        {
                            var now = DateTime.Now;

                            if (!_initialExpValue.HasValue)
                            {
                                _initialExpValue = currentVal.Value;
                                _initialExpIsPercent = isPercent;
                                _lastCalibrationTime = now;
                            }

                            // Calculate delta relative to baseline
                            double delta = 0;
                            bool isDeltaValid = false;
                            if (_initialExpIsPercent == isPercent)
                            {
                                delta = currentVal.Value - _initialExpValue.Value;
                                if (isPercent)
                                {
                                    if (delta < -80.0) // Level up wrap-around
                                    {
                                        delta = (100.0 + currentVal.Value) - _initialExpValue.Value;
                                    }
                                    if (delta >= -0.5 && delta <= 5.0)
                                    {
                                        isDeltaValid = true;
                                    }
                                }
                                else
                                {
                                    if (delta >= -10000 && delta <= 10000000)
                                    {
                                        isDeltaValid = true;
                                    }
                                }
                            }

                            // Every 30 seconds, calibrate baseline
                            if ((now - _lastCalibrationTime).TotalSeconds >= 30)
                            {
                                if (isDeltaValid)
                                {
                                    _accumulatedExpGained += (delta < 0 ? 0 : delta);
                                }
                                _initialExpValue = currentVal.Value;
                                _initialExpIsPercent = isPercent;
                                _lastCalibrationTime = now;
                                delta = 0;
                                isDeltaValid = true;
                            }

                            double displayDelta = (isDeltaValid && delta > 0) ? delta : 0;
                            totalGained = _accumulatedExpGained + displayDelta;

                            if (isDeltaValid)
                            {
                                _expHistory.Add((now, currentVal.Value));
                            }
                            _expHistory.RemoveAll(x => (now - x.Time).TotalMinutes > 11);

                            // Calculate growth rates
                            if (_expHistory.Count > 1)
                            {
                                var oldest = _expHistory[0];
                                double elapsedMin = (now - oldest.Time).TotalMinutes;
                                if (elapsedMin > 0.05)
                                {
                                    // 1-minute rate
                                    if (elapsedMin < 1.0)
                                    {
                                        double diff = currentVal.Value - oldest.Value;
                                        if (diff < 0) diff = 0;
                                        rateMin = diff / elapsedMin;
                                    }
                                    else
                                    {
                                        var target = now.AddMinutes(-1);
                                        var point = _expHistory.OrderBy(x => Math.Abs((x.Time - target).TotalSeconds)).First();
                                        double diff = currentVal.Value - point.Value;
                                        if (diff < 0) diff = 0;
                                        rateMin = diff;
                                    }

                                    // 10-minute rate
                                    if (elapsedMin < 10.0)
                                    {
                                        double diff = currentVal.Value - oldest.Value;
                                        if (diff < 0) diff = 0;
                                        rate10Min = (diff / elapsedMin) * 10;
                                    }
                                    else
                                    {
                                        var target = now.AddMinutes(-10);
                                        var point = _expHistory.OrderBy(x => Math.Abs((x.Time - target).TotalSeconds)).First();
                                        double diff = currentVal.Value - point.Value;
                                        if (diff < 0) diff = 0;
                                        rate10Min = diff;
                                    }
                                }
                            }

                            if (_lastParsedExp.HasValue)
                            {
                                if (currentVal.Value != _lastParsedExp.Value && isDeltaValid)
                                {
                                    changed = true;
                                    double expDiff = currentVal.Value - _lastParsedExp.Value;
                                    if (isPercent && expDiff < -80.0)
                                    {
                                        expDiff = (100.0 + currentVal.Value) - _lastParsedExp.Value;
                                    }
                                    if (expDiff > 0)
                                    {
                                        ProcessBossHuntOcrChange(expDiff, isPercent);
                                    }
                                    _lastParsedExp = currentVal;
                                }
                            }
                            else
                            {
                                _lastParsedExp = currentVal;
                                changed = true;
                            }
                        }

                        // Update both UI panels using helper function
                        UpdateExpUi(bmp, ocrText, currentVal, rateMin, rate10Min, totalGained, isPercent);

                        // If global macro is running and monitor is enabled, check timeout
                        if (_isRunningGlobal && switchExpMonitor.IsChecked == true)
                        {
                            double expTimeout = 15;
                            UpdateUi(() => double.TryParse(txtExpTimeout.Text, out expTimeout));

                            if (changed)
                            {
                                staticSec = 0;
                                if (_isGloballyPaused)
                                {
                                    _isGloballyPaused = false;
                                }
                                UpdateUi(() => {
                                    txtExpStatus.Text = "正常运行 (经验变动中)";
                                    if (txtExpStatusBH != null) txtExpStatusBH.Text = "正常运行 (经验变动中)";
                                });
                            }
                            else
                            {
                                staticSec += 2; // Approx 1.5s delay
                                UpdateUi(() => {
                                    txtExpStatus.Text = $"停滞无变化 ({staticSec}秒 / {expTimeout}秒)";
                                    if (txtExpStatusBH != null) txtExpStatusBH.Text = $"停滞无变化 ({staticSec}秒 / {expTimeout}秒)";
                                });

                                if (staticSec >= expTimeout)
                                {
                                    HandleExpTimeout(hwnd);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // Reset staticSec if not running or monitor disabled
                            staticSec = 0;
                            UpdateUi(() => {
                                string activeText = "正常监控中";
                                txtExpStatus.Text = activeText;
                                if (txtExpStatusBH != null) txtExpStatusBH.Text = activeText;
                            });
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    UpdateUi(() => {
                        txtExpStatus.Text = $"异常: {ex.Message}";
                        if (txtExpStatusBH != null) txtExpStatusBH.Text = $"异常: {ex.Message}";
                    });
                }
            }, token);
        }

        private void StopGlobalMonitorLoop()
        {
            if (_globalMonitorCts != null)
            {
                _globalMonitorCts.Cancel();
                _globalMonitorCts.Dispose();
                _globalMonitorCts = null;
            }
        }

        private string FormatGrindingTime(double totalSeconds)
        {
            int h = (int)(totalSeconds / 3600);
            int m = (int)((totalSeconds % 3600) / 60);
            int s = (int)(totalSeconds % 60);
            return $"{h}小时 {m}分钟 {s}秒";
        }

        private void UpdateExpUi(BitmapSource? bmp, string ocrText, double? val, double rateMin, double rate10Min, double totalGained, bool isPercentDisplay)
        {
            UpdateUi(() =>
            {
                if (bmp != null)
                {
                    imgExpRegion.Source = bmp;
                    if (imgExpRegionBH != null)
                    {
                        imgExpRegionBH.Source = bmp;
                    }
                }

                string friendlyText;
                if (string.IsNullOrWhiteSpace(ocrText) || ocrText.StartsWith("__ERROR_"))
                {
                    friendlyText = GetFriendlyOcrDisplay(ocrText);
                }
                else
                {
                    friendlyText = val.HasValue 
                        ? $"{ocrText.Trim()} (解析成功: {val.Value})" 
                        : $"{ocrText.Trim()} (解析失败)";
                }

                txtExpOcrText.Text = friendlyText;
                if (txtExpOcrTextBH != null) txtExpOcrTextBH.Text = friendlyText;

                string minStr, tenMinStr, totalStr;
                if (isPercentDisplay)
                {
                    minStr = $"+{rateMin:F4}%";
                    tenMinStr = $"+{rate10Min:F4}%";
                    totalStr = $"+{totalGained:F4}%";
                }
                else
                {
                    minStr = $"+{rateMin:F1}";
                    tenMinStr = $"+{rate10Min:F1}";
                    totalStr = $"+{totalGained:F1}";
                }

                txtExpRateMin.Text = minStr;
                txtExpRate10Min.Text = tenMinStr;
                txtExpTotalGained.Text = totalStr;

                if (txtExpRateMinBH != null) txtExpRateMinBH.Text = minStr;
                if (txtExpRate10MinBH != null) txtExpRate10MinBH.Text = tenMinStr;
                if (txtExpTotalGainedBH != null) txtExpTotalGainedBH.Text = totalStr;

                string timeStr = FormatGrindingTime(_totalGrindingSeconds);
                if (txtExpGrindingTime != null) txtExpGrindingTime.Text = timeStr;
                if (txtExpGrindingTimeBH != null) txtExpGrindingTimeBH.Text = timeStr;
            });
        }

        private void HandleExpTimeout(IntPtr hwnd)
        {
            bool closeGame = false;
            UpdateUi(() => closeGame = switchExpCloseGame.IsChecked == true);

            if (closeGame)
            {
                UpdateUi(() => txtExpStatus.Text = "🚨 超时！已强制关闭游戏");
                UpdateUi(() => StopAll());
                CloseTargetWindow(hwnd);
            }
            else
            {
                _isGloballyPaused = true;
                ReleaseKeySafe("right");
                ReleaseKeySafe("left");
                if (_exclusiveCard != null)
                {
                    ReleaseKeySafe(_exclusiveCard.Key);
                }
                UpdateUi(() => txtExpStatus.Text = "🚨 经验停滞超时，已暂停所有按键！");
            }
        }

        private static string SanitizeOcrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            
            // Standardize spaces and case
            string t = text.Trim().ToLower();
            
            // Commonly misidentified characters in numbers:
            // 'l' or 'i' instead of '1'
            // 'o' instead of '0'
            // Replace 'l' or 'i' with '1' and 'o' with '0' ONLY when they are adjacent to digits or decimal points!
            t = System.Text.RegularExpressions.Regex.Replace(t, @"(?<=\d)[li](?=\d|%)|(?<=\d[.,]\d*)[li]", "1");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"(?<=\d)[o](?=\d|%)|(?<=\d[.,]\d*)[o]", "0");
            
            return t;
        }

        private static double? ParseExpValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Contains("__ERROR_")) return null;
            string sanitized = SanitizeOcrText(text);
            
            // Look for percentage, allowing optional spaces around dot/comma and before %
            var percentMatch = System.Text.RegularExpressions.Regex.Match(sanitized, @"(\d+(?:\s*[\.,]\s*\d+)?)\s*%");
            if (percentMatch.Success)
            {
                string cleanVal = System.Text.RegularExpressions.Regex.Replace(percentMatch.Groups[1].Value, @"\s+", "").Replace(',', '.');
                if (double.TryParse(cleanVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }
            }

            // Try to extract the first continuous sequence of digits (including commas/dots for decimals/thousands)
            var numberMatch = System.Text.RegularExpressions.Regex.Match(sanitized, @"[\d\.,\s]+");
            if (numberMatch.Success)
            {
                string clean = System.Text.RegularExpressions.Regex.Replace(numberMatch.Value, @"[\s,]+", "");
                if (double.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }
            }
            
            return null;
        }

        private static string GetFriendlyOcrDisplay(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText) || ocrText == "未识别到字符")
            {
                return "未识别到字符";
            }
            if (ocrText == "__ERROR_NO_OCR_ENGINE__")
            {
                return "未识别到字符 (未检测到系统 OCR 语言包，请在 Windows 设置中添加语言)";
            }
            if (ocrText.StartsWith("__ERROR_OCR_EXCEPTION__: "))
            {
                return $"未识别到字符 ({ocrText.Substring("__ERROR_OCR_EXCEPTION__: ".Length)})";
            }
            return ocrText;
        }

        private static async Task<string> OcrBitmapAsync(BitmapSource bitmapSource)
        {
            try
            {
                // Scale up the bitmap to satisfy Windows OcrEngine's minimum 40px requirement
                // and to dramatically improve text recognition accuracy on small fonts.
                double scale = 3.0;
                var scaleTransform = new ScaleTransform(scale, scale);
                var scaledBitmap = new TransformedBitmap(bitmapSource, scaleTransform);
                scaledBitmap.Freeze();

                // Convert scaled BitmapSource to PNG byte array
                byte[] pngBytes;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(scaledBitmap));
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    pngBytes = ms.ToArray();
                }

                // Write to Windows Runtime stream
                var randomAccessStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                using (var writer = new Windows.Storage.Streams.DataWriter(randomAccessStream))
                {
                    writer.WriteBytes(pngBytes);
                    await writer.StoreAsync();
                    await writer.FlushAsync();
                    writer.DetachStream(); // Detach underlying stream so disposing writer doesn't dispose the stream
                }
                
                randomAccessStream.Seek(0);
                var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                // Robust fallback for OcrEngine languages
                var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null && Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Count > 0)
                {
                    engine = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages[0]);
                }
                
                if (engine != null)
                {
                    var result = await engine.RecognizeAsync(softwareBitmap);
                    return result.Text;
                }
                else
                {
                    return "__ERROR_NO_OCR_ENGINE__";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR error: {ex.Message}");
                return $"__ERROR_OCR_EXCEPTION__: {ex.Message}";
            }
        }

        private void SwitchTab(object? selectedItem)
        {
            if (KeysPanel == null || PatrolPanel == null || PresetsPanel == null || SettingsPanel == null || BossHuntingPanel == null) return;
            
            KeysPanel.Visibility = Visibility.Collapsed;
            PatrolPanel.Visibility = Visibility.Collapsed;
            PresetsPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            BossHuntingPanel.Visibility = Visibility.Collapsed;
            
            if (selectedItem is NavigationViewItem item)
            {
                string content = item.Content?.ToString() ?? "";
                if (content.Contains("常规按键"))
                {
                    KeysPanel.Visibility = Visibility.Visible;
                }
                else if (content.Contains("多组巡逻"))
                {
                    PatrolPanel.Visibility = Visibility.Visible;
                }
                else if (content.Contains("配置预设"))
                {
                    PresetsPanel.Visibility = Visibility.Visible;
                }
                else if (content.Contains("找王辅助"))
                {
                    BossHuntingPanel.Visibility = Visibility.Visible;
                }
                else if (content.Contains("全局与安全"))
                {
                    SettingsPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void NavigationViewItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is NavigationViewItem item)
            {
                SwitchTab(item);
            }
        }

        private void SwitchBgMode_Changed(object sender, RoutedEventArgs e)
        {
            ToggleBgFields();
        }

        private void SwitchPatrolMode_Changed(object sender, RoutedEventArgs e)
        {
        }

        private void SwitchExpMonitor_Changed(object sender, RoutedEventArgs e)
        {
            ToggleExpFields();
        }

        private void BtnRefreshWindows_Click(object sender, RoutedEventArgs e)
        {
            var list = GetVisibleWindows();
            comboBgWindow.ItemsSource = list;
            if (list.Count > 0)
            {
                comboBgWindow.SelectedIndex = 0;
            }
        }

        private void ComboBgWindow_DropDownOpened(object sender, EventArgs e)
        {
            var list = GetVisibleWindows();
            comboBgWindow.ItemsSource = list;
        }

        private void BtnAddCard_Click(object sender, RoutedEventArgs e)
        {
            _cards.Add(new BuffCardViewModel { Key = "f5", IntervalText = "175", FluctuationText = "10" });
        }

        private void BtnDeleteCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is BuffCardViewModel card)
            {
                if (card.Cts != null)
                {
                    card.Cts.Cancel();
                    card.Cts.Dispose();
                }
                _cards.Remove(card);
            }
        }

        private void BtnAddPatrolGroup_Click(object sender, RoutedEventArgs e)
        {
            var newGroup = new PatrolGroupViewModel { RightTimeText = "2.0", LeftTimeText = "2.0", MidPauseTimeText = "0.0", IntervalAfterText = "5.0" };
            newGroup.InitializeStepsFromLegacy();
            _patrolGroups.Add(newGroup);
        }

        private void BtnDeletePatrolGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PatrolGroupViewModel group)
            {
                _patrolGroups.Remove(group);
            }
        }

        private void BtnAddPatrolStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PatrolGroupViewModel group)
            {
                group.Steps.Add(new PatrolStepViewModel { Direction = "右", DurationText = "2.0" });
            }
        }

        private void BtnDeletePatrolStep_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PatrolStepViewModel step)
            {
                foreach (var group in _patrolGroups)
                {
                    if (group.Steps.Contains(step))
                    {
                        group.Steps.Remove(step);
                        break;
                    }
                }
            }
        }

        private void BtnSaveConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings(true);
        }

        private void BtnThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            string newTheme = _config.theme == "dark" ? "light" : "dark";
            ApplyThemeSetting(newTheme);
            SaveSettings(false);
        }

        private List<BuffCardViewModel> CloneCards(List<BuffCardViewModel> source)
        {
            if (source == null) return new List<BuffCardViewModel>();
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(source);
                return System.Text.Json.JsonSerializer.Deserialize<List<BuffCardViewModel>>(json) ?? new List<BuffCardViewModel>();
            }
            catch
            {
                return new List<BuffCardViewModel>();
            }
        }

        private List<PatrolGroupViewModel> ClonePatrolGroups(List<PatrolGroupViewModel> source)
        {
            if (source == null) return new List<PatrolGroupViewModel>();
            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(source);
                return System.Text.Json.JsonSerializer.Deserialize<List<PatrolGroupViewModel>>(json) ?? new List<PatrolGroupViewModel>();
            }
            catch
            {
                return new List<PatrolGroupViewModel>();
            }
        }

        private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
        {
            string name = txtPresetName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                System.Windows.MessageBox.Show(this, "请输入预设方案的名称！", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var preset = new PresetData
            {
                global_delay = txtGlobalDelay.Text.Trim(),
                bg_enabled = switchBgMode.IsChecked == true,
                bg_title = comboBgWindow.Text.Trim(),
                exp_enabled = switchExpMonitor.IsChecked == true,
                exp_close_game = switchExpCloseGame.IsChecked == true,
                exp_time = txtExpTimeout.Text.Trim(),
                exp_crop_x = _cropX,
                exp_crop_y = _cropY,
                exp_crop_w = _cropW,
                exp_crop_h = _cropH,
                patrol_pause_others = false,
                patrol_fluct = txtPatrolFluct.Text.Trim(),
                cards = CloneCards(_cards.ToList()),
                patrol_groups = ClonePatrolGroups(_patrolGroups.ToList())
            };
            
            _config.presets[name] = preset;
            ConfigHelper.Save(_config);
            
            comboPresets.ItemsSource = _config.presets.Keys.ToList();
            comboPresets.SelectedItem = name;
            
            System.Windows.MessageBox.Show(this, $"预设方案 '{name}' 已成功保存！", "预设已保存", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLoadPreset_Click(object sender, RoutedEventArgs e)
        {
            if (comboPresets.SelectedItem == null)
            {
                System.Windows.MessageBox.Show(this, "请先选择要应用的预设方案！", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string name = comboPresets.SelectedItem.ToString();
            if (_config.presets.TryGetValue(name, out var preset))
            {
                txtGlobalDelay.Text = preset.global_delay;
                switchBgMode.IsChecked = preset.bg_enabled;
                comboBgWindow.Text = preset.bg_title;
                switchExpMonitor.IsChecked = preset.exp_enabled;
                switchExpCloseGame.IsChecked = preset.exp_close_game;
                txtExpTimeout.Text = preset.exp_time;
                
                _cropX = preset.exp_crop_x;
                _cropY = preset.exp_crop_y;
                _cropW = preset.exp_crop_w;
                _cropH = preset.exp_crop_h;
                
                txtPatrolFluct.Text = preset.patrol_fluct;
                
                _cards.Clear();
                var clonedCards = CloneCards(preset.cards);
                foreach (var c in clonedCards) _cards.Add(c);
                
                _patrolGroups.Clear();
                var clonedGroups = ClonePatrolGroups(preset.patrol_groups);
                foreach (var g in clonedGroups)
                {
                    g.InitializeStepsFromLegacy();
                    _patrolGroups.Add(g);
                }
                
                txtPresetName.Text = name; // Sync name textbox on load
                
                ToggleBgFields();
                ToggleExpFields();
                
                System.Windows.MessageBox.Show(this, $"已成功加载并应用预设方案 '{name}'！", "应用成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnExportPreset_Click(object sender, RoutedEventArgs e)
        {
            if (comboPresets.SelectedItem == null)
            {
                System.Windows.MessageBox.Show(this, "请先选择要导出的预设方案！", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string name = comboPresets.SelectedItem.ToString();
            if (_config.presets.TryGetValue(name, out var preset))
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "JSON Files|*.json",
                    FileName = $"{name}.json",
                    Title = "导出预设方案"
                };
                
                if (sfd.ShowDialog() == true)
                {
                    try
                    {
                        var dict = new Dictionary<string, PresetData> { { name, preset } };
                        string json = System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(sfd.FileName, json);
                        
                        System.Windows.MessageBox.Show(this, $"预设方案 '{name}' 已成功导出！", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(this, $"导出失败: {ex.Message}", "导出失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnImportPreset_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON Files|*.json",
                Title = "导入预设方案"
            };
            
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PresetData>>(json);
                    
                    if (dict == null || dict.Count == 0)
                    {
                        System.Windows.MessageBox.Show(this, "预设文件格式不正确！", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    string importName = dict.Keys.First();
                    var preset = dict[importName];
                    
                    if (preset.cards == null)
                    {
                        System.Windows.MessageBox.Show(this, "预设文件缺少 cards 属性！", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    string finalName = importName;
                    int suffix = 1;
                    while (_config.presets.ContainsKey(finalName))
                    {
                        finalName = $"{importName}_{suffix}";
                        suffix++;
                    }
                    
                    _config.presets[finalName] = preset;
                    ConfigHelper.Save(_config);
                    
                    comboPresets.ItemsSource = _config.presets.Keys.ToList();
                    comboPresets.SelectedItem = finalName;
                    
                    // Apply
                    txtGlobalDelay.Text = preset.global_delay;
                    switchBgMode.IsChecked = preset.bg_enabled;
                    comboBgWindow.Text = preset.bg_title;
                    switchExpMonitor.IsChecked = preset.exp_enabled;
                    switchExpCloseGame.IsChecked = preset.exp_close_game;
                    txtExpTimeout.Text = preset.exp_time;
                    
                    _cropX = preset.exp_crop_x;
                    _cropY = preset.exp_crop_y;
                    _cropW = preset.exp_crop_w;
                    _cropH = preset.exp_crop_h;
                    
                    txtPatrolFluct.Text = preset.patrol_fluct;
                    
                    _cards.Clear();
                    var clonedCards = CloneCards(preset.cards);
                    foreach (var c in clonedCards) _cards.Add(c);
                    
                    _patrolGroups.Clear();
                    var clonedGroups = ClonePatrolGroups(preset.patrol_groups);
                    if (clonedGroups != null)
                    {
                        foreach (var g in clonedGroups)
                        {
                            g.InitializeStepsFromLegacy();
                            _patrolGroups.Add(g);
                        }
                    }
                    
                    txtPresetName.Text = finalName; // Sync name textbox on import
                    
                    ToggleBgFields();
                    ToggleExpFields();
                    
                    System.Windows.MessageBox.Show(this, $"成功导入预设方案 '{finalName}'！", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(this, $"解析导入文件失败: {ex.Message}", "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (comboPresets.SelectedItem == null)
            {
                System.Windows.MessageBox.Show(this, "请选择要删除的预设方案！", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string name = comboPresets.SelectedItem.ToString();
            if (System.Windows.MessageBox.Show(this, $"确定要删除预设方案 '{name}' 吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _config.presets.Remove(name);
                ConfigHelper.Save(_config);
                
                comboPresets.ItemsSource = _config.presets.Keys.ToList();
                if (_config.presets.Count > 0)
                {
                    comboPresets.SelectedIndex = 0;
                }
                else
                {
                    comboPresets.Text = "";
                }
                
                System.Windows.MessageBox.Show(this, $"预设方案 '{name}' 已成功删除！", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            StartAll();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            StopAll();
        }

        private void BtnSelectExpRegion_Click(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = GetTargetHwnd();
            if (hwnd == IntPtr.Zero)
            {
                System.Windows.MessageBox.Show(this, "请先在上方‘常规按键’或‘后台挂机’设置中指定正确的游戏目标窗口！", "未找到指定窗口", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var screenshot = CaptureWindowClientArea(hwnd);
            if (screenshot == null)
            {
                System.Windows.MessageBox.Show(this, "截取游戏窗口画面失败，请确保游戏未最小化！", "截图失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var cropWin = new CropWindow(screenshot);
            cropWin.Owner = this;
            cropWin.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (cropWin.ShowDialog() == true)
            {
                var rect = cropWin.SelectedRect;
                if (rect.Width > 5 && rect.Height > 5)
                {
                    _cropX = rect.X;
                    _cropY = rect.Y;
                    _cropW = rect.Width;
                    _cropH = rect.Height;
                    
                    SaveSettings(false);
                    
                    // Show confirmation and crop preview immediately
                    var preview = CaptureExpRegion(hwnd, _cropX, _cropY, _cropW, _cropH);
                    if (preview != null)
                    {
                        imgExpRegion.Source = preview;
                    }
                    
                    System.Windows.MessageBox.Show(this, $"选择成功！\n区域坐标: X={_cropX}, Y={_cropY}, 宽={_cropW}, 高={_cropH}\n设置已自动保存。", "区域设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show(this, "划选区域过小，请重新选择！", "选择范围太小", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnResetExp_Click(object sender, RoutedEventArgs e)
        {
            _initialExpValue = null;
            _accumulatedExpGained = 0;
            _initialExpIsPercent = false;
            _lastCalibrationTime = DateTime.Now;
            _expHistory.Clear();
            _lastParsedExp = null;
            _totalGrindingSeconds = 0;

            UpdateUi(() => {
                string zeroStr = "0.00%";
                string resetStr = "已重置，等待下一次识别...";
                string zeroTime = "0小时 0分钟 0秒";

                txtExpRateMin.Text = zeroStr;
                txtExpRate10Min.Text = zeroStr;
                txtExpTotalGained.Text = zeroStr;
                txtExpOcrText.Text = resetStr;

                if (txtExpRateMinBH != null) txtExpRateMinBH.Text = zeroStr;
                if (txtExpRate10MinBH != null) txtExpRate10MinBH.Text = zeroStr;
                if (txtExpTotalGainedBH != null) txtExpTotalGainedBH.Text = zeroStr;
                if (txtExpOcrTextBH != null) txtExpOcrTextBH.Text = resetStr;

                if (txtExpGrindingTime != null) txtExpGrindingTime.Text = zeroTime;
                if (txtExpGrindingTimeBH != null) txtExpGrindingTimeBH.Text = zeroTime;
            });
        }

        private void BtnSelectExpRegionBH_Click(object sender, RoutedEventArgs e)
        {
            BtnSelectExpRegion_Click(sender, e);
            if (imgExpRegionBH != null)
            {
                IntPtr hwnd = GetTargetHwnd();
                if (hwnd != IntPtr.Zero)
                {
                    var preview = CaptureExpRegion(hwnd, _cropX, _cropY, _cropW, _cropH);
                    if (preview != null)
                    {
                        imgExpRegionBH.Source = preview;
                    }
                }
            }
        }

        private void BtnResetExpBH_Click(object sender, RoutedEventArgs e)
        {
            BtnResetExp_Click(sender, e);
        }

        private static BitmapSource? CaptureWindowClientArea(IntPtr hwnd)
        {
            if (IsIconic(hwnd)) return null;
            if (!GetClientRect(hwnd, out RECT rect)) return null;
            
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return null;
            
            IntPtr clientDC = GetDC(hwnd);
            if (clientDC == IntPtr.Zero) return null;
            
            IntPtr memDC = CreateCompatibleDC(clientDC);
            IntPtr hbitmap = CreateCompatibleBitmap(clientDC, w, h);
            IntPtr oldBmp = SelectObject(memDC, hbitmap);
            
            bool success = false;
            try
            {
                // Try capturing using PrintWindow
                success = PrintWindow(hwnd, memDC, 3);
            }
            catch
            {
                success = false;
            }
            
            // Fallback to direct BitBlt if PrintWindow fails
            if (!success)
            {
                success = BitBlt(memDC, 0, 0, w, h, clientDC, 0, 0, 0x00CC0020);
            }
            
            BitmapSource? bmp = null;
            if (success)
            {
                int stride = ((w * 3) + 3) & ~3;
                BITMAPINFOHEADER bih = new BITMAPINFOHEADER();
                bih.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bih.biWidth = w;
                bih.biHeight = -h;
                bih.biPlanes = 1;
                bih.biBitCount = 24;
                bih.biCompression = 0;
                bih.biSizeImage = (uint)(stride * h);
                
                byte[] buffer = new byte[bih.biSizeImage];
                GetDIBits(memDC, hbitmap, 0, (uint)h, buffer, ref bih, 0);
                
                bmp = BitmapSource.Create(w, h, 96, 96, System.Windows.Media.PixelFormats.Bgr24, null, buffer, stride);
                bmp.Freeze();
            }
            
            SelectObject(memDC, oldBmp);
            DeleteObject(hbitmap);
            DeleteDC(memDC);
            ReleaseDC(hwnd, clientDC);
            
            return bmp;
        }

        // ==========================================
        // 找王辅助 (Boss Hunting Helper) Logic
        // ==========================================
        private int _bossHuntKills = 0;
        private double _bossHuntLastDiff = 0;

        private void ResetBossHuntCount()
        {
            UpdateUi(() =>
            {
                _bossHuntKills = 0;
                txtBossHuntKills.Text = "0 / 10";
                txtBossHuntKills.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
                txtBossHuntStatus.Text = "等待击杀统计...";
                txtBossHuntStatus.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
                
                try
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
                catch { }
            });
        }

        private void BtnResetBossHuntKills_Click(object sender, RoutedEventArgs e)
        {
            ResetBossHuntCount();
        }

        private void BtnLockBossHuntExp_Click(object sender, RoutedEventArgs e)
        {
            if (_bossHuntLastDiff > 0)
            {
                bool isPercent = _initialExpIsPercent || _bossHuntLastDiff < 1.0;
                txtBossHuntMonsterExp.Text = isPercent ? $"{_bossHuntLastDiff:F4}" : $"{_bossHuntLastDiff:F1}";
                
                // Save immediately
                string mapName = comboBossHuntMap.Text.Trim();
                if (!string.IsNullOrEmpty(mapName))
                {
                    _config.boss_hunt_map_exp[mapName] = _bossHuntLastDiff;
                    ConfigHelper.Save(_config);
                    RefreshBossHuntMapsCombo(mapName);
                }
            }
        }

        private void BtnSaveBossHuntMap_Click(object sender, RoutedEventArgs e)
        {
            string mapName = comboBossHuntMap.Text.Trim();
            if (string.IsNullOrEmpty(mapName))
            {
                System.Windows.MessageBox.Show(this, "请输入或选择地图名称！", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (double.TryParse(txtBossHuntMonsterExp.Text, out double exp) && exp > 0)
            {
                _config.boss_hunt_map_exp[mapName] = exp;
                ConfigHelper.Save(_config);
                RefreshBossHuntMapsCombo(mapName);
                System.Windows.MessageBox.Show(this, $"地图 '{mapName}' 单只怪物经验值 {exp} 已保存！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(this, "请输入有效的怪物经验值！", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnDeleteBossHuntMap_Click(object sender, RoutedEventArgs e)
        {
            string mapName = comboBossHuntMap.Text.Trim();
            if (string.IsNullOrEmpty(mapName)) return;
            
            if (_config.boss_hunt_map_exp.ContainsKey(mapName))
            {
                var result = System.Windows.MessageBox.Show(this, $"确定要删除地图 '{mapName}' 的记录吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _config.boss_hunt_map_exp.Remove(mapName);
                    ConfigHelper.Save(_config);
                    RefreshBossHuntMapsCombo("");
                    txtBossHuntMonsterExp.Text = "";
                }
            }
        }

        private void ComboBossHuntMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboBossHuntMap.SelectedItem != null)
            {
                string mapName = comboBossHuntMap.SelectedItem.ToString();
                if (_config.boss_hunt_map_exp.TryGetValue(mapName, out double exp))
                {
                    bool isPercent = _initialExpIsPercent || exp < 1.0;
                    txtBossHuntMonsterExp.Text = isPercent ? $"{exp:F4}" : $"{exp:F1}";
                }
            }
        }

        private void ComboBossHuntMap_TextChanged(object sender, TextChangedEventArgs e)
        {
            string mapName = comboBossHuntMap.Text.Trim();
            if (string.IsNullOrEmpty(mapName)) return;
            
            if (_config.boss_hunt_map_exp.TryGetValue(mapName, out double exp))
            {
                bool isPercent = _initialExpIsPercent || exp < 1.0;
                txtBossHuntMonsterExp.Text = isPercent ? $"{exp:F4}" : $"{exp:F1}";
            }
        }

        private void RefreshBossHuntMapsCombo(string selectedMap)
        {
            if (_config.boss_hunt_map_exp != null)
            {
                comboBossHuntMap.ItemsSource = _config.boss_hunt_map_exp.Keys.ToList();
                if (!string.IsNullOrEmpty(selectedMap))
                {
                    comboBossHuntMap.Text = selectedMap;
                }
            }
        }

        private void ProcessBossHuntOcrChange(double expDiff, bool isPercent)
        {
            UpdateUi(() =>
            {
                _bossHuntLastDiff = expDiff;
                txtBossHuntLastDiff.Text = isPercent ? $"{expDiff:F4}%" : $"{expDiff:F1}";

                string expText = txtBossHuntMonsterExp.Text.Trim();
                double mobExp = 0;
                
                if (string.IsNullOrEmpty(expText) || !double.TryParse(expText, out mobExp) || mobExp <= 0)
                {
                    mobExp = expDiff;
                    txtBossHuntMonsterExp.Text = isPercent ? $"{mobExp:F4}" : $"{mobExp:F1}";
                    
                    string mapName = comboBossHuntMap.Text.Trim();
                    if (!string.IsNullOrEmpty(mapName))
                    {
                        _config.boss_hunt_map_exp[mapName] = mobExp;
                        ConfigHelper.Save(_config);
                        RefreshBossHuntMapsCombo(mapName);
                    }
                }

                if (mobExp > 0)
                {
                    int killsThisTime = (int)Math.Round(expDiff / mobExp);
                    if (killsThisTime < 1) killsThisTime = 1;
                    
                    _bossHuntKills += killsThisTime;
                    txtBossHuntKills.Text = $"{_bossHuntKills} / 10";
                    
                    if (_bossHuntKills >= 10)
                    {
                        txtBossHuntKills.Foreground = System.Windows.Media.Brushes.MediumSeaGreen;
                        txtBossHuntStatus.Text = "已满 10 只小怪！可以换线或观察王痕。";
                        txtBossHuntStatus.Foreground = System.Windows.Media.Brushes.MediumSeaGreen;
                        
                        try
                        {
                            System.Media.SystemSounds.Exclamation.Play();
                        }
                        catch { }
                    }
                    else
                    {
                        txtBossHuntKills.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
                        txtBossHuntStatus.Text = $"已击杀 {_bossHuntKills} 只，还需 {10 - _bossHuntKills} 只。";
                        txtBossHuntStatus.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
                    }
                }
            });
        }
    }

    public class CropWindow : Window
    {
        private Point _startPoint;
        private Rectangle? _selectionRect;
        private Canvas _canvas;
        private bool _isDragging = false;
        
        public Int32Rect SelectedRect { get; private set; }

        public CropWindow(BitmapSource screenshot)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Cursor = System.Windows.Input.Cursors.Cross;
            
            // Show image
            var image = new System.Windows.Controls.Image
            {
                Source = screenshot,
                Stretch = Stretch.None
            };
            
            _canvas = new Canvas
            {
                Background = Brushes.Transparent
            };
            
            var grid = new Grid();
            grid.Children.Add(image);
            
            // Add a dark semi-transparent overlay
            var overlay = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add(overlay);
            grid.Children.Add(_canvas);
            
            Content = grid;
            
            Width = screenshot.PixelWidth;
            Height = screenshot.PixelHeight;
            
            _canvas.MouseLeftButtonDown += (s, e) =>
            {
                _startPoint = e.GetPosition(_canvas);
                _isDragging = true;
                
                if (_selectionRect != null)
                {
                    _canvas.Children.Remove(_selectionRect);
                }
                
                _selectionRect = new Rectangle
                {
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 255, 0, 0))
                };
                
                Canvas.SetLeft(_selectionRect, _startPoint.X);
                Canvas.SetTop(_selectionRect, _startPoint.Y);
                _selectionRect.Width = 0;
                _selectionRect.Height = 0;
                
                _canvas.Children.Add(_selectionRect);
                _canvas.CaptureMouse();
            };
            
            _canvas.MouseMove += (s, e) =>
            {
                if (!_isDragging || _selectionRect == null) return;
                
                var currentPoint = e.GetPosition(_canvas);
                
                double x = Math.Min(_startPoint.X, currentPoint.X);
                double y = Math.Min(_startPoint.Y, currentPoint.Y);
                double w = Math.Abs(_startPoint.X - currentPoint.X);
                double h = Math.Abs(_startPoint.Y - currentPoint.Y);
                
                Canvas.SetLeft(_selectionRect, x);
                Canvas.SetTop(_selectionRect, y);
                _selectionRect.Width = w;
                _selectionRect.Height = h;
            };
            
            _canvas.MouseLeftButtonUp += (s, e) =>
            {
                if (!_isDragging) return;
                _isDragging = false;
                _canvas.ReleaseMouseCapture();
                
                if (_selectionRect != null)
                {
                    double x = Canvas.GetLeft(_selectionRect);
                    double y = Canvas.GetTop(_selectionRect);
                    double w = _selectionRect.Width;
                    double h = _selectionRect.Height;
                    
                    SelectedRect = new Int32Rect((int)x, (int)y, (int)w, (int)h);
                }
                
                DialogResult = true;
                Close();
            };
            
            // Allow escape to cancel selection
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
            };
        }
    }
}