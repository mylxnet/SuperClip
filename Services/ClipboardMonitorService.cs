using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using SuperClip.Native;

namespace SuperClip.Services
{
    /// <summary>
    /// 剪贴板监听服务：创建独立的隐藏消息窗口，注册 WM_CLIPBOARDUPDATE 监听。
    /// 不依赖主窗口显隐，程序常驻期间（含主窗口收起）持续监听系统剪贴板。
    /// 所有调用均为本地 Win32，无任何网络依赖。
    /// </summary>
    public sealed class ClipboardMonitorService : IDisposable
    {
        private readonly HwndSource _hwndSource;
        private readonly Action<string> _onClipboardText;
        private readonly Func<bool> _isInternalPaste; // 用于屏蔽"程序自身粘贴写入剪贴板"引发的递归监听
        private readonly Func<string> _getLastPasted;  // 返回最近一次粘贴的内容，用于内容比对拦截自粘贴
        private bool _disposed;

        /// <param name="onClipboardText">剪贴板出现新文本时回调（已是纯文本）</param>
        /// <param name="isInternalPaste">返回 true 表示当前正在由本程序写入剪贴板（粘贴），应忽略本次通知</param>
        /// <param name="getLastPasted">返回最近一次粘贴到目标窗口的内容；若新剪贴板文本与之相同则判定为本程序自写入，跳过</param>
        public ClipboardMonitorService(Action<string> onClipboardText, Func<bool> isInternalPaste, Func<string>? getLastPasted = null)
        {
            _onClipboardText = onClipboardText ?? throw new ArgumentNullException(nameof(onClipboardText));
            _isInternalPaste = isInternalPaste ?? (() => false);
            _getLastPasted = getLastPasted ?? (() => string.Empty);

            // 创建屏幕外、零尺寸的弹出式隐藏窗口，仅用于接收剪贴板消息
            var param = new HwndSourceParameters("SuperClipMonitor")
            {
                Width = 0,
                Height = 0,
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP
            };
            param.SetPosition(-32000, -32000); // 移出可见区域，彻底不可见
            _hwndSource = new HwndSource(param);

            _hwndSource.AddHook(WndProc);
            if (!WinApi.AddClipboardFormatListener(_hwndSource.Handle))
            {
                // 监听注册失败不致命，UI 仍可手动复制粘贴，只是不会自动入列表
                System.Diagnostics.Debug.WriteLine("AddClipboardFormatListener 失败");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WinApi.WM_CLIPBOARDUPDATE)
            {
                handled = true;
                // 屏蔽本程序粘贴时写入剪贴板引发的自身监听
                if (_isInternalPaste()) return IntPtr.Zero;
                // 延后到 UI 线程读取并重试，规避源程序（如 Excel）复制时剪贴板短暂被锁导致读取失败。
                // ReadClipboardWithRetryAsync 内部使用 await Task.Delay 释放 UI 线程，不再用 Thread.Sleep 卡顿。
                _hwndSource.Dispatcher.BeginInvoke(new Action(async () => await ReadClipboardWithRetryAsync()));
            }
            return IntPtr.Zero;
        }

        // 异步读取剪贴板：用 await Task.Delay 释放 UI 线程；非文本内容立即返回不浪费重试。
        private async Task ReadClipboardWithRetryAsync()
        {
            var last = _getLastPasted();
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    if (!Clipboard.ContainsText())
                        return; // 非文本（图片/文件等）直接返回，不阻塞 UI
                    var text = Clipboard.GetText();
                    if (string.IsNullOrEmpty(text))
                        return; // 空文本无需继续重试
                    // 内容比对：若与最近一次粘贴内容相同，判定为本程序自写入，跳过
                    if (text == last) return;
                    _onClipboardText(text);
                    return;
                }
                catch
                {
                    // 剪贴板被其他进程独占时访问失败，释放 UI 线程后重试
                }
                await Task.Delay(25);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { WinApi.RemoveClipboardFormatListener(_hwndSource.Handle); } catch { }
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
        }
    }
}
