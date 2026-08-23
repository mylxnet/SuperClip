using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SuperClip.Models;
using SuperClip.Native;
using SuperClip.Services;
using SuperClip.ViewModels;

namespace SuperClip.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly TrayService _tray;
        private readonly ClipboardMonitorService _monitor;
        private IntPtr _hwnd;
        private IntPtr _lastExternalWindow = IntPtr.Zero;
        private bool _internalPaste;
        private string _lastPastedContent = string.Empty;
        private bool _topmost;
        private readonly System.Windows.Threading.DispatcherTimer _pasteGuard;

        // 进程绑定：粘贴目标进程窗口。默认绑定到呼出前正在使用的程序窗口。
        private IntPtr _boundWindow = IntPtr.Zero;
        private string _boundProcessName = "未绑定";
        private bool _boundOnce;

        // 点选模式：全局鼠标钩子 + 十字光标，直到点选目标窗口
        private bool _picking;
        private IntPtr _mouseHook;
        private WinApi.LowLevelMouseProc? _mouseProc;

        private const string AppVersion = "2.0.0";

        // 绑定图标颜色：未绑定=红，已绑定=绿
        private static readonly Color BindColorRed = Color.FromRgb(0xE5, 0x39, 0x35);
        private static readonly Color BindColorGreen = Color.FromRgb(0x2E, 0x7D, 0x32);

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            cmbFilter.ItemsSource = (FilterType[])Enum.GetValues(typeof(FilterType));
            cmbFilter.SelectedIndex = 0;   // 默认「全部」

            _pasteGuard = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _pasteGuard.Tick += (_, _) =>
            {
                _pasteGuard.Stop();
                _internalPaste = false;
                _lastPastedContent = string.Empty;
            };

            _tray = new TrayService(ShowWindow, ExitApp);
            this.PreviewKeyDown += OnPreviewKeyDown;

            // 默认悬浮置顶，并高亮 📌 按钮
            _topmost = true;
            Topmost = true;
            btnTop.Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42));

            UpdateBindIcon();   // 初始未绑定 → 红色

            // 独立隐藏窗口监听系统剪贴板，主窗口收起后仍持续工作
            _monitor = new ClipboardMonitorService(
                text => _vm.AddFromClipboard(text),
                () => _internalPaste,
                () => _lastPastedContent);

            SourceInitialized += OnSourceInitialized;
        }

        // ---------- 剪贴板监听 + 热键 ----------
        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(_hwnd);
            source.AddHook(HwndHook);
            WinApi.RegisterHotKey(_hwnd, 1, WinApi.MOD_CONTROL, WinApi.VK_OEM_3);
            PositionToRightSide();   // 默认贴屏幕右侧、宽度缩为原始的 30%、高度不变
        }

        // 默认停靠屏幕右侧：宽度约 380（原 30% 过窄，已调整），高度不变，垂直居中
        private void PositionToRightSide()
        {
            const double targetWidth = 380;
            Width = targetWidth;
            var wa = SystemParameters.WorkArea;
            Left = Math.Max(0, wa.Width - Width);
            Top = Math.Max(0, (wa.Height - Height) / 2);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WinApi.WM_HOTKEY)
            {
                handled = true;
                ToggleVisibility();
            }
            return IntPtr.Zero;
        }

        // ---------- 窗口显隐 ----------
        private void ToggleVisibility()
        {
            if (_picking) { CancelPick(); return; } // 点选中途按快捷键则取消
            if (Visibility == Visibility.Visible) Hide();
            else { ShowWindow(); }
        }

        private void ShowWindow()
        {
            // 记录呼出前的活动窗口，作为后续粘贴的目标（比 Deactivated 时机更可靠）
            _lastExternalWindow = WinApi.GetForegroundWindow();
            // 首次呼出：默认绑定到当前正在使用的程序（如 Excel / 浏览器）
            if (!_boundOnce)
            {
                _boundOnce = true;
                _boundWindow = _lastExternalWindow;
                _boundProcessName = ResolveProcessName(_lastExternalWindow);
                txtBindName.Text = _boundProcessName;
                UpdateBindIcon();
            }
            Show();
            Activate();
            lstItems.Focus();
        }

        // 点击绑定按钮：进入「点选模式」——鼠标变十字，隐藏窗口，等你点选目标程序窗口
        private void BtnBind_Click(object sender, RoutedEventArgs e)
        {
            if (_picking) { CancelPick(); return; }
            StartPick();
        }

        private void StartPick()
        {
            _picking = true;
            txtBindName.Text = "点选窗口…";
            SetCrosshairCursor();   // 全局鼠标变为十字
            Hide();                 // 隐藏自身，让用户去点别的窗口

            _mouseProc = MouseHookCallback;
            _mouseHook = WinApi.SetWindowsHookEx(WinApi.WH_MOUSE_LL, _mouseProc,
                                                 WinApi.GetModuleHandle(null), 0);
            // 钩子安装失败则回退：直接绑呼出前的外部窗口
            if (_mouseHook == IntPtr.Zero) EndPick(useExternal: true);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == WinApi.WM_LBUTTONDOWN)
            {
                var ms = Marshal.PtrToStructure<WinApi.MSLLHOOKSTRUCT>(lParam);
                var hwnd = WinApi.WindowFromPoint(ms.pt);
                var root = hwnd != IntPtr.Zero ? WinApi.GetAncestor(hwnd, WinApi.GA_ROOT) : IntPtr.Zero;
                var target = root != IntPtr.Zero ? root : hwnd;
                if (target != IntPtr.Zero && target != _hwnd) // 不绑定自身
                {
                    _boundWindow = target;
                    _boundProcessName = ResolveProcessName(target);
                }
                EndPick(useExternal: false);
                return WinApi.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
            }
            return WinApi.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // 结束点选：卸载钩子、恢复箭头光标、恢复窗口显示
        private void EndPick(bool useExternal)
        {
            if (_mouseHook != IntPtr.Zero)
            {
                WinApi.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            _mouseProc = null;
            _picking = false;
            RestoreCursor();

            if (useExternal && _lastExternalWindow != IntPtr.Zero)
            {
                _boundWindow = _lastExternalWindow;
                _boundProcessName = ResolveProcessName(_lastExternalWindow);
            }
            // 低层鼠标钩子回调本身就在 UI 线程执行，可直接更新 UI
            txtBindName.Text = _boundProcessName;
            UpdateBindIcon();
            if (Visibility != Visibility.Visible) ShowWindow();
        }

        // 取消点选（Esc / 再次点击 / 呼出快捷键时）
        private void CancelPick() => EndPick(useExternal: true);

        // 临时把系统普通箭头光标替换为十字光标（全局生效，直到 RestoreCursor）
        private void SetCrosshairCursor()
        {
            try
            {
                var cross = WinApi.LoadCursor(IntPtr.Zero, new IntPtr(WinApi.IDC_CROSS));
                if (cross != IntPtr.Zero) WinApi.SetSystemCursor(cross, WinApi.OCR_NORMAL);
            }
            catch { /* 光标替换失败不阻断点选逻辑 */ }
        }

        private void RestoreCursor()
        {
            try
            {
                // 复位所有系统光标到默认值：保证十字光标准确还原为箭头，
                // 且不依赖 LoadCursor 是否成功（避免被 try/catch 静默吞掉导致十字残留）
                WinApi.SystemParametersInfo(
                    WinApi.SPI_SETCURSORS, 0, IntPtr.Zero, WinApi.SPIF_SENDCHANGE);
            }
            catch { /* 忽略 */ }
        }

        // 解析窗口所属进程名（如 EXCEL / chrome），用于绑定按钮展示
        private static string ResolveProcessName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "未绑定";
            WinApi.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "未知";
            try
            {
                using var p = Process.GetProcessById((int)pid);
                return p.ProcessName;   // 仅进程名，不含文档标题
            }
            catch { return "已关闭"; }
        }

        // 是否已有效绑定到某个进程窗口（决定靶心图标红/绿）
        private bool IsWindowBound()
            => _boundWindow != IntPtr.Zero
               && _boundProcessName != "未绑定"
               && _boundProcessName != "已关闭";

        // 刷新关联按钮靶心图标颜色：未绑定红、已绑定绿
        private void UpdateBindIcon()
        {
            if (Resources["BindIconBrush"] is SolidColorBrush brush)
                brush.Color = IsWindowBound() ? BindColorGreen : BindColorRed;
        }

        // 实际粘贴目标：优先用绑定的进程窗口（仍有效），否则退回上次外部窗口
        private IntPtr GetPasteTarget()
        {
            if (_boundWindow != IntPtr.Zero && WinApi.IsWindow(_boundWindow))
                return _boundWindow;
            return _lastExternalWindow;
        }

        // ---------- 标题栏 ----------
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return; // 按钮不触发拖拽
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void BtnMin_Click(object sender, RoutedEventArgs e) => Hide();

        private void BtnTop_Click(object sender, RoutedEventArgs e)
        {
            _topmost = !_topmost;
            Topmost = _topmost;
            btnTop.Background = _topmost
                ? new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42))
                : Brushes.Transparent;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => ExitApp();

        private void ExitApp()
        {
            if (_picking) { WinApi.UnhookWindowsHookEx(_mouseHook); RestoreCursor(); }
            if (_hwnd != IntPtr.Zero)
                WinApi.UnregisterHotKey(_hwnd, 1);
            _monitor?.Dispose();
            _tray?.Dispose();
            Application.Current.Shutdown();
        }

        // ---------- 右键菜单 ----------
        private void Window_PreviewRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 在 TextBox（搜框/筛选下拉的内部输入框）上保留原生右键菜单（粘贴/全选），
            // 不弹本程序的菜单；让用户对输入控件的右键体验更贴近原生。
            if (e.OriginalSource is TextBox) return;
            if (Resources["MainMenu"] is not ContextMenu menu) { e.Handled = true; return; }
            // 动态刷新模式项、复制模式项标题，直观显示当前状态并提示点击切换
            foreach (var item in menu.Items)
            {
                if (item is not MenuItem mi) continue;
                switch (mi.Tag as string)
                {
                    case "Mode":
                        mi.Header = _vm.Mode == PasteMode.Quick
                            ? "模式：快速（点此切普通）"
                            : "模式：普通（点此切快速）";
                        break;
                    case "CopyMode":
                        mi.Header = TableParser.SplitSingleColumn
                            ? "复制模式：表格复制（点此切一般）"
                            : "复制模式：一般复制（点此切表格）";
                        break;
                }
            }
            menu.PlacementTarget = this;
            menu.IsOpen = true;
            e.Handled = true;
        }

        private void MenuMode_Click(object sender, RoutedEventArgs e)
        {
            _vm.Mode = _vm.Mode == PasteMode.Normal ? PasteMode.Quick : PasteMode.Normal;
            if (_vm.Mode == PasteMode.Quick) lstItems.Focus();
        }

        // 复制模式开关：一般复制（默认，多行纯文本不拆）⇄ 表格复制（单列/多行也拆）
        private void MenuCopyMode_Click(object sender, RoutedEventArgs e)
        {
            TableParser.SplitSingleColumn = !TableParser.SplitSingleColumn;
        }

        // 打开新手使用帮助
        private void MenuHelp_Click(object sender, RoutedEventArgs e)
        {
            var help = new HelpWindow { Owner = this };
            help.ShowDialog();
        }

        private void MenuAbout_Click(object sender, RoutedEventArgs e)
        {
            var mode = _vm.Mode == PasteMode.Quick ? "快速" : "普通";
            var copyMode = TableParser.SplitSingleColumn ? "表格复制" : "一般复制";
            MessageBox.Show(
                "SuperClip 超级剪贴板\n\n" +
                "• 本地运行，无网络依赖\n" +
                "• 全局快捷键 Ctrl+` 呼出 / 隐藏\n" +
                "• 当前模式：" + mode + "\n" +
                "• 复制模式：" + copyMode + "\n" +
                "• 绑定目标：" + _boundProcessName + "\n\n" +
                "by Mr lin",
                "关于 SuperClip  v" + AppVersion, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ---------- 工具栏 ----------
        private void BtnClear_Click(object sender, RoutedEventArgs e) => _vm.ClearAll();
        private void BtnReset_Click(object sender, RoutedEventArgs e) => _vm.Reset();

        // 筛选下拉：手动同步到 VM（避免 enum 与 ComboBox 双向绑定偶发不同步，
        // 表现为「全部」筛选项显示空列表）
        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbFilter.SelectedItem is FilterType ft)
                _vm.FilterType = ft;
        }

        // ---------- 列表交互 ----------
        private void LstItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_vm.Mode != PasteMode.Normal) return;
            var item = ItemFromSource(e.OriginalSource);
            if (item != null) DoPaste(item, false);
        }

        private void LstItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 快速模式：单击仅切换选中（不粘贴）；普通模式忽略
            _vm.SelectItem(lstItems.SelectedItem as ClipItem);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Esc：点选中途取消
            if (e.Key == Key.Escape && _picking) { CancelPick(); e.Handled = true; return; }
            // 窗口级捕获空格：快速模式下粘贴选中项（焦点在输入控件时除外，
            // 否则在搜框或筛选下拉里按空格会同时触发粘贴 + 原生行为）
            if (e.Key == Key.Space && _vm.Mode == PasteMode.Quick && _vm.SelectedItem != null)
            {
                if (Keyboard.FocusedElement is TextBox or ComboBox) return;
                e.Handled = true;
                DoPaste(_vm.SelectedItem, true);
            }
        }

        private void Favorite_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is ClipItem item)
                _vm.ToggleFavorite(item);
        }

        private static ClipItem? ItemFromSource(object source)
        {
            var dep = source as DependencyObject;
            while (dep != null && dep is not ListBoxItem)
                dep = VisualTreeHelper.GetParent(dep);
            return (dep as ListBoxItem)?.DataContext as ClipItem;
        }

        // ---------- 粘贴 ----------
        private async void DoPaste(ClipItem item, bool moveToEnd)
        {
            _internalPaste = true;       // 屏蔽本次写剪贴板触发的自监听
            _lastPastedContent = item.Content; // 内容比对拦截（兜底）
            await PasteService.PasteTextAsync(item.Content, GetPasteTarget());
            _vm.PasteDone(item, moveToEnd);
            _pasteGuard.Stop();
            _pasteGuard.Start();         // 400ms 后重置 _internalPaste 与 _lastPastedContent
        }
    }
}
