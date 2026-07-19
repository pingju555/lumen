using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Lumen.Persistence;

namespace Lumen.I18n
{
    /// <summary>
    /// 轻量 i18n：外置 JSON 语言包（程序目录内置 + AppData 可覆盖，便于社区/用户翻译）。
    /// 用法：
    ///   代码  Loc.T("key", arg0...)
    ///   XAML  xmlns:loc="clr-namespace:Lumen.I18n"  然后  Content="{loc:Loc settings.title}"
    /// 缺失 key 先回退默认语言包，仍缺失则原样返回 key（便于一眼发现漏译）。
    /// 当前提供两版：简体中文(zh-CN) / 英式英语(en-GB)。
    /// </summary>
    public static class Loc
    {
        public const string LangZh = "zh-CN";
        public const string LangEn = "en-GB";

        private static readonly string AppDataLangDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Lumen", "lang");
        private static readonly string BuiltinLangDir =
            Path.Combine(AppContext.BaseDirectory, "lang");

        // code -> (key -> 文本)
        private static readonly Dictionary<string, Dictionary<string, string>> _bundles = new();

        private static string _cur = LangZh;
        private static readonly string _default = LangEn; // 回退基准

        public static event EventHandler LangChanged;

        /// <summary>下拉可选项（显示用名称随 Locale 本身固定，不翻译语言名）。</summary>
        public static IReadOnlyList<LangItem> Available { get; } = new[]
        {
            new LangItem { Code = LangZh, Name = "简体中文" },
            new LangItem { Code = LangEn, Name = "English (UK)" },
        };

        public static string Cur => _cur;

        /// <summary>启动早期调用：预载全部语言包，并按 meta 持久化或系统 culture 决定当前语言。</summary>
        public static void Init()
        {
            LoadAll();
            var saved = ConfigStore.ReadLang();
            _cur = string.IsNullOrEmpty(saved) ? DetectSystemLang() : saved;
            if (!_bundles.ContainsKey(_cur)) _cur = _default;
        }

        private static string DetectSystemLang()
        {
            var name = CultureInfo.CurrentUICulture.Name;
            return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? LangZh : LangEn;
        }

        private static void LoadAll()
        {
            foreach (var item in Available)
                _bundles[item.Code] = MergeBundle(item.Code);
        }

        private static Dictionary<string, string> MergeBundle(string code)
        {
            var merged = new Dictionary<string, string>();
            ApplyFile(merged, Path.Combine(BuiltinLangDir, code + ".json"));   // 1) 程序目录内置
            ApplyFile(merged, Path.Combine(AppDataLangDir, code + ".json"));  // 2) AppData 覆盖优先
            return merged;
        }

        private static void ApplyFile(Dictionary<string, string> dict, string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                using var d = JsonDocument.Parse(File.ReadAllText(path));
                if (d.RootElement.ValueKind != JsonValueKind.Object) return;
                foreach (var p in d.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String)
                        dict[p.Name] = p.Value.GetString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Loc load {path} failed: {ex.Message}");
            }
        }

        public static string T(string key) => Resolve(key);

        public static string T(string key, params object[] args)
        {
            var s = Resolve(key);
            if (args != null && args.Length > 0)
            {
                try { s = string.Format(s, args); }
                catch { /* 占位符不匹配时保留原文 */ }
            }
            return s;
        }

        private static string Resolve(string key)
        {
            if (key == null) return "";
            if (_bundles.TryGetValue(_cur, out var cur) && cur.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
            if (_bundles.TryGetValue(_default, out var def) && def.TryGetValue(key, out var dv) && !string.IsNullOrEmpty(dv))
                return dv;
            return key; // 回退失败：原样返回 key
        }

        /// <summary>切换语言并持久化，触发 LangChanged 供已打开窗口刷新自身文案。</summary>
        public static void Load(string code)
        {
            if (!_bundles.ContainsKey(code) || code == _cur) return;
            _cur = code;
            try { ConfigStore.SetLang(code); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Loc.SetLang failed: {ex.Message}"); }
            LangChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public sealed class LangItem
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }
}
