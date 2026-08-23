using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SuperClip.Models
{
    public enum ClipType
    {
        Text,       // 普通文本
        TableCell   // Excel 表格拆分出的单元格
    }

    public partial class ClipItem : ObservableObject
    {
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public string Content { get; init; } = string.Empty;
        public ClipType Type { get; init; } = ClipType.Text;
        public int? SourceRow { get; init; }
        public int? SourceCol { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.Now;
        public string Hash { get; init; } = string.Empty;

        // 收藏状态（★/☆），UI 需实时反映
        [ObservableProperty]
        private bool _isFavorite;

        // 已粘贴（灰显）状态，持久化后在重启后保留
        [ObservableProperty]
        private bool _isPasted;

        /// <summary>来源标注，例如「来自表格：第 1 行 第 2 列」；普通文本为空。</summary>
        public string SourceLabel =>
            Type == ClipType.TableCell && SourceRow.HasValue && SourceCol.HasValue
                ? $"来自表格：第 {SourceRow} 行 第 {SourceCol} 列"
                : string.Empty;
    }
}
