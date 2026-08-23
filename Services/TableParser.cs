using System;
using System.Collections.Generic;

namespace SuperClip.Services
{
    /// <summary>
    /// Excel 表格（TSV）识别与单元格拆分。
    /// 规则：列分隔符 \t，行分隔符 \r\n 或 \n；以文本（TSV）解析为准。
    /// </summary>
    public static class TableParser
    {
        /// <summary>
        /// 复制模式开关。
        /// true  = 表格复制：单列/多行（仅有换行、无 \t）也按单元格拆分；
        /// false = 一般复制（默认）：仅含制表符的多列表格才拆分，多行纯文本按整段文本记录。
        /// </summary>
        public static bool SplitSingleColumn { get; set; } = false;

        /// <summary>
        /// 是否看起来像表格：
        /// - 含制表符（多列/单行）→ 始终视为 TSV，按单元格拆分；
        /// - 单列/多行（仅有换行、无 \t）→ 仅在「表格复制」模式下才拆分。
        /// 单行文本（无 \t 无换行）始终按普通文本处理。
        /// </summary>
        public static bool IsTable(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Contains('\t')) return true;                       // 多列表格，始终拆
            return SplitSingleColumn                                   // 单列/多行：仅表格复制模式拆
                && (text.Contains('\n') || text.Contains('\r'));
        }

        /// <summary>
        /// 按「行 → 列」顺序拆分出单元格。
        /// - 含制表符的行：按 \t 拆成多列（支持单行多列）。
        /// - 无制表符的行（单列/多行）：整行作为一个单元格（支持单列）。
        /// 跳过完全为空的单元格（空白无意义），如需保留空单元格可去掉该判断。
        /// </summary>
        public static List<Cell> Parse(string text)
        {
            var result = new List<Cell>();
            if (!IsTable(text)) return result;

            // 统一换行符并去掉首尾空行
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n');
            if (normalized.Length == 0) return result;

            var rows = normalized.Split('\n');
            for (int r = 0; r < rows.Length; r++)
            {
                var line = rows[r];
                if (!line.Contains('\t'))
                {
                    // 单列：整行作为一个单元格
                    if (!string.IsNullOrEmpty(line))
                        result.Add(new Cell { Content = line, Row = r + 1, Col = 1 });
                }
                else
                {
                    var cols = line.Split('\t');
                    for (int c = 0; c < cols.Length; c++)
                    {
                        if (string.IsNullOrEmpty(cols[c])) continue; // 跳过空单元格
                        result.Add(new Cell { Content = cols[c], Row = r + 1, Col = c + 1 });
                    }
                }
            }
            return result;
        }
    }

    public class Cell
    {
        public string Content { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Col { get; set; }
    }
}
