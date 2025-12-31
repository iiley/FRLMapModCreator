//FR Legend shader tools

#ifndef FR_LEGEND
#define FR_LEGEND


inline fixed3 changeSaturation(fixed3 cl, fixed s) {
    fixed rwgt = 0.3086;
    fixed gwgt = 0.6094;
    fixed bwgt = 0.0820;
    fixed a = (1.0-s)*rwgt + s;
    fixed b = (1.0-s)*rwgt;
    fixed c = (1.0-s)*rwgt;
    fixed d = (1.0-s)*gwgt;
    fixed e = (1.0-s)*gwgt + s;
    fixed f = (1.0-s)*gwgt;
    fixed g = (1.0-s)*bwgt;
    fixed h = (1.0-s)*bwgt;
    fixed i = (1.0-s)*bwgt + s;
    fixed4x4 mat = {
        a,      d,      g,      0.0,
        b,      e,      h,      0.0,
        c,      f,      i,      0.0,
        0.0,    0.0,    0.0,    1.0,
    };
    return mul(mat, cl);
}

float Epsilon = 1e-10;

inline float3 rgb2hcv(in float3 RGB)
{
    // Based on work by Sam Hocevar and Emil Persson
    float4 P = lerp(float4(RGB.bg, -1.0, 2.0/3.0), float4(RGB.gb, 0.0, -1.0/3.0), step(RGB.b, RGB.g));
    float4 Q = lerp(float4(P.xyw, RGB.r), float4(RGB.r, P.yzx), step(P.x, RGB.r));
    float C = Q.x - min(Q.w, Q.y);
    float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
    return float3(H, C, Q.x);
}

inline float3 rgb2hsl(in float3 RGB)
{
    float3 HCV = rgb2hcv(RGB);
    float L = HCV.z - HCV.y * 0.5;
    float S = HCV.y / (1 - abs(L * 2 - 1) + Epsilon);
    return float3(HCV.x, S, L);
}

inline float3 hsl2rgb(float3 c)
{
    c = float3(frac(c.x), clamp(c.yz, 0.0, 1.0));
    float3 rgb = clamp(abs(fmod(c.x * 6.0 + float3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return c.z + c.y * (rgb - 0.5) * (1.0 - abs(2.0 * c.z - 1.0));
}
            
#endif // FR_LEGEND