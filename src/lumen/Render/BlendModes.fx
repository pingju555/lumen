// BlendModes.fx — WPF像素着色器：7种混合模式
// 编译：fxc /T ps_2_0 /E main /Fo BlendModes.ps BlendModes.fx

// sampler0 = 前景（原子自身渲染内容）
// sampler1 = 背景（原子下方的画布内容）
sampler2D Input : register(s0);
sampler2D Background : register(s1);

// 混合模式参数：0=Normal 1=Multiply 2=Screen 3=Overlay 4=Darken 5=Lighten 6=Difference
float Mode : register(c0);

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 fg = tex2D(Input, uv);
    float4 bg = tex2D(Background, uv);
    float4 result = fg;

    // Normal（默认合成，WPF已处理，此处为兜底）
    if (Mode < 0.5)
        result = fg;
    // Multiply
    else if (Mode < 1.5)
        result = float4(fg.rgb * bg.rgb, fg.a);
    // Screen
    else if (Mode < 2.5)
        result = float4(1 - (1 - fg.rgb) * (1 - bg.rgb), fg.a);
    // Overlay
    else if (Mode < 3.5)
    {
        float3 o;
        o.r = bg.r < 0.5 ? 2 * fg.r * bg.r : 1 - 2 * (1 - fg.r) * (1 - bg.r);
        o.g = bg.g < 0.5 ? 2 * fg.g * bg.g : 1 - 2 * (1 - fg.g) * (1 - bg.g);
        o.b = bg.b < 0.5 ? 2 * fg.b * bg.b : 1 - 2 * (1 - fg.b) * (1 - bg.b);
        result = float4(o, fg.a);
    }
    // Darken
    else if (Mode < 4.5)
        result = float4(min(fg.rgb, bg.rgb), fg.a);
    // Lighten
    else if (Mode < 5.5)
        result = float4(max(fg.rgb, bg.rgb), fg.a);
    // Difference
    else
        result = float4(abs(fg.rgb - bg.rgb), fg.a);

    return result;
}
