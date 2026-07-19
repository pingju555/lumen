using System;
using System.Windows;

namespace Lumen.Render
{
    /// <summary>工作区像素坐标，原点左上（Auto-hide 即满屏）。</summary>
    public struct ScreenPoint
    {
        public double X, Y;
        public ScreenPoint(double x, double y) { X = x; Y = y; }
    }

    /// <summary>层内自由像素坐标，原点层左上。</summary>
    public struct CanvasPoint
    {
        public double X, Y;
        public CanvasPoint(double x, double y) { X = x; Y = y; }
    }

    /// <summary>网格单元坐标（P3 细化）。</summary>
    public struct GridCell
    {
        public int Col, Row;
        public GridCell(int col, int row) { Col = col; Row = row; }
    }

    /// <summary>九宫格锚点（画布定位体系）：相对父区域的 9 个参考位置。</summary>
    public enum NineAnchor
    {
        TopLeft, TopCenter, TopRight,
        MiddleLeft, Center, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }

    /// <summary>
    /// 三套坐标系换算 + 九宫格锚点解析。
    /// 详见 docs/project/phases/P1_渲染基座与画布/P1-02_坐标系.md
    /// </summary>
    public static class Coord
    {
        // P3 注入：活动网格档位（四档之一），原子/小组件拖拽吸附到此间距。
        public static double GridSize { get; set; } = 40;

        /// <summary>当前工作区尺寸（由宿主 ComposeCurrentPage 注入）：拖拽松手时把像素 Bounds 反解为九宫格偏移用。</summary>
        public static double AreaW { get; set; } = 1920;
        public static double AreaH { get; set; } = 1080;

        /// <summary>网格吸附开关：true=拖拽时吸附到网格（网格模式）；false=自由像素移动（画布模式）。由宿主按页面 ShowGrid 设定。</summary>
        public static bool SnapEnabled { get; set; } = true;

        /// <summary>把画布像素点吸附到最近 cell 原点（P3-01，仅 SnapEnabled 时生效）。</summary>
        public static Point Snap(Point canvasPt)
            => SnapEnabled ? GridModel.SnapToCell(canvasPt, GridSize) : canvasPt;

        /// <summary>
        /// 解析九宫格锚点 + XY偏移 → 实际像素坐标。
        /// anchor 决定基准点在父区域中的位置；offsetX/offsetY 从该基准点的像素偏移。
        /// </summary>
        public static Point ResolveAnchor(NineAnchor anchor, double offsetX, double offsetY,
                                          double areaW, double areaH)
        {
            // 计算基准点：根据锚点决定在区域中的 X/Y 位置
            double bx = anchor switch
            {
                NineAnchor.TopLeft or NineAnchor.MiddleLeft or NineAnchor.BottomLeft => 0,
                NineAnchor.TopCenter or NineAnchor.Center or NineAnchor.BottomCenter => areaW / 2,
                NineAnchor.TopRight or NineAnchor.MiddleRight or NineAnchor.BottomRight => areaW,
                _ => 0
            };
            double by = anchor switch
            {
                NineAnchor.TopLeft or NineAnchor.TopCenter or NineAnchor.TopRight => 0,
                NineAnchor.MiddleLeft or NineAnchor.Center or NineAnchor.MiddleRight => areaH / 2,
                NineAnchor.BottomLeft or NineAnchor.BottomCenter or NineAnchor.BottomRight => areaH,
                _ => 0
            };
            return new Point(bx + offsetX, by + offsetY);
        }

        public static ScreenPoint CanvasToScreen(Layer layer, CanvasPoint p)
            => new ScreenPoint(p.X + OffsetX(layer), p.Y + OffsetY(layer));

        public static CanvasPoint ScreenToCanvas(Layer layer, ScreenPoint p)
            => new CanvasPoint(p.X - OffsetX(layer), p.Y - OffsetY(layer));

        public static CanvasPoint GridToCanvas(GridCell c, double gridSize)
            => new CanvasPoint(c.Col * gridSize, c.Row * gridSize);

        // v1 单层覆盖全工作区 -> 层偏移(0,0)；多屏/多页扩展时在此注入。
        private static double OffsetX(Layer layer) => 0;
        private static double OffsetY(Layer layer) => 0;
    }
}
