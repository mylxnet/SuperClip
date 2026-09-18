using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SuperClip.Services
{
    /// <summary>
    /// 用户设置持久化：窗口位置/大小、绑定目标进程名、置顶状态。
    /// 文件位于 %AppData%\SuperClip\settings.json，与 history.json 同目录。
    /// 使用原子写策略，与 StorageService 保持一致。
    /// </summary>
    public static class SettingsService
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SuperClip");

        private static string FilePath => Path.Combine(AppDir, "settings.json");
        private static string TempPath => FilePath + ".tmp";

        public sealed class UserSettings
        {
            // 窗口位置（屏幕坐标系，单位 dp）
            public double Left { get; set; } = 0;
            public double Top { get; set; } = 0;
            public double Width { get; set; } = 380;
            public double Height { get; set; } = 600;

            // 绑定目标进程名（如 "EXCEL"、"chrome"）
            public string? BoundProcessName { get; set; }

            // 窗口置顶状态
            public bool Topmost { get; set; } = true;

            // 粘贴模式（Normal=0, Quick=1）
            public int PasteMode { get; set; } = 0;

            // 复制模式（一般复制=false, 表格复制=true）
            public bool SplitSingleColumn { get; set; } = false;

            // 筛选类型索引（All=0, Text=1, TableCell=2, Favorite=3）
            public int FilterType { get; set; } = 0;
        }

        /// <summary>加载用户设置；文件不存在或损坏时返回默认值。</summary>
        public static UserSettings Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new UserSettings();
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json)) return new UserSettings();
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                return new UserSettings();
            }
        }

        /// <summary>原子保存用户设置。</summary>
        public static void Save(UserSettings settings)
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = false
                });
                File.WriteAllText(TempPath, json);
                if (File.Exists(FilePath))
                    File.Replace(TempPath, FilePath, destinationBackupFileName: null);
                else
                    File.Move(TempPath, FilePath);
            }
            catch
            {
                try { if (File.Exists(TempPath)) File.Delete(TempPath); } catch { }
            }
        }
    }
}
