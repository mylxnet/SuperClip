using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SuperClip.Models;

namespace SuperClip.Services
{
    /// <summary>
    /// 本地 JSON 持久化（无网络）。文件位于 %AppData%/SuperClip/history.json。
    /// 保存时按列表当前顺序序列化，确保重启后排序、收藏、灰显全部保留。
    /// 写入采用「写 .tmp → 原子重命名」策略，避免崩溃在半路损坏主文件。
    /// </summary>
    public static class StorageService
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SuperClip");

        private static string FilePath => Path.Combine(AppDir, "history.json");
        private static string TempPath => FilePath + ".tmp";

        /// <summary>内容 SHA-256 哈希，用于连续复制去重。</summary>
        public static string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content)) return string.Empty;
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public static void Save(IEnumerable<ClipItem> items)
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = false });
                // 原子写：先写临时文件，再覆盖主文件。即使程序在写一半崩溃，主文件也不会损坏。
                // File.Move(overwrite: true) 在 .NET Core 3.0+ 可用，内部使用 ReplaceFile/MoveFileEx。
                File.WriteAllText(TempPath, json);
                if (File.Exists(FilePath))
                    File.Replace(TempPath, FilePath, destinationBackupFileName: null);
                else
                    File.Move(TempPath, FilePath);
            }
            catch
            {
                // 持久化失败不应中断主流程；尝试清理残留 .tmp
                try { if (File.Exists(TempPath)) File.Delete(TempPath); } catch { }
            }
        }

        public static List<ClipItem> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<ClipItem>();
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json)) return new List<ClipItem>();
                return JsonSerializer.Deserialize<List<ClipItem>>(json) ?? new List<ClipItem>();
            }
            catch
            {
                return new List<ClipItem>();
            }
        }
    }
}
