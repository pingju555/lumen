using System;
using System.Collections.Generic;
using System.Windows;

namespace Lumen.Render
{
    /// <summary>
    /// 网格模型（P3-01）：gcd 间距推导 + 四档可选 + cell↔px 换算 + 吸附。
    /// 四档 {20,40,60,120} 以 1920×1080 基准推导（gcd=120 的约数细分）。
    /// 详见 docs/project/phases/P3_网格小组件页面/P3-01_网格.md
    /// </summary>
    public static class GridModel
    {
        /// <summary>四档网格间距（px）。1920×1080 → gcd=120；此四档为 120 的细分约数。</summary>
        public static readonly int[] PRESETS = { 20, 40, 60, 120 };

        /// <summary>返回距离 target 最近的四档档位索引。</summary>
        public static int NearestGear(double target)
        {
            int best = 0; double bestD = double.MaxValue;
            for (int i = 0; i < PRESETS.Length; i++)
            {
                double d = Math.Abs(PRESETS[i] - target);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>欧几里得 gcd（正整数）。</summary>
        public static int Gcd(int w, int h)
        {
            w = Math.Abs(w); h = Math.Abs(h);
            while (h != 0) { int t = h; h = w % h; w = t; }
            return w == 0 ? 1 : w;
        }

        /// <summary>工作区可容纳的 cell 列/行数（向上取整）。</summary>
        public static (int cols, int rows) CellsFor(double width, double height, double gridSize)
            => ((int)Math.Ceiling(width / gridSize), (int)Math.Ceiling(height / gridSize));

        /// <summary>把画布像素点吸附到最近 cell 原点。</summary>
        public static Point SnapToCell(Point canvasPt, double gridSize)
            => new Point(Math.Round(canvasPt.X / gridSize) * gridSize,
                         Math.Round(canvasPt.Y / gridSize) * gridSize);

        /// <summary>cell 坐标 → 画布像素原点。</summary>
        public static Point CellToPx(int col, int row, double gridSize)
            => new Point(col * gridSize, row * gridSize);

        /// <summary>画布像素点 → cell 坐标（四舍五入）。</summary>
        public static GridCell PxToCell(Point canvasPt, double gridSize)
            => new GridCell((int)Math.Round(canvasPt.X / gridSize),
                            (int)Math.Round(canvasPt.Y / gridSize));

        /// <summary>占用冲突检测：目标 cell 区间是否与已占集合重叠。</summary>
        public static bool Overlaps(Rect cellSpan, ISet<(int col, int row)> taken)
        {
            int c0 = (int)Math.Round(cellSpan.X);
            int r0 = (int)Math.Round(cellSpan.Y);
            int c1 = c0 + (int)Math.Round(cellSpan.Width) - 1;
            int r1 = r0 + (int)Math.Round(cellSpan.Height) - 1;
            for (int c = c0; c <= c1; c++)
                for (int r = r0; r <= r1; r++)
                    if (taken.Contains((c, r))) return true;
            return false;
        }

        /// <summary>把 cell 区间写入占用集合。</summary>
        public static void Mark(Rect cellSpan, ISet<(int col, int row)> taken)
        {
            int c0 = (int)Math.Round(cellSpan.X);
            int r0 = (int)Math.Round(cellSpan.Y);
            int c1 = c0 + (int)Math.Round(cellSpan.Width) - 1;
            int r1 = r0 + (int)Math.Round(cellSpan.Height) - 1;
            for (int c = c0; c <= c1; c++)
                for (int r = r0; r <= r1; r++)
                    taken.Add((c, r));
        }
    }
}
