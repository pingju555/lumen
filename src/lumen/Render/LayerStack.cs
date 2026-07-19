using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Lumen.Render
{
    /// <summary>
    /// 层栈：持有各 Layer，按 ZIndex 排序挂载到宿主 Panel，绑定 Opacity / Enabled。
    /// 详见 docs/project/phases/P1_渲染基座与画布/P1-01_多层模型.md
    /// </summary>
    public class LayerStack
    {
        public ObservableCollection<Layer> Layers { get; } = new();
        public Panel Host { get; private set; }

        public void Attach(Panel host)
        {
            Host = host;
            Recompose();
        }

        /// <summary>ZIndex 重排 + 挂载各层 Root 到宿主；Opacity/Enabled 即时生效。</summary>
        public void Recompose()
        {
            if (Host == null) return;
            Host.Children.Clear();
            foreach (var layer in Layers.OrderBy(l => l.ZIndex))
            {
                var p = layer.Root;
                Panel.SetZIndex(p, layer.ZIndex);
                p.Opacity = layer.Opacity;
                p.Visibility = layer.Enabled ? Visibility.Visible : Visibility.Collapsed;
                // 层容器透明区域不命中 -> 点击空白被覆盖层吸收（不穿透桌面）；
                // 交互元素（原子）自行 IsHitTestVisible=True 接收事件。
                Host.Children.Add(p);
            }
        }

        /// <summary>插入三类预设层之一，默认 z = (int)kind + 1（壁纸1/网格2/画布3）。</summary>
        public Layer AddPreset(LayerKind kind)
        {
            Layer l = kind switch
            {
                LayerKind.Wallpaper => new WallpaperLayer(),
                LayerKind.Grid => new GridLayer(),
                LayerKind.Canvas => new CanvasLayer(),
                _ => throw new System.ArgumentOutOfRangeException(nameof(kind))
            };
            l.ZIndex = (int)kind + 1;
            Layers.Add(l);
            return l;
        }
    }
}
