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

        private static byte[] CaptureExpRegion(IntPtr hwnd)
        {
            if (IsIconic(hwnd)) return null;
            if (!GetClientRect(hwnd, out RECT rect)) return null;
            
            int w = rect.Right - rect.Left;
            int h = rect.Bottom - rect.Top;
            if (w <= 0 || h <= 0) return null;
            
            int x1 = (int)(0.533 * w);
            int x2 = (int)(0.665 * w);
            int y1 = (int)(0.931 * h);
            int y2 = (int)(0.994 * h);
            
            int regionW = x2 - x1;
            int regionH = y2 - y1;
            if (regionW <= 0 || regionH <= 0) return null;
            
            IntPtr clientDC = GetDC(hwnd);
            if (clientDC == IntPtr.Zero) return null;
            
            IntPtr memDC = CreateCompatibleDC(clientDC);
            IntPtr hbitmap = CreateCompatibleBitmap(clientDC, regionW, regionH);
            IntPtr oldBmp = SelectObject(memDC, hbitmap);
            
            bool success = BitBlt(memDC, 0, 0, regionW, regionH, clientDC, x1, y1, 0x00CC0020);
            
            byte[] buffer = null;
            if (success)
            {
                BITMAPINFOHEADER bih = new BITMAPINFOHEADER();
                bih.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bih.biWidth = regionW;
                bih.biHeight = -regionH;
                bih.biPlanes = 1;
                bih.biBitCount = 24;
                bih.biCompression = 0;
                bih.biSizeImage = (uint)(regionW * regionH * 3);
                
                buffer = new byte[bih.biSizeImage];
                GetDIBits(memDC, hbitmap, 0, (uint)regionH, buffer, ref bih, 0);
            }
            
            SelectObject(memDC, oldBmp);
            DeleteObject(hbitmap);
            DeleteDC(memDC);
            ReleaseDC(hwnd, clientDC);
            
            return buffer;
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
        private CancellationTokenSource _expCts = null;
        
        // Thread-safe cached state (避免跨线程访问UI控件)
        private volatile bool _isBgMode = false;
        private IntPtr _cachedBgHwnd = IntPtr.Zero;
        private readonly Random _globalRand = new Random();
        
        // 全局临时暂停（经验监测超时时使用）
        private volatile bool _isGloballyPaused = false;
        // 巡逻移动中暂停其他技能（巡逻移动中为true，且开启了chkPatrolPauseOthers）
        private volatile bool _isPatrolMoving = false;
        private volatile bool _shouldPauseOthersDuringPatrol = false;
        
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
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            if (_hwnd != IntPtr.Zero)
            {
                UnregisterHotKey(_hwnd, HOTKEY_START_ID);
                UnregisterHotKey(_hwnd, HOTKEY_STOP_ID);
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
                Dispatcher.BeginInvoke(action);
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
            
            txtGlobalDelay.Text = _config.global_delay;
            switchBgMode.IsChecked = _config.bg_enabled;
            comboBgWindow.Text = _config.bg_title;
            
            switchExpMonitor.IsChecked = _config.exp_enabled;
            switchExpCloseGame.IsChecked = _config.exp_close_game;
            txtExpTimeout.Text = _config.exp_time;
            
            chkPatrolPauseOthers.IsChecked = _config.patrol_pause_others;
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
                    _patrolGroups.Add(g);
                }
            }
            
            if (_cards.Count == 0)
            {
                _cards.Add(new BuffCardViewModel { Key = "f5", IntervalText = "175", FluctuationText = "10" });
            }
            if (_patrolGroups.Count == 0)
            {
                _patrolGroups.Add(new PatrolGroupViewModel { RightTimeText = "2.0", LeftTimeText = "2.0", MidPauseTimeText = "0.0", IntervalAfterText = "5.0" });
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
            
            _config.patrol_pause_others = chkPatrolPauseOthers.IsChecked == true;
            _config.patrol_fluct = txtPatrolFluct.Text.Trim();
            
            _config.cards = _cards.ToList();
            _config.patrol_groups = _patrolGroups.ToList();
            
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
            _shouldPauseOthersDuringPatrol = chkPatrolPauseOthers.IsChecked == true;
            
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
            
            // Start EXP Monitor
            if (switchExpMonitor.IsChecked == true)
            {
                double expTimeout = 15;
                double.TryParse(txtExpTimeout.Text, out expTimeout);
                StartExpMonitorLogic(expTimeout);
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
            
            // Cancel EXP Monitor
            if (_expCts != null)
            {
                _expCts.Cancel();
                _expCts.Dispose();
                _expCts = null;
            }
            
            // 重置缓存的后台状态与暂停状态
            _isBgMode = false;
            _cachedBgHwnd = IntPtr.Zero;
            _isGloballyPaused = false;
            _isPatrolMoving = false;
            
            txtPatrolStatus.Text = "已停止";
            txtExpStatus.Text = switchExpMonitor.IsChecked == true ? "监测关闭" : "监测关闭";
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

        private async Task PatrolDelayAsync(string? heldKey, double durationSec, CancellationToken token)
        {
            double elapsed = 0;
            double step = 0.05; // 50ms resolution

            while (elapsed < durationSec)
            {
                token.ThrowIfCancellationRequested();

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

            Task.Run(async () =>
            {
                try
                {
                    Random rand = new Random();
                    while (!token.IsCancellationRequested)
                    {
                        if (_isGloballyPaused)
                        {
                            UpdateUi(() => txtPatrolStatus.Text = "暂停中 (全局暂停)");
                            await Task.Delay(500, token);
                            continue;
                        }

                        var activeGroups = _patrolGroups.Where(g => g.IsActive).ToList();
                        if (activeGroups.Count == 0)
                        {
                            UpdateUi(() => txtPatrolStatus.Text = "无启用组，等待中...");
                            await Task.Delay(1000, token);
                            continue;
                        }

                        foreach (var g in activeGroups)
                        {
                            token.ThrowIfCancellationRequested();
                            
                            while (_isGloballyPaused && !token.IsCancellationRequested)
                            {
                                await Task.Delay(200, token);
                            }
                            token.ThrowIfCancellationRequested();

                            double rightTime = 0;
                            double.TryParse(g.RightTimeText, out rightTime);
                            double leftTime = 0;
                            double.TryParse(g.LeftTimeText, out leftTime);
                            double midPause = 0;
                            double.TryParse(g.MidPauseTimeText, out midPause);
                            double interval = 0;
                            double.TryParse(g.IntervalAfterText, out interval);

                            double fluctPercent = 0;
                            string fluctText = "";
                            UpdateUi(() => fluctText = txtPatrolFluct.Text);
                            double.TryParse(fluctText, out fluctPercent);

                            // Apply fluctuations
                            Func<double, double> applyFluct = (val) =>
                            {
                                if (val <= 0 || fluctPercent <= 0) return val;
                                double maxDeviation = val * (fluctPercent / 100.0);
                                double dev = (rand.NextDouble() * 2 - 1) * maxDeviation;
                                double finalVal = val + dev;
                                return finalVal < 0.01 ? 0.01 : finalVal;
                            };

                            double actualRightTime = applyFluct(rightTime);
                            double actualLeftTime = applyFluct(leftTime);
                            double actualMidPause = applyFluct(midPause);
                            double actualInterval = applyFluct(interval);

                            // 1. Move Right
                            if (actualRightTime > 0)
                            {
                                UpdateUi(() => txtPatrolStatus.Text = "巡逻中");
                                g.Status = $"向右 {actualRightTime:F1}秒";
                                _isPatrolMoving = true;
                                PressKeySafe("right");
                                await PatrolDelayAsync("right", actualRightTime, token);
                                ReleaseKeySafe("right");
                                _isPatrolMoving = false;
                            }

                            // 2. Mid Pause
                            if (actualMidPause > 0)
                            {
                                g.Status = $"暂停 {actualMidPause:F1}秒";
                                await PatrolDelayAsync(null, actualMidPause, token);
                            }

                            // 3. Move Left
                            if (actualLeftTime > 0)
                            {
                                UpdateUi(() => txtPatrolStatus.Text = "巡逻中");
                                g.Status = $"向左 {actualLeftTime:F1}秒";
                                _isPatrolMoving = true;
                                PressKeySafe("left");
                                await PatrolDelayAsync("left", actualLeftTime, token);
                                ReleaseKeySafe("left");
                                _isPatrolMoving = false;
                            }

                            // 4. Group Interval
                            if (actualInterval > 0)
                            {
                                g.Status = $"组后停留 {actualInterval:F1}秒";
                                await PatrolDelayAsync(null, actualInterval, token);
                            }

                            g.Status = "等待下次循环";
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
                    ReleaseKeySafe("right");
                    ReleaseKeySafe("left");
                    foreach (var g in _patrolGroups) g.Status = "等待运行";
                }
            }, token);
        }

        private void StartExpMonitorLogic(double expTimeout)
        {
            if (_expCts != null)
            {
                _expCts.Cancel();
                _expCts.Dispose();
            }
            _expCts = new CancellationTokenSource();
            var token = _expCts.Token;

            txtExpStatus.Text = "启动中...";

            Task.Run(async () =>
            {
                byte[]? prevBuffer = null;
                int staticSec = 0;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, token);

                        IntPtr hwnd = GetTargetHwnd();
                        if (hwnd == IntPtr.Zero)
                        {
                            UpdateUi(() => txtExpStatus.Text = "未找到目标窗口");
                            staticSec = 0;
                            continue;
                        }

                        if (IsIconic(hwnd))
                        {
                            UpdateUi(() => txtExpStatus.Text = "窗口最小化 (暂停计数)");
                            staticSec = 0;
                            continue;
                        }

                        byte[]? currentBuffer = CaptureExpRegion(hwnd);
                        if (currentBuffer == null)
                        {
                            UpdateUi(() => txtExpStatus.Text = "捕获失败 (暂停计数)");
                            staticSec = 0;
                            continue;
                        }

                        bool isAllBlack = true;
                        for (int i = 0; i < currentBuffer.Length; i++)
                        {
                            if (currentBuffer[i] != 0)
                            {
                                isAllBlack = false;
                                break;
                            }
                        }

                        if (isAllBlack)
                        {
                            UpdateUi(() => txtExpStatus.Text = "黑屏加载中 (暂停计数)");
                            staticSec = 0;
                            continue;
                        }

                        if (prevBuffer != null)
                        {
                            bool equal = AreBuffersEqual(prevBuffer, currentBuffer);
                            if (equal)
                            {
                                staticSec++;
                                UpdateUi(() => txtExpStatus.Text = $"停滞无变化 ({staticSec}秒 / {expTimeout}秒)");

                                if (staticSec >= expTimeout)
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
                                        // 释放当前可能按住的移动键和独占键
                                        ReleaseKeySafe("right");
                                        ReleaseKeySafe("left");
                                        if (_exclusiveCard != null)
                                        {
                                            ReleaseKeySafe(_exclusiveCard.Key);
                                        }
                                        UpdateUi(() => txtExpStatus.Text = "🚨 经验停滞超时，已暂停所有按键！");
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                staticSec = 0;
                                if (_isGloballyPaused)
                                {
                                    _isGloballyPaused = false;
                                }
                                UpdateUi(() => txtExpStatus.Text = "正常运行 (经验变动中)");
                            }
                        }
                        else
                        {
                            UpdateUi(() => txtExpStatus.Text = "已连接 (开始监测)");
                        }

                        prevBuffer = currentBuffer;
                    }
                }
                catch (OperationCanceledException)
                {
                    UpdateUi(() => txtExpStatus.Text = "监测已关闭");
                }
                catch (Exception ex)
                {
                    UpdateUi(() => txtExpStatus.Text = $"监测出错: {ex.Message}");
                }
            }, token);
        }

        private static bool AreBuffersEqual(byte[]? b1, byte[]? b2)
        {
            if (b1 == null || b2 == null) return false;
            if (b1.Length != b2.Length) return false;
            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] != b2[i]) return false;
            }
            return true;
        }

        private void SwitchTab(object? selectedItem)
        {
            if (KeysPanel == null || PatrolPanel == null || PresetsPanel == null || SettingsPanel == null) return;
            
            KeysPanel.Visibility = Visibility.Collapsed;
            PatrolPanel.Visibility = Visibility.Collapsed;
            PresetsPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            
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
            _patrolGroups.Add(new PatrolGroupViewModel { RightTimeText = "2.0", LeftTimeText = "2.0", MidPauseTimeText = "0.0", IntervalAfterText = "5.0" });
        }

        private void BtnDeletePatrolGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is PatrolGroupViewModel group)
            {
                _patrolGroups.Remove(group);
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
                patrol_pause_others = chkPatrolPauseOthers.IsChecked == true,
                patrol_fluct = txtPatrolFluct.Text.Trim(),
                cards = _cards.ToList(),
                patrol_groups = _patrolGroups.ToList()
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
                
                chkPatrolPauseOthers.IsChecked = preset.patrol_pause_others;
                txtPatrolFluct.Text = preset.patrol_fluct;
                
                _cards.Clear();
                foreach (var c in preset.cards) _cards.Add(c);
                
                _patrolGroups.Clear();
                foreach (var g in preset.patrol_groups) _patrolGroups.Add(g);
                
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
                    
                    chkPatrolPauseOthers.IsChecked = preset.patrol_pause_others;
                    txtPatrolFluct.Text = preset.patrol_fluct;
                    
                    _cards.Clear();
                    foreach (var c in preset.cards) _cards.Add(c);
                    
                    _patrolGroups.Clear();
                    if (preset.patrol_groups != null)
                    {
                        foreach (var g in preset.patrol_groups) _patrolGroups.Add(g);
                    }
                    
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
    }
}