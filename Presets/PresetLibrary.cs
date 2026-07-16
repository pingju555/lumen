using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Lumen.Atoms;
using Lumen.Pages;
using Lumen.Persistence;
using Lumen.Render;

namespace Lumen.Presets
{
    /// <summary>
    /// 预设库（P3-04）：三类内置（WallpaperOnly / GridWorkspace / CanvasFree）+ 用户自定义（增删改 + JSON 导入导出）。
    /// Apply 重建当前页的网格档位 / 是否显示网格 / 背景。
    /// 详见 docs/project/phases/P3_网格小组件页面/P3-04_预设库.md
    /// </summary>
    public static class PresetLibrary
    {
        private static readonly Dictionary<string, Preset> _builtin =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Preset> _user =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly JsonSerializerOptions Opt =
            new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        static PresetLibrary() => BuildBuiltins();

        public static IReadOnlyCollection<Preset> Builtins => _builtin.Values;
        public static IReadOnlyCollection<Preset> User => _user.Values;

        /// <summary>清空用户预设（切换配置档时整体替换为目标档的用户预设）。</summary>
        public static void ClearUser() => _user.Clear();

        public static Preset GetBuiltin(string name)
            => _builtin.TryGetValue(name ?? "", out var p) ? p : null;

        public static Preset GetAny(string name)
            => GetBuiltin(name) ?? (_user.TryGetValue(name ?? "", out var p) ? p : null);

        public static bool AddUser(Preset p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Name)) return false;
            _user[p.Name] = p;
            return true;
        }

        public static bool RemoveUser(string name) => _user.Remove(name ?? "");

        /// <summary>
        /// 套用预设到目标页。
        /// Appearance：仅设置网格档位 / 显隐网格 / 背景（不动原子）。
        /// Scene：整页替换（深拷贝原子 + 网格 + 背景）。
        /// 背景与原子均 clone，避免预设实例被页面后续编辑篡改。
        /// </summary>
        public static void Apply(Preset p, Page page)
        {
            if (p == null || page == null) return;
            if (p.Kind == PresetKind.Scene)
            {
                page.Atoms = CloneAtoms(p.Atoms);
            }
            page.GridSize = p.GridSize;
            page.ShowGrid = p.Layers.Contains(LayerKind.Grid);
            page.Background = new BackgroundRef { Kind = p.Background?.Kind, Source = p.Background?.Source };
        }

        /// <summary>把当前页（原子 + 网格 + 背景）快照成一个 Scene 预设（深拷贝）。</summary>
        public static Preset CaptureFromPage(Page page, string name)
        {
            if (page == null) return null;
            return new Preset
            {
                Name = name,
                Kind = PresetKind.Scene,
                Layers = page.ShowGrid
                    ? new List<LayerKind> { LayerKind.Wallpaper, LayerKind.Grid }
                    : new List<LayerKind> { LayerKind.Wallpaper },
                GridSize = page.GridSize,
                Background = new BackgroundRef { Kind = page.Background?.Kind, Source = page.Background?.Source },
                Atoms = CloneAtoms(page.Atoms)
            };
        }

        /// <summary>经 DTO 往返深拷贝原子列表（复用 ConfigStore 既有多态序列化），保证预设与页面实例互不共享。</summary>
        private static List<Atom> CloneAtoms(IEnumerable<Atom> src)
        {
            if (src == null) return new List<Atom>();
            return src.Select(a => ConfigStore.AtomFromDto(ConfigStore.AtomToDto(a))).ToList();
        }

        public static string Export(string name)
        {
            var p = GetAny(name);
            return p == null ? "" : JsonSerializer.Serialize(p, Opt);
        }

        public static bool Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var p = JsonSerializer.Deserialize<Preset>(json, Opt);
                if (p == null) return false;
                _user[p.Name] = p;
                return true;
            }
            catch { return false; }
        }

        private static void BuildBuiltins()
        {
            _builtin["WallpaperOnly"] = new Preset
            {
                Name = "WallpaperOnly",
                Layers = new List<LayerKind> { LayerKind.Wallpaper },
                GridSize = 40,
                Background = new BackgroundRef { Kind = "solid", Source = "#FF1E1E1E" }
            };
            _builtin["GridWorkspace"] = new Preset
            {
                Name = "GridWorkspace",
                Layers = new List<LayerKind> { LayerKind.Wallpaper, LayerKind.Grid },
                GridSize = 40,
                Background = new BackgroundRef { Kind = "solid", Source = "#FF1E1E1E" }
            };
            _builtin["CanvasFree"] = new Preset
            {
                Name = "CanvasFree",
                Layers = new List<LayerKind> { LayerKind.Wallpaper, LayerKind.Canvas },
                GridSize = 20,
                Background = new BackgroundRef { Kind = "solid", Source = "#FF1E1E1E" }
            };
            // 配色模式（日间 / 夜间）：轻量「整体预设」，仅换背景色 + 网格，原子保持各自显式颜色
            _builtin["Day"] = new Preset
            {
                Name = "Day",
                Layers = new List<LayerKind> { LayerKind.Wallpaper, LayerKind.Grid },
                GridSize = 40,
                Background = new BackgroundRef { Kind = "solid", Source = "#FFF2F2F2" }
            };
            _builtin["Night"] = new Preset
            {
                Name = "Night",
                Layers = new List<LayerKind> { LayerKind.Wallpaper, LayerKind.Grid },
                GridSize = 40,
                Background = new BackgroundRef { Kind = "solid", Source = "#FF121212" }
            };
        }
    }
}
