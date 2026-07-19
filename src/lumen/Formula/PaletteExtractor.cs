using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lumen.Formula
{
    /// <summary>
    /// 中位切分（Median Cut）调色板提取。纯 C# 实现，无第三方依赖。
    /// 输入图像（本地文件或 URL），量化出 dominant / vibrant / muted / light / dark 一组主色。
    /// 颜色统一用 uint(AARRGGBB) 表示，A 固定 0xFF。
    /// </summary>
    public static class PaletteExtractor
    {
        public struct Swatch
        {
            public uint Color;
            public int Population;
        }

        // 用具名 struct 携带像素与计数，避免 C# 元组元素名在容器取值时丢失的问题
        private struct Pix
        {
            public byte R, G, B;
            public int N;
        }

        /// <summary>
        /// 从图片文件/URL 提取调色板。返回 5 种风格色（键：dominant/vibrant/muted/light/dark）。
        /// 失败或无像素时返回空字典。
        /// </summary>
        public static Dictionary<string, uint> Extract(string imagePath, int targetColors = 16)
        {
            var bmp = LoadBitmap(imagePath);
            return Compute(bmp, targetColors);
        }

        /// <summary>
        /// 从原始图片字节（如 SMTC 媒体封面流）提取调色板。
        /// 失败或无像素时返回空字典。
        /// </summary>
        public static Dictionary<string, uint> Extract(byte[] data, int targetColors = 16)
        {
            var bmp = LoadBitmap(data);
            return Compute(bmp, targetColors);
        }

        private static Dictionary<string, uint> Compute(BitmapSource bmp, int targetColors)
        {
            if (bmp == null) return new Dictionary<string, uint>();
            var pixels = CollectPixels(bmp);
            if (pixels.Count == 0) return new Dictionary<string, uint>();
            var swatches = MedianCut(pixels, targetColors);
            return Classify(swatches);
        }

        // ---------- 解码 ----------

        /// <summary>按文件内容嗅探解码（与扩展名无关），用于无扩展名的缓存图片。返回 true 表示确为可解码位图。</summary>
        public static bool IsDecodable(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                return dec.Frames.Count > 0 && dec.Frames[0].PixelWidth > 0 && dec.Frames[0].PixelHeight > 0;
            }
            catch { return false; }
        }

        private static BitmapSource LoadBitmap(string path)
        {
            BitmapSource Load()
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var dec = BitmapDecoder.Create(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var frame = dec.Frames[0];
                if (frame == null || frame.PixelWidth == 0 || frame.PixelHeight == 0) return null;
                frame.Freeze();
                double scale = 100.0 / frame.PixelWidth;   // 降采样提速，调色板无需原分辨率
                var tb = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                tb.Freeze();
                return tb;
            }
            return RunOnUiIfNeeded(Load);
        }

        private static BitmapSource LoadBitmap(byte[] data)
        {
            BitmapSource Load()
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new MemoryStream(data);
                bmp.DecodePixelWidth = 100;          // 降采样提速，调色板无需原分辨率
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            return RunOnUiIfNeeded(Load);
        }

        // BitmapImage 需在 UI(STA) 线程创建；公式可能在后台线程评估，故必要时切回 UI 线程
        private static BitmapSource RunOnUiIfNeeded(Func<BitmapSource> load)
        {
            try
            {
                var app = Application.Current;
                if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
                    return (BitmapSource)app.Dispatcher.Invoke(load);
                return load();
            }
            catch { return null; }
        }

        private static List<Pix> CollectPixels(BitmapSource bmp)
        {
            int w = bmp.PixelWidth, h = bmp.PixelHeight;
            if (w <= 0 || h <= 0) return new List<Pix>();
            if (bmp.Format != PixelFormats.Bgra32)
                bmp = new FormatConvertedBitmap(bmp, PixelFormats.Bgra32, null, 0);
            int stride = w * 4;
            var buf = new byte[stride * h];
            bmp.CopyPixels(buf, stride, 0);
            // 去重统计，跳过透明像素
            var dict = new Dictionary<long, int>();
            for (int i = 0; i < buf.Length; i += 4)
            {
                byte b = buf[i], g = buf[i + 1], r = buf[i + 2], a = buf[i + 3];
                if (a < 128) continue;
                long key = ((long)r << 16) | ((long)g << 8) | b;
                dict.TryGetValue(key, out int c);
                dict[key] = c + 1;
            }
            var list = new List<Pix>(dict.Count);
            foreach (var kv in dict)
                list.Add(new Pix { R = (byte)(kv.Key >> 16), G = (byte)(kv.Key >> 8), B = (byte)kv.Key, N = kv.Value });
            return list;
        }

        // ---------- 中位切分 ----------

        private static List<Swatch> MedianCut(List<Pix> px, int count)
        {
            var boxes = new List<List<Pix>> { px };
            while (boxes.Count < count)
            {
                int bi = -1; double best = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    var b = boxes[i];
                    if (b.Count < 2) continue;
                    double m = Math.Max(Range(b, 0), Math.Max(Range(b, 1), Range(b, 2)));
                    if (m > best) { best = m; bi = i; }
                }
                if (bi < 0) break;
                var box = boxes[bi];
                double rR = Range(box, 0), rG = Range(box, 1), rB = Range(box, 2);
                int ch = (rR >= rG && rR >= rB) ? 0 : (rG >= rB ? 1 : 2);
                box.Sort((p, q) => Channel(p, ch).CompareTo(Channel(q, ch)));
                int total = 0; foreach (var p in box) total += p.N;
                int half = total / 2, acc = 0, mid = 0;
                for (int i = 0; i < box.Count; i++) { acc += box[i].N; if (acc >= half) { mid = i; break; } }
                mid = Math.Max(1, Math.Min(mid, box.Count - 1));
                var left = box.GetRange(0, mid);
                var right = box.GetRange(mid, box.Count - mid);
                boxes[bi] = left; boxes.Add(right);
            }
            var sw = new List<Swatch>(boxes.Count);
            foreach (var b in boxes) sw.Add(Average(b));
            return sw;
        }

        private static double Range(List<Pix> b, int ch)
        {
            int mn = 255, mx = 0;
            foreach (var p in b) { int v = Channel(p, ch); if (v < mn) mn = v; if (v > mx) mx = v; }
            return mx - mn;
        }
        private static int Channel(Pix p, int ch) => ch == 0 ? p.R : ch == 1 ? p.G : p.B;

        private static Swatch Average(List<Pix> b)
        {
            long r = 0, g = 0, bl = 0, n = 0;
            foreach (var p in b) { r += p.R * p.N; g += p.G * p.N; bl += p.B * p.N; n += p.N; }
            byte rr = (byte)(r / n), gg = (byte)(g / n), bb = (byte)(bl / n);
            return new Swatch
            {
                Color = 0xFF000000u | ((uint)rr << 16) | ((uint)gg << 8) | bb,
                Population = (int)n
            };
        }

        // ---------- 分类选色（对齐 Android Palette 风格） ----------

        private static Dictionary<string, uint> Classify(List<Swatch> swatches)
        {
            var res = new Dictionary<string, uint>();
            if (swatches.Count == 0) return res;
            Swatch dominant = swatches[0];
            foreach (var s in swatches) if (s.Population > dominant.Population) dominant = s;
            res["dominant"] = dominant.Color;

            Swatch bestVibrant = default, bestMuted = default, bestLight = default, bestDark = default;
            double scV = -1, scM = -1, scL = -1, scD = -1;
            foreach (var s in swatches)
            {
                ArgbToHsl(s.Color, out _, out double sat, out double lum, out _);
                double sv = s.Population * sat * (lum >= 0.35 && lum <= 0.65 ? 1 : 0.3);
                if (sv > scV) { scV = sv; bestVibrant = s; }
                double sm = s.Population * (1 - sat);
                if (sm > scM) { scM = sm; bestMuted = s; }
                double sl = s.Population * lum;
                if (sl > scL) { scL = sl; bestLight = s; }
                double sd = s.Population * (1 - lum);
                if (sd > scD) { scD = sd; bestDark = s; }
            }
            res["vibrant"] = bestVibrant.Color != 0 ? bestVibrant.Color : dominant.Color;
            res["muted"] = bestMuted.Color != 0 ? bestMuted.Color : dominant.Color;
            res["light"] = bestLight.Color != 0 ? bestLight.Color : ShiftLum(dominant.Color, +0.3);
            res["dark"] = bestDark.Color != 0 ? bestDark.Color : ShiftLum(dominant.Color, -0.3);
            return res;
        }

        // ---------- HSL 转换（自带，避免耦合 FunctionRegistry 私有方法） ----------

        private static void ArgbToHsl(uint argb, out double h, out double s, out double l, out double a)
        {
            double r = ((argb >> 16) & 0xFF) / 255.0;
            double g = ((argb >> 8) & 0xFF) / 255.0;
            double b = (argb & 0xFF) / 255.0;
            a = ((argb >> 24) & 0xFF) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            h = max == r ? (g - b) / d + (g < b ? 6 : 0)
              : max == g ? (b - r) / d + 2
              : (r - g) / d + 4;
            h *= 60;
        }

        private static uint HslToArgb(double h, double s, double l, double a)
        {
            h = (h % 360 + 360) % 360;
            s = Math.Max(0, Math.Min(1, s));
            l = Math.Max(0, Math.Min(1, l));
            a = Math.Max(0, Math.Min(1, a));
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; }
            else if (h < 120) { r = x; g = c; }
            else if (h < 180) { g = c; b = x; }
            else if (h < 240) { g = x; b = c; }
            else if (h < 300) { r = x; b = c; }
            else { r = c; b = x; }
            byte rr = (byte)Math.Round((r + m) * 255);
            byte gg = (byte)Math.Round((g + m) * 255);
            byte bb = (byte)Math.Round((b + m) * 255);
            byte aa = (byte)Math.Round(a * 255);
            return (uint)(aa << 24 | rr << 16 | gg << 8 | bb);
        }

        private static uint ShiftLum(uint argb, double dl)
        {
            ArgbToHsl(argb, out double h, out double s, out double l, out double a);
            return HslToArgb(h, s, Math.Max(0, Math.Min(1, l + dl)), a);
        }
    }
}
