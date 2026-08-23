using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SuperClip.Native;

namespace SuperClip
{
    public partial class App : Application
    {
        // 单实例互斥体：防止重复打开多个剪贴板程序
        private static Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 单实例：已存在实例则把它带到前台并退出当前进程
            const string mutexName = @"Global\SuperClip_SingleInstance_9F3A2B1C";
            _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                try
                {
                    var hwnd = WinApi.FindWindow(null, "SuperClip");
                    if (hwnd != IntPtr.Zero)
                    {
                        if (WinApi.IsIconic(hwnd)) WinApi.ShowWindow(hwnd, WinApi.SW_RESTORE);
                        WinApi.SetForegroundWindow(hwnd);
                    }
                }
                catch { }
                _singleInstanceMutex.Close();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            DispatcherUnhandledException += OnDispatcherUnhandled;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        }

        // UI 线程未处理异常：弹窗 + 写日志，并标记已处理避免进程直接退出
        private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogAndShow("UI", e.Exception);
            e.Handled = true;
        }

        // 任何线程的致命异常：写日志 + 弹窗
        private void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
        {
            LogAndShow("Fatal", e.ExceptionObject as Exception);
        }

        private static void LogAndShow(string kind, Exception? ex)
        {
            var detail = $"[{kind}] {DateTime.Now:HH:mm:ss}\n{ex?.ToString() ?? "未知错误"}";
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SuperClip");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), detail + "\n\n");
            }
            catch { }
            try
            {
                MessageBox.Show(detail, "SuperClip 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
        }
    }
}
