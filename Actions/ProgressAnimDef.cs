using System;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace Lumen.Actions
{
    /// <summary>
    /// 交互式进度驱动动画定义（Item 6）。
    /// 输入 progress (0~1) → 插值输出到原子属性。
    /// 触发源：Timer / Formula / Touch。
    /// 动作属性：Fade / TranslateX / TranslateY / Rotate / Scale / Slide / Zoom。
    /// </summary>
    public class ProgressAnimDef
    {
        public string Formula { get; set; } = "";        // 公式触发源（Trigger=Formula时）
        public double Duration { get; set; } = 1000;     // 播放时长 ms
        public string Easing { get; set; } = "linear";   // linear/easeIn/easeOut/easeInOut/bounce/overshoot

        // 各属性起止值（默认=不启用该属性）
        public double FadeFrom { get; set; } = -1; public double FadeTo { get; set; } = -1;
        public double TxFrom { get; set; } = 0; public double TxTo { get; set; } = 0;
        public double TyFrom { get; set; } = 0; public double TyTo { get; set; } = 0;
        public double RotFrom { get; set; } = 0; public double RotTo { get; set; } = 0;
        public double ScaleFrom { get; set; } = -1; public double ScaleTo { get; set; } = -1;
        public bool Slide { get; set; } = false;
        public bool Zoom { get; set; } = false;

        /// <summary>给定 progress 0~1，对目标 UIElement 应用当前插值。</summary>
        public void Apply(FrameworkElement target, double progress)
        {
            if (target == null) return;
            double p = Math.Clamp(progress, 0, 1);
            var (t, s, r) = EnsureTransforms(target);

            if (FadeFrom >= 0 && FadeTo >= 0)
                target.Opacity = Lerp(FadeFrom, FadeTo, p);

            if (TxFrom != 0 || TxTo != 0)
                t.X = Lerp(TxFrom, TxTo, p);
            if (TyFrom != 0 || TyTo != 0)
                t.Y = Lerp(TyFrom, TyTo, p);

            if (ScaleFrom >= 0 && ScaleTo >= 0)
            {
                s.ScaleX = Lerp(ScaleFrom, ScaleTo, p);
                s.ScaleY = Lerp(ScaleFrom, ScaleTo, p);
            }

            if (RotFrom != 0 || RotTo != 0)
                r.Angle = Lerp(RotFrom, RotTo, p);

            if (Slide && p < 0.5)
            {
                double slideProgress = p * 2; // 0→1 in first half
                t.X = Lerp(TxFrom, TxTo, slideProgress) - 64 * (1 - slideProgress);
                target.Opacity = Lerp(0, 1, slideProgress);
            }

            if (Zoom && p < 0.5)
            {
                double zoomProgress = p * 2;
                s.ScaleX = Lerp(ScaleFrom >= 0 ? ScaleFrom : 0.6, ScaleTo >= 0 ? ScaleTo : 1, zoomProgress);
                s.ScaleY = s.ScaleX;
                target.Opacity = Lerp(0, 1, zoomProgress);
            }
        }

        /// <summary>复位到初始态。</summary>
        public void Reset(FrameworkElement target)
        {
            if (target == null) return;
            var (t, s, r) = EnsureTransforms(target);
            target.Opacity = FadeFrom >= 0 ? FadeFrom : 1;
            t.X = TxFrom; t.Y = TyFrom;
            s.ScaleX = ScaleFrom >= 0 ? ScaleFrom : 1;
            s.ScaleY = ScaleFrom >= 0 ? ScaleFrom : 1;
            r.Angle = RotFrom;
        }

        /// <summary>获取缓动函数。</summary>
        public IEasingFunction GetEasing()
        {
            return Easing?.ToLowerInvariant() switch
            {
                "easein" => new CubicEase { EasingMode = EasingMode.EaseIn },
                "easeout" => new CubicEase { EasingMode = EasingMode.EaseOut },
                "easeinout" => new CubicEase { EasingMode = EasingMode.EaseInOut },
                "bounce" => new BounceEase { Bounces = 3, Bounciness = 2 },
                "overshoot" => new BackEase { Amplitude = 0.5 },
                _ => null // linear
            };
        }

        public double ApplyEasing(double t)
        {
            var ease = GetEasing();
            return ease != null ? ease.Ease(t) : t;
        }

        // ---- 序列化 ----

        public string Serialize() => JsonSerializer.Serialize(this, JsonOpts);
        public static ProgressAnimDef Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}" || json == "null") return new ProgressAnimDef();
            try { return JsonSerializer.Deserialize<ProgressAnimDef>(json, JsonOpts); }
            catch { return new ProgressAnimDef(); }
        }
        private static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true };

        // ---- 辅助 ----

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static (TranslateTransform, ScaleTransform, RotateTransform) EnsureTransforms(FrameworkElement host)
        {
            if (host.RenderTransform is TransformGroup tg && tg.Children.Count >= 3)
                return (tg.Children[0] as TranslateTransform, tg.Children[1] as ScaleTransform, tg.Children[2] as RotateTransform);

            var tt = new TranslateTransform(0, 0);
            var st = new ScaleTransform(1, 1);
            var rt = new RotateTransform(0);
            host.RenderTransform = new TransformGroup { Children = { tt, st, rt } };
            host.RenderTransformOrigin = new Point(0.5, 0.5);
            return (tt, st, rt);
        }
    }
}
