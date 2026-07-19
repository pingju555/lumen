using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Lumen.Render
{
    /// <summary>
    /// 混合模式 WPF 像素着色器。
    /// 将原子内容（Input）与背景（Background）按 Mode 参数进行混合。
    /// 背景通过 VisualBrush 传递——由 AtomHost 在 Compose 时创建。
    /// </summary>
    public class BlendEffect : ShaderEffect
    {
        /// <summary>原子自身的渲染内容（sampler 0）。WPF 自动填充。</summary>
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty("Input", typeof(BlendEffect), 0);

        /// <summary>原子下方的背景画布内容（sampler 1）。需外部传入 VisualBrush。</summary>
        public static readonly DependencyProperty BackgroundProperty =
            RegisterPixelShaderSamplerProperty("Background", typeof(BlendEffect), 1);

        /// <summary>混合模式参数：0=Normal 1=Multiply 2=Screen 3=Overlay 4=Darken 5=Lighten 6=Difference</summary>
        public static readonly DependencyProperty ModeProperty =
            DependencyProperty.Register("Mode", typeof(double), typeof(BlendEffect),
                new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));

        public BlendEffect()
        {
            var ps = new PixelShader();
            ps.UriSource = new Uri("pack://application:,,,/Render/BlendModes.ps");
            PixelShader = ps;

            // 注册 sampler 和参数
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(BackgroundProperty);
            UpdateShaderValue(ModeProperty);

            // 默认背景为黑色（无原子时或关闭混合模式时仍能正常渲染）
            Background = new SolidColorBrush(Colors.Black);
        }

        public double Mode
        {
            get => (double)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }
    }
}
