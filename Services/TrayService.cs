using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using SuperClip.Native;

namespace SuperClip.Services
{
    /// <summary>
    /// 系统托盘常驻：纯 Win32（Shell_NotifyIcon）实现，无 WinForms 依赖，
    /// 纯 Win32 实现，无 WinForms 依赖，发布更干净。图标 + 双击打开 + 右键「打开 / 退出」。无任何网络依赖。
    /// </summary>
    public sealed class TrayService : IDisposable
    {
        private readonly HwndSource _hwndSource;
        private readonly IntPtr _hwnd;
        private readonly Action _onOpen;
        private readonly Action _onExit;
        private readonly uint _wmTrayIcon = 0x8000 + 1; // WM_APP + 1
        private uint _taskbarCreatedMsg;
        private bool _disposed;

        public TrayService(Action onOpen, Action onExit)
        {
            _onOpen = onOpen;
            _onExit = onExit;

            var param = new HwndSourceParameters("SuperClipTray")
            {
                Width = 0,
                Height = 0,
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP
            };
            param.SetPosition(-32000, -32000);
            _hwndSource = new HwndSource(param);
            _hwnd = _hwndSource.Handle;
            _hwndSource.AddHook(WndProc);

            _taskbarCreatedMsg = WinApi.RegisterWindowMessage("TaskbarCreated");
            AddTrayIcon();
        }

        private static IntPtr LoadAppIcon()
        {
            try
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(exe))
                {
                    ushort idx;
                    var h = WinApi.ExtractAssociatedIcon(IntPtr.Zero, exe, out idx);
                    if (h != IntPtr.Zero) return h;
                }
            }
            catch { }
            return WinApi.LoadIcon(IntPtr.Zero, new IntPtr(WinApi.IDI_APPLICATION));
        }

        private void AddTrayIcon()
        {
            var data = new WinApi.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WinApi.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = WinApi.NIF_MESSAGE | WinApi.NIF_ICON | WinApi.NIF_TIP,
                uCallbackMessage = _wmTrayIcon,
                szTip = "SuperClip"
            };
            data.hIcon = LoadAppIcon();
            if (!WinApi.Shell_NotifyIcon(WinApi.NIM_ADD, ref data))
            {
                // 托盘添加失败（如 Explorer 未就绪），用户从快捷键/标题栏仍可使用全部功能
                System.Diagnostics.Debug.WriteLine("Shell_NotifyIcon NIM_ADD 失败");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == (int)_wmTrayIcon)
            {
                handled = true;
                uint mouse = (uint)lParam;
                if (mouse == WinApi.WM_LBUTTONDBLCLK) _onOpen?.Invoke();
                else if (mouse == WinApi.WM_RBUTTONUP) ShowContextMenu();
                return IntPtr.Zero;
            }
            if (msg == (int)_taskbarCreatedMsg)
            {
                // 资源管理器重启后图标会消失，重新添加
                handled = true;
                AddTrayIcon();
            }
            return IntPtr.Zero;
        }

        private void ShowContextMenu()
        {
            var menu = WinApi.CreatePopupMenu();
            WinApi.AppendMenu(menu, WinApi.MF_STRING, 1001, "打开");
            WinApi.AppendMenu(menu, WinApi.MF_STRING, 1002, "退出");

            WinApi.GetCursorPos(out WinApi.POINT pt);
            WinApi.SetForegroundWindow(_hwnd); // 让弹出菜单能正常消失
            uint cmd = WinApi.TrackPopupMenuEx(menu, WinApi.TPM_RETURNCMD | WinApi.TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, IntPtr.Zero);
            WinApi.DestroyMenu(menu);

            if (cmd == 1001) _onOpen?.Invoke();
            else if (cmd == 1002) _onExit?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            var data = new WinApi.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<WinApi.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1
            };
            WinApi.Shell_NotifyIcon(WinApi.NIM_DELETE, ref data);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
        }
    }
}
