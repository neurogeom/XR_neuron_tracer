#ifndef MATH_CGINC
#define MATH_CGINC

#define PI 3.14159265358979

float Rand(float2 seed)
{
    return frac(sin(dot(seed, float2(12.9898, 78.233))) * 43758.5453);
}

inline float Mod(float a, float b)
{
    return frac(abs(a / b)) * abs(b);
}

inline float2 Mod(float2 a, float2 b)
{
    return frac(abs(a / b)) * abs(b);
}

inline float3 Mod(float3 a, float3 b)
{
    return frac(abs(a / b)) * abs(b);
}

inline float SmoothMin(float d1, float d2, float k)
{
    float h = exp(-k * d1) + exp(-k * d2);
    return -log(h) / k;
}


inline float CubicMin(float d1, float d2, float k)
{
     //return min(d1,d2);
    float h = max(k - abs(d1 - d2), 0.0f) / k;

    return min(d1, d2) - h * h * h * k * (1.0f / 6.0f);
}

float dynamicK(float3 p, float3 centroid1, float3 centroid2, float baseK) {
    float dist1 = length(p - centroid1);
    float dist2 = length(p - centroid2);
    float weight = dist1 / (dist1 + dist2);
    // 使 k 在两个重心附近增加，远离重心时减少
    return lerp(baseK * 0.5, baseK * 2.0, weight);
}

float opSmoothUnion(float d1, float d2, float3 p, float3 centroid1, float3 centroid2) {
    float k = dynamicK(p, centroid1, centroid2, 0.1); // 假设基础k值为0.1
    float h = max(k - abs(d1 - d2), 0.0) / k;
    return min(d1, d2) - h * h * h * k * (1.0 / 6.0);
}
 
inline float SmoothSubtraction( float d1, float d2, float k ) {
    float h = clamp( 0.5 - 0.5*(d2+d1)/k, 0.0, 1.0 );
    return lerp( d2, -d1, h ) + k*h*(1.0-h); }

inline float Repeat(float pos, float span)
{
    return Mod(pos, span) - span * 0.5;
}

inline float2 Repeat(float2 pos, float2 span)
{
    return Mod(pos, span) - span * 0.5;
}

inline float3 Repeat(float3 pos, float3 span)
{
    return Mod(pos, span) - span * 0.5;
}

inline float3 Rotate(float3 p, float angle, float3 axis)
{
    float3 a = normalize(axis);
    float s = sin(angle);
    float c = cos(angle);
    float r = 1.0 - c;
    float3x3 m = float3x3(
        a.x * a.x * r + c,
        a.y * a.x * r + a.z * s,
        a.z * a.x * r - a.y * s,
        a.x * a.y * r - a.z * s,
        a.y * a.y * r + c,
        a.z * a.y * r + a.x * s,
        a.x * a.z * r + a.y * s,
        a.y * a.z * r - a.x * s,
        a.z * a.z * r + c
    );
    return mul(m, p);
}

inline float3 TwistY(float3 p, float power)
{
    float s = sin(power * p.y);
    float c = cos(power * p.y);
    float3x3 m = float3x3(
          c, 0.0,  -s,
        0.0, 1.0, 0.0,
          s, 0.0,   c
    );
    return mul(m, p);
}

inline float3 TwistX(float3 p, float power)
{
    float s = sin(power * p.y);
    float c = cos(power * p.y);
    float3x3 m = float3x3(
        1.0, 0.0, 0.0,
        0.0,   c,   s,
        0.0,  -s,   c
    );
    return mul(m, p);
}

inline float3 TwistZ(float3 p, float power)
{
    float s = sin(power * p.y);
    float c = cos(power * p.y);
    float3x3 m = float3x3(
          c,   s, 0.0,
         -s,   c, 0.0,
        0.0, 0.0, 1.0
    );
    return mul(m, p);
}

#endif
