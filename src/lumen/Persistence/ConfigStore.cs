using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using Lumen.Atoms;
using Lumen.Globals;
using Lumen.I18n;
using Lumen.Pages;
using Lumen.Presets;
using Lumen.Core;
using Lumen.Render;

namespace Lumen.Persistence
{
    // ---- DTO（schema v1：pages / userPresets）----
    public class RectDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
    public class BackgroundDto
    {
        public string Kind { get; set; }
        public string Source { get; set; }
    }
    public class AtomDto
    {
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public double Z { get; set; }
        public Dictionary<string, string> Props { get; set; } = new();
        public List<AtomDto> Children { get; set; }
    }
    public class GvDto
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public int Selected { get; set; }
    }
    public class PageDto
    {
        public string Name { get; set; }
        public double GridSize { get; set; } = 40;
        public bool ShowGrid { get; set; } = true;
        public BackgroundDto Background { get; set; }
        public List<GvDto> Gv { get; set; } = new();
        public List<AtomDto> Atoms { get; set; } = new();
    }
    public class PresetDto
    {
        public string Name { get; set; }
        public string Kind { get; set; } = "Appearance";
        public List<string> Layers { get; set; } = new();
        public double GridSize { get; set; } = 40;
        public BackgroundDto Background { get; set; }
        public List<AtomDto> Atoms { get; set; } = new();
    }
    public class Document
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "";
        public List<GvDto> Gv { get; set; } = new();
        public List<PresetDto> UserPresets { get; set; } = new();
        public List<PageDto> Pages { get; set; } = new();
    }

    /// <summary>
    /// 全量持久化（schema v1）：pages（含原子 + 网格 + 背景）+ userPresets + gv。
    /// 坏文件 / 旧 schema → 兜底默认 + 日志，不崩；旧 screens schema 经 Migrate 转单页。
    /// 详见 docs/project/phases/P2_原子全集与公式引擎/P2-06_持久化.md 与 P3-04
    /// </summary>
    public static class ConfigStore
    {
        // 数据根可配置：统一经 LumenPaths 解析（默认 %LocalAppData%/Lumen，指针文件可重定向）。
        private static string Dir => LumenPaths.DataDir;
        private static string FilePath => LumenPaths.ConfigFilePath;
        private static string ProfilesDir => LumenPaths.ProfilesDir;
        private static string MetaPath => LumenPaths.MetaPath;
        private static string ProfileFilePath(string name) => Path.Combine(ProfilesDir, Slug(name) + ".json");
        private static string Slug(string name)
        {
            var s = (name ?? "").Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder();
            foreach (var c in s) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            var r = sb.ToString().Trim();
            return string.IsNullOrEmpty(r) ? "profile" : r;
        }
        private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public class LoadResult
        {
            public GvStore Gv = new();
            public List<Page> Pages = new();
            public List<Preset> UserPresets = new();
        }

        /// <summary>保存当前配置档（多 profile：写入 profiles/&lt;slug&gt;.json 并登记为激活档）。</summary>
        public static void Save(GvStore gv, IReadOnlyList<Page> pages, string profileName)
        {
            try
            {
                var doc = BuildDocument(gv, pages, profileName);
                Directory.CreateDirectory(ProfilesDir);
                File.WriteAllText(ProfileFilePath(profileName), JsonSerializer.Serialize(doc, Opt));
                SetActive(profileName);
            }
            catch (Exception ex) { Debug.WriteLine($"Config save failed: {ex.Message}"); }
        }

        private static Document BuildDocument(GvStore gv, IReadOnlyList<Page> pages, string name)
        {
            var doc = new Document { Name = name ?? "" };
            foreach (var p in PresetLibrary.User) doc.UserPresets.Add(ToDto(p));
            foreach (var pg in pages) doc.Pages.Add(ToDto(gv, pg));
            // 全局变量（跨页共享）在文档级持久化
            foreach (var kv in gv.All)
                doc.Gv.Add(new GvDto
                {
                    Name = kv.Key,
                    Type = GvTypeToString(kv.Value.Type),
                    Value = GvValueToString(kv.Value),
                    Selected = kv.Value.SelectedIndex
                });
            return doc;
        }

        public static LoadResult Load()
        {
            var res = new LoadResult();
            if (!File.Exists(FilePath)) return res;
            try { return ParseDoc(File.ReadAllText(FilePath)); }
            catch (Exception ex) { Debug.WriteLine($"Config load failed: {ex.Message}"); }
            return res;
        }

        // ---------- 多 profile 管理 ----------
        public static List<string> ListProfiles()
        {
            var list = new List<string>();
            if (!Directory.Exists(ProfilesDir)) return list;
            foreach (var f in Directory.GetFiles(ProfilesDir, "*.json"))
            {
                try
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(f));
                    if (d.RootElement.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        list.Add(n.GetString());
                    else
                        list.Add(Path.GetFileNameWithoutExtension(f));
                }
                catch { }
            }
            return list;
        }

        public static bool ProfileExists(string name) => File.Exists(ProfileFilePath(name));

        /// <summary>载入指定名称的配置档；文件缺失返回 null。</summary>
        public static LoadResult Load(string name)
        {
            var fp = ProfileFilePath(name);
            if (!File.Exists(fp)) return null;
            try { return ParseDoc(File.ReadAllText(fp)); }
            catch (Exception ex) { Debug.WriteLine($"Profile load failed: {ex.Message}"); }
            return null;
        }

        /// <summary>载入激活的配置档：首次运行从旧 config.json 迁移或播种默认档；返回 (结果, 激活档名)。</summary>
        public static (LoadResult result, string active) LoadActive()
        {
            // 每次启动都刷新内置手册文件（覆盖旧版），避免「新手册未加载」。
            // 仅写文件，不切换激活档；若用户当前激活的是手册，则自然读到最新版本。
            RefreshBuiltinManual();

            if (!Directory.Exists(ProfilesDir) || Directory.GetFiles(ProfilesDir, "*.json").Length == 0)
            {
                if (File.Exists(FilePath)) // 迁移旧单文件配置（一次性：迁移成功后删除源文件，避免反复迁移）
                {
                    var legacy = ParseDoc(File.ReadAllText(FilePath));
                    Directory.CreateDirectory(ProfilesDir);
                    var doc = BuildDocument(legacy.Gv, legacy.Pages, "默认");
                    doc.UserPresets = legacy.UserPresets.ConvertAll(ToDto);
                    File.WriteAllText(ProfileFilePath("默认"), JsonSerializer.Serialize(doc, Opt));
                    try { File.Delete(FilePath); } catch { /* 旧文件无写权限时静默跳过 */ }
                    var manual2 = InstallBuiltinManual();
                    return (Load(manual2) ?? legacy, manual2 ?? "默认");
                }
                // 全新：播种默认档，并自动内置「使用手册」作为首发激活档（无需手动载入）
                Directory.CreateDirectory(ProfilesDir);
                var gv = new GvStore();
                var pages = new List<Page> { new Page { Name = "主页", GridSize = 40, ShowGrid = true } };
                var d2 = BuildDocument(gv, pages, "默认");
                File.WriteAllText(ProfileFilePath("默认"), JsonSerializer.Serialize(d2, Opt));
                var manual = InstallBuiltinManual();
                return (Load(manual) ?? new LoadResult(), manual ?? "默认");
            }

            string active = ReadActive();
            if (string.IsNullOrEmpty(active) || !ProfileExists(active))
            {
                active = ListProfiles().FirstOrDefault();
                if (string.IsNullOrEmpty(active))
                {
                    var gv = new GvStore();
                    var pages = new List<Page> { new Page { Name = "主页", GridSize = 40, ShowGrid = true } };
                    var d3 = BuildDocument(gv, pages, "默认");
                    File.WriteAllText(ProfileFilePath("默认"), JsonSerializer.Serialize(d3, Opt));
                    active = "默认";
                }
                SetActive(active);
            }
            return (Load(active) ?? new LoadResult(), active);
        }

        public static void CreateProfile(string name)
        {
            Directory.CreateDirectory(ProfilesDir);
            var gv = new GvStore();
            var pages = new List<Page> { new Page { Name = "主页", GridSize = 40, ShowGrid = true } };
            var doc = BuildDocument(gv, pages, name);
            File.WriteAllText(ProfileFilePath(name), JsonSerializer.Serialize(doc, Opt));
            SetActive(name);
        }

        public static void DeleteProfile(string name)
        {
            var fp = ProfileFilePath(name);
            if (File.Exists(fp)) File.Delete(fp);
        }

        public static void RenameProfile(string oldName, string newName)
        {
            var fpOld = ProfileFilePath(oldName);
            if (!File.Exists(fpOld)) return;
            // 读取整份文档、改写 name 字段后写入新文件（保留全部页面/变量/预设）
            var json = File.ReadAllText(fpOld);
            var doc = JsonSerializer.Deserialize<Document>(json, Opt) ?? new Document();
            doc.Name = newName;
            File.WriteAllText(ProfileFilePath(newName), JsonSerializer.Serialize(doc, Opt));
            File.Delete(fpOld);
            if (ReadActive() == oldName) SetActive(newName);
        }

        /// <summary>导出配置档为独立文件（含页面/变量/用户预设）。</summary>
        public static void ExportProfile(string name, string path)
        {
            var fp = ProfileFilePath(name);
            if (File.Exists(fp)) File.Copy(fp, path, true);
        }

        /// <summary>从文件导入为新的配置档（自动命名避免冲突）。返回新建的配置档名。</summary>
        public static string ImportProfileFromFile(string path, string desiredName)
        {
            var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(path), Opt);
            if (doc == null) throw new Exception("文件格式无效");
            string name = desiredName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = doc.Name;
                if (string.IsNullOrWhiteSpace(name)) name = "导入的配置档";
            }
            string baseName = name; int i = 1;
            while (ProfileExists(name)) name = $"{baseName} ({i++})";
            doc.Name = name;
            File.WriteAllText(ProfileFilePath(name), JsonSerializer.Serialize(doc, Opt));
            return name;
        }

        /// <summary>
        /// 安装内置「使用手册」配置档：读取嵌入资源（按当前语言选 zh-CN / en-GB 版），
        /// 写入 profiles/&lt;文档名&gt;.json（每次覆盖以保持最新），并登记为激活档。
        /// 返回配置档名（失败返回 null）。
        /// </summary>
        /// <param name="activate">true=安装后设为激活档（首次运行/迁移）；false=仅刷新手册文件，不切换激活档。</param>
        public static string InstallBuiltinManual(bool activate = true)
        {
            try
            {
                var asm = typeof(ConfigStore).Assembly;
                // 按当前 UI 语言选对应手册版本（决策 ③-a：InstallBuiltinManual 按 lang 装对应语言版手册）
                string resName = Loc.Cur.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                    ? "Lumen.Resources.help_manual.en-GB.json"
                    : "Lumen.Resources.help_manual.json";
                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    Debug.WriteLine($"InstallBuiltinManual: resource not found: {resName}");
                    return null;
                }
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                // 解析出档名（默认「使用手册」）
                string name = "使用手册";
                try
                {
                    var doc = JsonSerializer.Deserialize<Document>(json, Opt);
                    if (doc != null && !string.IsNullOrWhiteSpace(doc.Name)) name = doc.Name;
                }
                catch { }
                Directory.CreateDirectory(ProfilesDir);
                File.WriteAllText(ProfileFilePath(name), json);
                if (activate) SetActive(name);
                return name;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InstallBuiltinManual failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>仅用嵌入资源刷新内置手册文件（覆盖旧版），不切换激活档。每次启动调用以保证最新。</summary>
        public static void RefreshBuiltinManual() => InstallBuiltinManual(activate: false);

        /// <summary>当前激活档是否为内置使用手册（任一语言版：使用手册 / User Manual）。</summary>
        public static bool IsActiveProfileManual()
        {
            var active = ReadActive();
            if (string.IsNullOrWhiteSpace(active)) return false;
            return BuiltinManualDocNames().Contains(active);
        }

        /// <summary>返回两语言手册的文档名集合（用于判定激活档是否为手册，不含 IO 之外的副作用）。</summary>
        private static HashSet<string> BuiltinManualDocNames()
        {
            var set = new HashSet<string>();
            foreach (var res in new[] { "Lumen.Resources.help_manual.json", "Lumen.Resources.help_manual.en-GB.json" })
            {
                try
                {
                    var asm = typeof(ConfigStore).Assembly;
                    using var s = asm.GetManifestResourceStream(res);
                    if (s == null) continue;
                    using var r = new StreamReader(s);
                    var doc = JsonSerializer.Deserialize<Document>(r.ReadToEnd(), Opt);
                    if (doc != null && !string.IsNullOrWhiteSpace(doc.Name)) set.Add(doc.Name);
                }
                catch { }
            }
            return set;
        }

        internal static void SetActive(string name)
        {
            // 切换激活档时保留已持久化的语言选择
            var (_, lang) = ReadMeta();
            WriteMeta(name, lang);
        }

        /// <summary>持久化当前 UI 语言（与激活档解耦，语言是全局设置）。</summary>
        internal static void SetLang(string code) => WriteMeta(ReadActive(), code);

        internal static string ReadLang() => ReadMeta().lang;

        private static (string active, string lang) ReadMeta()
        {
            try
            {
                if (File.Exists(MetaPath))
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(MetaPath));
                    var active = d.RootElement.TryGetProperty("active", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : "";
                    var lang = d.RootElement.TryGetProperty("lang", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : "";
                    return (active, lang);
                }
            }
            catch { }
            return ("", "");
        }

        private static void WriteMeta(string active, string lang)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(MetaPath, JsonSerializer.Serialize(new { active, lang }));
            }
            catch (Exception ex) { Debug.WriteLine($"WriteMeta failed: {ex.Message}"); }
        }

        private static string ReadActive()
        {
            try
            {
                if (File.Exists(MetaPath))
                {
                    using var d = JsonDocument.Parse(File.ReadAllText(MetaPath));
                    if (d.RootElement.TryGetProperty("active", out var a) && a.ValueKind == JsonValueKind.String)
                        return a.GetString();
                }
            }
            catch { }
            return "";
        }

        private static LoadResult ParseDoc(string json)
        {
            var res = new LoadResult();

            // 新 schema：强类型反序列化。Opt.PropertyNameCaseInsensitive=true，
            // 因此 Save 产出的 PascalCase 键（Pages/Atoms/Type…）与 camelCase 均可读回。
            // （历史坑：旧版此处用 JsonDocument 手动读小写键 "pages"，与序列化的 PascalCase
            //  不匹配→Load 永远返回空→每次启动/切档都 SeedDefaultProject 兜底，profile 内容永远丢失。）
            Document document = null;
            try { document = JsonSerializer.Deserialize<Document>(json, Opt); }
            catch (Exception ex) { Debug.WriteLine($"ParseDoc deserialize failed: {ex.Message}"); }

            if (document != null && document.Pages != null && document.Pages.Count > 0)
            {
                foreach (var pd in document.Pages) res.Pages.Add(PageFromDto(pd));
                if (document.UserPresets != null)
                    foreach (var ud in document.UserPresets) res.UserPresets.Add(PresetFromDto(ud));
                if (document.Gv != null)
                    foreach (var gd in document.Gv)
                        if (!string.IsNullOrEmpty(gd.Name))
                            res.Gv.Set(gd.Name, MakeTyped(gd.Type, gd.Value, gd.Selected));
                return res;
            }

            // 旧 schema：screens → 迁移为单页（手动读小写键，仅老版本文件用）
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("screens", out var screensArr))
            {
                var page = new Page { Name = "主页", GridSize = 40, ShowGrid = false };
                foreach (var screen in screensArr.EnumerateArray())
                {
                    if (!screen.TryGetProperty("layers", out var layersArr)) continue;
                    foreach (var layer in layersArr.EnumerateArray())
                    {
                        if (layer.TryGetProperty("gv", out var gvArr))
                            foreach (var g in gvArr.EnumerateArray())
                            {
                                string gvVal = "";
                                if (g.TryGetProperty("value", out var gvV)) gvVal = gvV.GetString() ?? "";
                                res.Gv.Set(g.GetProperty("name").GetString(),
                                    MakeTyped(g.GetProperty("type").GetString(), gvVal));
                            }
                        if (layer.TryGetProperty("atoms", out var atomsArr))
                            foreach (var a in atomsArr.EnumerateArray()) page.Atoms.Add(AtomFromDto(AtomDtoFromJson(a)));
                    }
                }
                res.Pages.Add(page);
                return res;
            }
            return res;
        }

        // ---- 原子 DTO（公开复用：Preset 序列化）----
        public static AtomDto AtomToDto(Atom a)
        {
            var dto = new AtomDto
            {
                Type = a.Type,
                X = a.Bounds.X, Y = a.Bounds.Y,
                W = a.Bounds.Width, H = a.Bounds.Height, Z = 0
            };
            foreach (var kv in a.GetProps())
                dto.Props[kv.Key] = PropertyValue.Serialize(kv.Value);
            if (a is ContainerAtom c && c.Children.Count > 0)
                dto.Children = new List<AtomDto>(c.Children.ConvertAll(AtomToDto));
            return dto;
        }

        public static Atom AtomFromDto(AtomDto d)
        {
            // 兼容旧配置：旧三合一 "Container" 按 layout 映射到独立组原子
            var type = d.Type ?? "Text";
            if (type == "Container")
            {
                string layout = "Stack";
                if (d.Props != null && d.Props.TryGetValue("layout", out var lv))
                    layout = (lv ?? "Stack").ToLowerInvariant();
                type = layout == "overlap" ? "Overlap" : layout == "series" ? "Series" : "Stack";
            }
            var a = AtomRegistry.Create(type);
            double w = d.W <= 0 ? 100 : d.W, h = d.H <= 0 ? 40 : d.H;
            a.Bounds = new Rect(d.X, d.Y, w, h);
            if (d.Props != null)
            {
                var dict = new Dictionary<string, PropertyValue>();
                foreach (var p in d.Props) dict[p.Key] = PropertyValue.Parse(p.Value ?? "");
                a.SetProps(dict);
            }
            if (a is ContainerAtom c && d.Children != null)
                foreach (var ch in d.Children) c.Children.Add(AtomFromDto(ch));
            return a;
        }

        // ---- DTO → 领域对象（强类型反序列化路径复用）----
        private static Page PageFromDto(PageDto d)
        {
            var p = new Page
            {
                Name = string.IsNullOrEmpty(d.Name) ? "页面" : d.Name,
                GridSize = d.GridSize > 0 ? d.GridSize : 40,
                ShowGrid = d.ShowGrid,
                Background = BgFromDto(d.Background)
            };
            if (d.Atoms != null)
                foreach (var a in d.Atoms) p.Atoms.Add(AtomFromDto(a));
            return p;
        }

        private static Preset PresetFromDto(PresetDto d)
        {
            var p = new Preset
            {
                Name = string.IsNullOrEmpty(d.Name) ? "Preset" : d.Name,
                Kind = Enum.TryParse<PresetKind>(d.Kind, true, out var pk) ? pk : PresetKind.Appearance,
                GridSize = d.GridSize > 0 ? d.GridSize : 40,
                Background = BgFromDto(d.Background)
            };
            if (d.Layers != null)
                foreach (var l in d.Layers)
                    if (Enum.TryParse<LayerKind>(l, true, out var lk)) p.Layers.Add(lk);
            if (d.Atoms != null)
                foreach (var a in d.Atoms) p.Atoms.Add(AtomFromDto(a));
            return p;
        }

        private static BackgroundRef BgFromDto(BackgroundDto b)
            => new BackgroundRef { Kind = b?.Kind, Source = b?.Source };

        private static AtomDto AtomDtoFromJson(JsonElement e)
        {
            var d = new AtomDto
            {
                Type = e.GetProperty("type").GetString(),
                X = Num(e, "x"), Y = Num(e, "y"), W = Num(e, "w"), H = Num(e, "h")
            };
            if (e.TryGetProperty("props", out var propsEl))
                foreach (var p in propsEl.EnumerateObject())
                    d.Props[p.Name] = p.Value.GetString() ?? "";
            if (e.TryGetProperty("children", out var ch))
            {
                d.Children = new List<AtomDto>();
                foreach (var c in ch.EnumerateArray()) d.Children.Add(AtomDtoFromJson(c));
            }
            return d;
        }

        // ---- Page ----
        private static PageDto ToDto(GvStore gv, Page p) => new()
        {
            Name = p.Name, GridSize = p.GridSize, ShowGrid = p.ShowGrid,
            Background = ToBg(p.Background),
            Atoms = new List<AtomDto>(p.Atoms.ConvertAll(AtomToDto))
        };
        private static Page FromPageDto(JsonElement e)
        {
            var p = new Page
            {
                Name = e.TryGetProperty("name", out var n) ? n.GetString() : "页面",
                GridSize = e.TryGetProperty("gridSize", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetDouble() : 40,
                ShowGrid = e.TryGetProperty("showGrid", out var s) ? s.GetBoolean() : true,
                Background = e.TryGetProperty("background", out var bg) ? FromBg(bg) : new BackgroundRef()
            };
            if (e.TryGetProperty("atoms", out var atomsArr))
                foreach (var a in atomsArr.EnumerateArray()) p.Atoms.Add(AtomFromDto(AtomDtoFromJson(a)));
            return p;
        }

        // ---- Preset ----
        private static PresetDto ToDto(Preset p) => new()
        {
            Name = p.Name,
            Kind = p.Kind.ToString(),
            Layers = new List<string>(p.Layers.ConvertAll(l => l.ToString())),
            GridSize = p.GridSize,
            Background = ToBg(p.Background),
            Atoms = new List<AtomDto>(p.Atoms.ConvertAll(AtomToDto))
        };
        private static Preset FromPresetDto(JsonElement e)
        {
            var p = new Preset
            {
                Name = e.TryGetProperty("name", out var n) ? n.GetString() : "Preset",
                Kind = e.TryGetProperty("kind", out var k) && Enum.TryParse<PresetKind>(k.GetString(), true, out var pk)
                    ? pk : PresetKind.Appearance,
                GridSize = e.TryGetProperty("gridSize", out var g) && g.ValueKind == JsonValueKind.Number ? g.GetDouble() : 40,
                Background = e.TryGetProperty("background", out var bg) ? FromBg(bg) : new BackgroundRef()
            };
            if (e.TryGetProperty("layers", out var lArr))
                foreach (var l in lArr.EnumerateArray())
                    if (Enum.TryParse<LayerKind>(l.GetString(), true, out var lk)) p.Layers.Add(lk);
            if (e.TryGetProperty("atoms", out var atomsArr) && atomsArr.ValueKind == JsonValueKind.Array)
                foreach (var a in atomsArr.EnumerateArray()) p.Atoms.Add(AtomFromDto(AtomDtoFromJson(a)));
            return p;
        }

        // ---- 小工具 ----
        private static RectDto ToRectDto(Rect r) => new() { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
        private static Rect FromRectDto(JsonElement e)
        {
            double X = e.TryGetProperty("x", out var x) ? x.GetDouble() : 0;
            double Y = e.TryGetProperty("y", out var y) ? y.GetDouble() : 0;
            double W = e.TryGetProperty("width", out var w) ? w.GetDouble() : 1;
            double H = e.TryGetProperty("height", out var h) ? h.GetDouble() : 1;
            return new Rect(X, Y, W, H);
        }
        private static BackgroundDto ToBg(BackgroundRef b) => new() { Kind = b?.Kind, Source = b?.Source };
        private static BackgroundRef FromBg(JsonElement e)
        {
            var b = new BackgroundRef();
            if (e.TryGetProperty("kind", out var k)) b.Kind = k.GetString();
            if (e.TryGetProperty("source", out var s)) b.Source = s.GetString();
            return b;
        }

        private static double Num(JsonElement e, string k)
            => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

        private static void ReadGv(JsonElement root, LoadResult res)
        {
            if (!root.TryGetProperty("gv", out var gvArr) || gvArr.ValueKind != JsonValueKind.Array) return;
            foreach (var g in gvArr.EnumerateArray())
            {
                if (!g.TryGetProperty("name", out var n)) continue;
                string name = n.GetString();
                string type = g.TryGetProperty("type", out var t) ? t.GetString() : "text";
                string val = g.TryGetProperty("value", out var v) ? (v.GetString() ?? "") : "";
                int sel = g.TryGetProperty("selected", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0;
                res.Gv.Set(name, MakeTyped(type, val, sel));
            }
        }

        private static TypedValue MakeTyped(string type, string val, int selected = 0)
        {
            var t = type?.ToLower() switch
            {
                "number" => GvType.Number,
                "color" => GvType.Color,
                "font" => GvType.Font,
                "list" => GvType.List,
                _ => GvType.Text
            };
            object raw;
            if (t == GvType.Number)
                raw = (object)(double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0);
            else if (t == GvType.Color && !string.IsNullOrEmpty(val) && val.StartsWith("#"))
                raw = ParseColorRaw(val);
            else if (t == GvType.List)
                raw = val ?? "";
            else
                raw = val;
            return new TypedValue { Type = t, Raw = raw, SelectedIndex = t == GvType.List ? Math.Max(0, selected) : 0 };
        }

        private static string GvTypeToString(GvType t) => t switch
        {
            GvType.Number => "number",
            GvType.Color => "color",
            GvType.Font => "font",
            GvType.List => "list",
            _ => "text"
        };

        private static string GvValueToString(TypedValue tv) => tv.Type switch
        {
            GvType.Number => tv.Raw is double d ? d.ToString(CultureInfo.InvariantCulture) : "0",
            GvType.Color => "#" + ((uint)(tv.Raw ?? 0u)).ToString("X8"),
            GvType.List => (tv.Raw as string) ?? "",
            _ => (tv.Raw as string) ?? ""
        };

        private static uint ParseColorRaw(string s)
        {
            var hex = s.TrimStart('#');
            if (hex.Length == 6) return 0xFF000000 | uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            if (hex.Length == 8) return uint.Parse(hex, System.Globalization.NumberStyles.HexNumber);
            return 0xFF000000;
        }
    }
}
