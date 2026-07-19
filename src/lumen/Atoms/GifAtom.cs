using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Lumen.Formula;
using Lumen.I18n;

namespace Lumen.Atoms
{
    /// <summary>
    /// GIF 动态图片原子：位图源 + 适应/拉伸/圆角 + 动画播放（按帧延迟/处置方式合成）。
    /// 解码用 WPF 内置 GifBitmapDecoder，预合成各帧为冻结 BitmapSource，DispatcherTimer 按倍速换帧。
    /// 处置方式（RestoreBackground / RestorePrevious）正确处理，透明/局部帧 GIF 不拖影。
    /// </summary>
    public class GifAtom : Atom
    {
        public PropertyValue SourceProp = new StaticValue("");
        public PropertyValue StretchProp = new StaticValue("Uniform");
        public PropertyValue RadiusProp = new StaticValue("0");
        public PropertyValue BgProp = new StaticValue("#00000000");
        public PropertyValue SpeedProp = new StaticValue("1");   // 倍速：1=原速，2=两倍快，0.5=半速

        private Image _img;
        private Border _border;
        private TextBlock _placeholder;
        private string _lastSrc;
        private DispatcherTimer _timer;
        private List<BitmapSource> _frames = new();
        private List<int> _delays = new();   // 每帧延迟(ms)
        private int _frameIndex;

        public GifAtom() : base("Gif") { Bounds = new Rect(360, 440, 200, 150); }

        public override UIElement Render()
        {
            // 重建时停掉旧 timer，避免泄漏（旧 Image 被移出视觉树后 IsLoaded=false 也会自停）
            StopTimer();

            _img = new Image { IsHitTestVisible = true };
            _placeholder = new TextBlock
            {
                Text = Loc.T("atom.gif.empty"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.Gray),
                IsHitTestVisible = false
            };
            var imgGrid = new Grid();
            imgGrid.Children.Add(_img);
            imgGrid.Children.Add(_placeholder);
            _border = new Border
            {
                Child = imgGrid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            // 移除时自停 timer
            _img.Unloaded += (s, e) => StopTimer();
            ApplyDynamic();
            _root = MakeDraggable(_border);
            return _root;
        }

        public override void Update() { base.Update(); ApplyDynamic(); }

        private void ApplyDynamic()
        {
            if (_img == null) return;
            var src = Ctx != null ? Txt(SourceProp, Ctx) : SourceProp.Materialize();
            bool hasGif = !string.IsNullOrWhiteSpace(src) && File.Exists(src) &&
                          src.Trim().EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
            if (src != _lastSrc)
            {
                _lastSrc = src;
                if (hasGif)
                {
                    try { LoadGif(src); }
                    catch { _frames.Clear(); _delays.Clear(); }
                    if (_frames.Count > 0)
                    {
                        _placeholder.Visibility = Visibility.Collapsed;
                        StartAnimation();
                    }
                    else
                    {
                        // 解码失败 / 非 GIF：退化为静态位图
                        try { _img.Source = LoadBitmap(src); } catch { _img.Source = null; }
                        _placeholder.Visibility = _img.Source == null ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
                else
                {
                    StopTimer();
                    _img.Source = null;
                    _placeholder.Visibility = Visibility.Visible;
                }
            }
            else if (hasGif && _timer != null && _timer.IsEnabled)
            {
                // 源未变但倍速可能变：重排下一帧间隔
                ApplySpeed();
            }

            _border.Background = (hasGif && _img.Source != null)
                ? ResolveBrush(BgProp, Ctx, Brushes.Transparent)
                : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            if (Enum.TryParse<Stretch>(Txt(StretchProp, Ctx), true, out var st))
                _img.Stretch = st;
            if (double.TryParse(Txt(RadiusProp, Ctx), out var r) && r > 0) _border.CornerRadius = new CornerRadius(r);
            else _border.CornerRadius = new CornerRadius(0);
            ApplyCommon();
        }

        /// <summary>解码 GIF：提取每帧 + 延迟 + 处置方式，预合成成冻结 BitmapSource 列表。</summary>
        private void LoadGif(string path)
        {
            _frames.Clear(); _delays.Clear();
            var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return;
            if (decoder is not GifBitmapDecoder)
            {
                // 非 GIF（理论上不会进来，因调用前已校验 .gif 后缀）：当作单帧静态图
                var fb = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
                var frozen = new WriteableBitmap(fb); frozen.Freeze();
                _frames.Add(frozen); _delays.Add(0);
                return;
            }

            int w = decoder.Frames[0].PixelWidth;
            int h = decoder.Frames[0].PixelHeight;
            // 逻辑屏尺寸（部分 GIF 帧小于屏，需以此为准做合成）
            if (decoder.Frames[0].Metadata is BitmapMetadata lm)
            {
                if (lm.GetQuery("/logscrdesc/Width") is ushort lw && lw > 0) w = lw;
                if (lm.GetQuery("/logscrdesc/Height") is ushort lh && lh > 0) h = lh;
            }

            var accum = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            ClearRect(accum, 0, 0, w, h);
            byte[] restorePrev = null;

            int prevDisposal = 1;
            int prevLeft = 0, prevTop = 0, prevW = w, prevH = h;

            for (int i = 0; i < decoder.Frames.Count; i++)
            {
                var frame = decoder.Frames[i];
                int left = 0, top = 0, delay = 100; int disposal = 1;
                if (frame.Metadata is BitmapMetadata md)
                {
                    if (md.GetQuery("/imgdesc/Left") is ushort l) left = l;
                    if (md.GetQuery("/imgdesc/Top") is ushort t) top = t;
                    if (md.GetQuery("/grctlext/Delay") is ushort d) delay = d;   // 1/100 秒
                    if (md.GetQuery("/grctlext/Disposal") is byte dp) disposal = dp;
                }
                if (delay <= 0) delay = 10;
                int fw = frame.PixelWidth, fh = frame.PixelHeight;

                // 应用上一帧的处置方式（在画当前帧之前）
                if (i > 0)
                {
                    if (prevDisposal == 2) ClearRect(accum, prevLeft, prevTop, prevW, prevH);
                    else if (prevDisposal == 3 && restorePrev != null) Restore(accum, restorePrev);
                }

                // 本帧处置方式为「恢复到上一帧前」时，先快照当前画布（画本帧之前的状态）
                if (disposal == 3) restorePrev = CopyPixels(accum, w, h);

                DrawFrame(accum, frame, left, top);

                var disp = new WriteableBitmap(accum); disp.Freeze();
                _frames.Add(disp);
                _delays.Add(delay * 10);   // → ms

                prevDisposal = disposal; prevLeft = left; prevTop = top; prevW = fw; prevH = fh;
            }
        }

        private void DrawFrame(WriteableBitmap accum, BitmapSource frame, int left, int top)
        {
            var bmp = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            int fw = bmp.PixelWidth, fh = bmp.PixelHeight;
            var src = new byte[fw * fh * 4];
            bmp.CopyPixels(src, fw * 4, 0);
            int aw = accum.PixelWidth, ah = accum.PixelHeight;
            var dst = new byte[aw * ah * 4];
            accum.CopyPixels(dst, aw * 4, 0);
            for (int y = 0; y < fh; y++)
            {
                int dy = y + top; if (dy < 0 || dy >= ah) continue;
                for (int x = 0; x < fw; x++)
                {
                    int dx = x + left; if (dx < 0 || dx >= aw) continue;
                    int si = (y * fw + x) * 4;
                    byte sa = src[si + 3];
                    if (sa == 0) continue;
                    int di = (dy * aw + dx) * 4;
                    if (sa == 255)
                    {
                        dst[di] = src[si]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si + 2]; dst[di + 3] = 255;
                    }
                    else
                    {
                        float a = sa / 255f, ia = 1 - a;
                        dst[di] = (byte)(src[si] * a + dst[di] * ia);
                        dst[di + 1] = (byte)(src[si + 1] * a + dst[di + 1] * ia);
                        dst[di + 2] = (byte)(src[si + 2] * a + dst[di + 2] * ia);
                        dst[di + 3] = (byte)(sa + dst[di + 3] * ia);
                    }
                }
            }
            accum.WritePixels(new Int32Rect(0, 0, aw, ah), dst, aw * 4, 0);
        }

        private static void ClearRect(WriteableBitmap accum, int left, int top, int w, int h)
        {
            int aw = accum.PixelWidth, ah = accum.PixelHeight;
            var dst = new byte[aw * ah * 4];
            accum.CopyPixels(dst, aw * 4, 0);
            for (int y = 0; y < h; y++)
            {
                int dy = y + top; if (dy < 0 || dy >= ah) continue;
                for (int x = 0; x < w; x++)
                {
                    int dx = x + left; if (dx < 0 || dx >= aw) continue;
                    int di = (dy * aw + dx) * 4;
                    dst[di] = dst[di + 1] = dst[di + 2] = dst[di + 3] = 0;
                }
            }
            accum.WritePixels(new Int32Rect(0, 0, aw, ah), dst, aw * 4, 0);
        }

        private static void Restore(WriteableBitmap accum, byte[] snap)
        {
            int aw = accum.PixelWidth, ah = accum.PixelHeight;
            accum.WritePixels(new Int32Rect(0, 0, aw, ah), snap, aw * 4, 0);
        }

        private static byte[] CopyPixels(WriteableBitmap bmp, int w, int h)
        {
            var buf = new byte[w * h * 4];
            bmp.CopyPixels(buf, w * 4, 0);
            return buf;
        }

        // ---------- 播放 ----------
        private void StartAnimation()
        {
            StopTimer();
            if (_frames.Count == 0) return;
            _frameIndex = 0;
            _img.Source = _frames[0];
            if (_frames.Count == 1) return;   // 单帧：无需计时
            _timer = new DispatcherTimer(DispatcherPriority.Render);
            _timer.Tick += OnTick;
            ScheduleNext();
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_img == null || !_img.IsLoaded) { StopTimer(); return; }
            if (_frames.Count == 0) { StopTimer(); return; }
            _frameIndex = (_frameIndex + 1) % _frames.Count;
            _img.Source = _frames[_frameIndex];
            ScheduleNext();
        }

        private void ScheduleNext()
        {
            if (_timer == null || _frames.Count == 0) return;
            int d = _frameIndex < _delays.Count ? _delays[_frameIndex] : 100;
            double speed = 1;
            if (double.TryParse(Txt(SpeedProp, Ctx), out var sp) && sp > 0) speed = sp;
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, d / speed));
            if (!_timer.IsEnabled) _timer.Start();
        }

        private void ApplySpeed()
        {
            if (_timer == null || !_timer.IsEnabled) return;
            int d = _frameIndex < _delays.Count ? _delays[_frameIndex] : 100;
            double speed = 1;
            if (double.TryParse(Txt(SpeedProp, Ctx), out var sp) && sp > 0) speed = sp;
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, d / speed));
        }

        private void StopTimer()
        {
            if (_timer != null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
        }

        private static BitmapImage LoadBitmap(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            return bmp;
        }

        public override Dictionary<string, PropertyValue> GetProps()
        {
            var d = new Dictionary<string, PropertyValue>
            {
                ["source"] = SourceProp, ["stretch"] = StretchProp,
                ["radius"] = RadiusProp, ["bg"] = BgProp, ["speed"] = SpeedProp
            };
            AddCommonProps(d);
            return d;
        }
        public override void SetProps(Dictionary<string, PropertyValue> props)
        {
            if (props.TryGetValue("source", out var s)) SourceProp = s;
            if (props.TryGetValue("stretch", out var st)) StretchProp = st;
            if (props.TryGetValue("radius", out var r)) RadiusProp = r;
            if (props.TryGetValue("bg", out var bg)) BgProp = bg;
            if (props.TryGetValue("speed", out var sp)) SpeedProp = sp;
            ReadCommonProps(props);
        }

        public override List<EditField> EditFields()
        {
            var l = base.EditFields();
            l.Add(new EditField { Key = "source", Label = Loc.T("atom.label.gifSource"), Kind = EditKind.File, Hint = Loc.T("atom.hint.gifSource") });
            l.Add(new EditField { Key = "stretch", Label = Loc.T("atom.label.stretch"), Kind = EditKind.Choice, Choices = new[] { "None", "Uniform", "Fill", "UniformToFill" } });
            l.Add(new EditField { Key = "radius", Label = Loc.T("atom.label.radius"), Kind = EditKind.Slider, Min = 0, Max = 200, Tab = "style" });
            l.Add(new EditField { Key = "bg", Label = Loc.T("atom.label.bgPlaceholder"), Kind = EditKind.Color, Tab = "style" });
            l.Add(new EditField { Key = "speed", Label = Loc.T("atom.label.speed"), Kind = EditKind.Slider, Min = 0.25, Max = 4, Tab = "style" });
            return l;
        }
    }
}
