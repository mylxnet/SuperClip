using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using SuperClip.Native;

namespace SuperClip.Services
{
    /// <summary>
    /// 模拟键入：把文本写入系统剪贴板 → 激活目标窗口 → 发送 Ctrl+V。
    /// 调用方需在调用前设置内部粘贴标志，以屏蔽本次写剪贴板触发的自监听。
    /// 全部使用 await Task.Delay 释放 UI 线程，避免 60ms 同步卡顿。
    /// </summary>
    public static class PasteService
    {
        public static async Task PasteTextAsync(string text, IntPtr targetHwnd)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                // 1. 写入剪贴板（会触发 WM_CLIPBOARDUPDATE，由调用方忽略）
                Clipboard.SetText(text);

                if (targetHwnd == IntPtr.Zero) return; // 无目标窗口则不粘贴，避免粘回自己

                IntPtr selfHwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (targetHwnd == selfHwnd) return;   // 目标就是自己，跳过

                // 2. 把焦点稳稳还给目标窗口（关键：绕过 Windows 前台锁，否则 Ctrl+V 会粘错地方）
                WinApi.GetWindowThreadProcessId(targetHwnd, out uint targetPid);
                WinApi.AllowSetForegroundWindow(targetPid);   // SuperClip 当前是前台，授权目标抢占前台
                WinApi.SetForegroundWindow(targetHwnd);
                WinApi.SetActiveWindow(targetHwnd);           // 一并激活，部分程序需要
                // 若前台锁仍生效，用更强力的方式兜底（Win10/11 仍可用）
                if (WinApi.GetForegroundWindow() != targetHwnd)
                    WinApi.SwitchToThisWindow(targetHwnd, true);
                // 等待目标真正获得键盘焦点（Excel 等程序切换有微小延迟）；
                // 改为异步等待，UI 线程可在期间处理 WM_CLIPBOARDUPDATE 等消息
                await Task.Delay(60);

                // 3. 发送 Ctrl+V 到目标窗口光标处
                WinApi.keybd_event(WinApi.VK_CONTROL, 0, 0, UIntPtr.Zero);
                WinApi.keybd_event(WinApi.VK_V, 0, 0, UIntPtr.Zero);
                WinApi.keybd_event(WinApi.VK_V, 0, WinApi.KEYEVENTF_KEYUP, UIntPtr.Zero);
                WinApi.keybd_event(WinApi.VK_CONTROL, 0, WinApi.KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch
            {
                // 粘贴失败静默处理，不影响程序
            }
        }
    }
}
