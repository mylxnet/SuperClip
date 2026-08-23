using CommunityToolkit.Mvvm.ComponentModel;
using SuperClip.Models;
using SuperClip.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

namespace SuperClip.ViewModels
{
    public enum FilterType { All, Text, TableCell, Favorite }
    public enum PasteMode { Normal, Quick }

    public partial class MainViewModel : ObservableObject
    {
        private const int MaxItems = 500;

        [ObservableProperty]
        private ObservableCollection<ClipItem> _items = new();

        [ObservableProperty]
        private ObservableCollection<ClipItem> _displayItems = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private FilterType _filterType = FilterType.All;

        [ObservableProperty]
        private PasteMode _mode = PasteMode.Normal;

        [ObservableProperty]
        private ClipItem? _selectedItem;

        private readonly DispatcherTimer _searchTimer;

        public MainViewModel()
        {
            foreach (var it in StorageService.Load())
                Items.Add(it);
            ApplyOrder();
            Save();
            RefreshDisplay();

            // 搜索防抖：300ms 内不再输入才刷新
            _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); RefreshDisplay(); };
        }

        partial void OnSearchTextChanged(string value)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        partial void OnFilterTypeChanged(FilterType value) => RefreshDisplay();

        partial void OnModeChanged(PasteMode value)
        {
            // 模式切换时选中当前可见列表最顶部（不选被筛选过滤掉的项）
            SelectedItem = DisplayItems.FirstOrDefault();
        }

        // ---------- 剪贴板新增 ----------
        public void AddFromClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            if (TableParser.IsTable(text))
                AddTable(TableParser.Parse(text));
            else
                AddSingle(text, ClipType.Text, null, null);

            EnforceLimit();
            Save();
            RefreshDisplay();
        }

        // Excel 表格：按「行→列」解析后，以整块顺序插入到非收藏区最前，
        // 避免逐条前插导致的整体倒序。
        private void AddTable(List<Cell> cells)
        {
            if (cells == null || cells.Count == 0) return;

            // 先按内容去重（相同单元格只保留一条，更新时间戳）
            foreach (var cell in cells)
            {
                var hash = StorageService.ComputeHash(cell.Content);
                var existing = Items.FirstOrDefault(x => x.Hash == hash);
                if (existing != null) Items.Remove(existing);
            }

            var firstNonFav = Items.FirstOrDefault(x => !x.IsFavorite);
            int idx = firstNonFav == null ? Items.Count : Items.IndexOf(firstNonFav);
            int i = idx;
            foreach (var cell in cells)
            {
                var item = new ClipItem
                {
                    Content = cell.Content,
                    Type = ClipType.TableCell,
                    SourceRow = cell.Row,
                    SourceCol = cell.Col,
                    Timestamp = DateTime.Now,
                    Hash = StorageService.ComputeHash(cell.Content)
                };
                Items.Insert(i, item);
                i++;
            }
        }

        private void AddSingle(string content, ClipType type, int? row, int? col)
        {
            var hash = StorageService.ComputeHash(content);

            // 去重：相同内容只保留一条（更新时间戳）
            var existing = Items.FirstOrDefault(x => x.Hash == hash);
            if (existing != null) Items.Remove(existing);

            var item = new ClipItem
            {
                Content = content,
                Type = type,
                SourceRow = row,
                SourceCol = col,
                Timestamp = DateTime.Now,
                Hash = hash
            };
            if (existing != null) item.IsFavorite = existing.IsFavorite; // 保留收藏状态

            // 插入到非收藏区最前（收藏始终置顶分组）
            var firstNonFav = Items.FirstOrDefault(x => !x.IsFavorite);
            int idx = firstNonFav == null ? Items.Count : Items.IndexOf(firstNonFav);
            Items.Insert(idx, item);
        }

        private void EnforceLimit()
        {
            int nonFav = Items.Count(x => !x.IsFavorite);
            while (nonFav > MaxItems)
            {
                var oldest = Items.First(x => !x.IsFavorite); // 非收藏区最前 = 最旧
                Items.Remove(oldest);
                nonFav--;
            }
        }

        // 收藏置顶：保持各自相对顺序（原地 Move，不清空整个集合，
        // 避免 WPF 重置选中/滚动位置、避免整屏闪烁）。
        private void ApplyOrder()
        {
            var favs = Items.Where(x => x.IsFavorite).ToList();
            var others = Items.Where(x => !x.IsFavorite).ToList();
            var target = favs.Concat(others).ToList();
            SyncInPlace(Items, target);
        }

        // ---------- 收藏 ----------
        public void ToggleFavorite(ClipItem? item)
        {
            if (item == null) return;
            item.IsFavorite = !item.IsFavorite;
            ApplyOrder();
            Save();
            RefreshDisplay();
        }

        // ---------- 粘贴完成回调 ----------
        public void PasteDone(ClipItem? item, bool moveToEnd)
        {
            if (item == null) return;
            item.IsPasted = true;
            if (moveToEnd)
            {
                Items.Remove(item);
                Items.Add(item); // 沉底（非收藏区末尾）
            }
            Save();
            RefreshDisplay();
            // 快速模式：粘贴成功后默认选中列表最顶部（未粘贴）的数据，便于连续空格粘贴
            if (Mode == PasteMode.Quick)
                SelectedItem = DisplayItems.FirstOrDefault(x => !x.IsPasted) ?? DisplayItems.FirstOrDefault();
        }

        // ---------- 清除 / 复位 ----------
        public void ClearAll()
        {
            Items.Clear();
            SelectedItem = null;
            Save();
            RefreshDisplay();
        }

        public void Reset()
        {
            // 仅快速模式有意义：清除灰显并恢复最初（按时间）顺序
            foreach (var it in Items) it.IsPasted = false;
            var favs = Items.Where(x => x.IsFavorite).OrderBy(x => x.Timestamp).ToList();
            var others = Items.Where(x => !x.IsFavorite).OrderByDescending(x => x.Timestamp).ToList();
            var target = favs.Concat(others).ToList();
            SyncInPlace(Items, target);
            // 同步显示列表的顺序（让 DisplayItems 与 Items 一致）
            RefreshDisplay();
            SelectedItem = DisplayItems.FirstOrDefault();
            Save();
        }

        // 列表选择：所有模式都同步到 VM（之前仅 Quick 模式同步，Normal 模式点击条目不更新 VM，
        // 切到 Quick 后选中会跳到第一项，体验割裂）。
        public void SelectItem(ClipItem? item)
        {
            SelectedItem = item;
        }

        // ---------- 过滤/搜索 ----------
        private void Save() => StorageService.Save(Items);

        // 重建显示列表：原地增删/Move，不重新赋值 ItemsSource，保留选中与滚动位置。
        public void RefreshDisplay()
        {
            var target = BuildDisplayList();
            SyncInPlace(DisplayItems, target);
        }

        private List<ClipItem> BuildDisplayList()
        {
            IEnumerable<ClipItem> q = Items;
            switch (FilterType)
            {
                case FilterType.Text: q = q.Where(x => x.Type == ClipType.Text); break;
                case FilterType.TableCell: q = q.Where(x => x.Type == ClipType.TableCell); break;
                case FilterType.Favorite: q = q.Where(x => x.IsFavorite); break;
            }
            var kw = (SearchText ?? string.Empty).Trim();
            if (kw.Length > 0)
                q = q.Where(x => x.Content.Contains(kw, StringComparison.OrdinalIgnoreCase)
                              || x.SourceLabel.Contains(kw, StringComparison.OrdinalIgnoreCase));
            return q.ToList();
        }

        // 原地同步：让 col 最终持有 target 的所有元素，顺序一致。
        // 不重新赋值（避免 WPF ItemsSource Reset 导致的选中/滚动丢失）。
        private static void SyncInPlace<T>(ObservableCollection<T> col, IList<T> target) where T : class
        {
            if (ReferenceEquals(col, target)) return;
            // 1) 删除 col 中不在 target 的项
            var targetSet = new HashSet<T>(target);
            for (int i = col.Count - 1; i >= 0; i--)
            {
                if (!targetSet.Contains(col[i]))
                    col.RemoveAt(i);
            }
            // 2) 逐位匹配 target
            for (int i = 0; i < target.Count; i++)
            {
                if (i >= col.Count)
                {
                    col.Add(target[i]);
                }
                else if (!ReferenceEquals(col[i], target[i]))
                {
                    int existingIdx = col.IndexOf(target[i]);
                    if (existingIdx >= 0)
                        col.Move(existingIdx, i);
                    else
                        col.Insert(i, target[i]);
                }
            }
        }
    }
}
