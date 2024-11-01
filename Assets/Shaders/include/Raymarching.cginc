#ifndef RAYMARCHING_CGINC
#define RAYMARCHING_CGINC

#include "UnityCG.cginc"
#include "./Camera.cginc"
#include "./Utils.cginc"
#include "./Structs.cginc"

// #ifndef DISTANCE_FUNCTION
// inline float _DefaultDistanceFunction(float3 pos)
// {
//     return Box(pos, 1.0);
// }
// #define DISTANCE_FUNCTION _DefaultDistanceFunction
// #endif

// inline float _DistanceFunction(float3 pos)
// {
//     #ifdef WORLD_SPACE
//     return DISTANCE_FUNCTION(pos);
//     #else
//     #ifdef OBJECT_SCALE
//     return DISTANCE_FUNCTION(ToLocal(pos));
//     #else
//     return DISTANCE_FUNCTION(ToLocal(pos) * GetScale());
//     #endif
//     #endif
// }
//
// inline float3 GetDistanceFunctionNormal(float3 pos)
// {
//     const float d = 1e-4;
//     return normalize(float3(
//         _DistanceFunction(pos + float3(d, 0.0, 0.0)) - _DistanceFunction(pos),
//         _DistanceFunction(pos + float3(0.0, d, 0.0)) - _DistanceFunction(pos),
//         _DistanceFunction(pos + float3(0.0, 0.0, d)) - _DistanceFunction(pos)));
// }

inline bool _ShouldRaymarchFinish(RaymarchInfo ray)
{
    if (ray.lastDistance < ray.minDistance || ray.totalLength > ray.maxDistance) return true;

    #if defined(OBJECT_SHAPE_CUBE) && !defined(FULL_SCREEN)
    if (!IsInnerObject(ray.endPos)) return true;
    #endif

    return false;
}

inline void InitRaymarchFullScreen(out RaymarchInfo ray, float4 projPos)
{
    UNITY_INITIALIZE_OUTPUT(RaymarchInfo, ray);
    ray.rayDir = GetCameraDirection(projPos);
    ray.projPos = projPos;
    #if defined(USING_STEREO_MATRICES)
    float3 cameraPos = unity_StereoWorldSpaceCameraPos[unity_StereoEyeIndex];
    cameraPos += float3(1., 0, 0) * unity_StereoEyeIndex;
    #else
    float3 cameraPos = _WorldSpaceCameraPos;
    #endif
    ray.startPos = cameraPos + GetCameraNearClip() * ray.rayDir;
    ray.maxDistance = GetCameraFarClip();
}

inline void InitRaymarchObject(out RaymarchInfo ray, float4 projPos, float3 worldPos, float3 worldNormal)
{
    UNITY_INITIALIZE_OUTPUT(RaymarchInfo, ray);
    ray.rayDir = normalize(worldPos - GetCameraPosition());
    ray.projPos = projPos;
    ray.startPos = worldPos;
    ray.polyNormal = worldNormal;
    ray.maxDistance = GetCameraFarClip();

    float3 cameraNearPlanePos = GetCameraPosition() + GetDistanceFromCameraToNearClipPlane(projPos) * ray.rayDir;
    if (IsInnerObject(cameraNearPlanePos))
    {
        ray.startPos = cameraNearPlanePos;
        ray.polyNormal = -ray.rayDir;
    }

    #ifdef CAMERA_INSIDE_OBJECT
    #endif
}

inline void InitRaymarchParams(inout RaymarchInfo ray, int maxLoop, float minDistance)
{
    ray.maxLoop = maxLoop;
    ray.minDistance = minDistance;
}

#ifdef USE_CAMERA_DEPTH_TEXTURE
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

inline void UseCameraDepthTextureForMaxDistance(inout RaymarchInfo ray, float4 projPos)
{
    float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(projPos)));
    float dist = depth / dot(ray.rayDir, GetCameraForward());
    ray.maxDistance = dist;
}
#endif

#if defined(FULL_SCREEN)
    #define INITIALIZE_RAYMARCH_INFO(ray, i, loop, minDistance) \
        InitRaymarchFullScreen(ray, i.projPos); \
        InitRaymarchParams(ray, loop, minDistance);
#elif defined(USE_CAMERA_DEPTH_TEXTURE)
    #define INITIALIZE_RAYMARCH_INFO(ray, i, loop, minDistance) \
        InitRaymarchObject(ray, i.projPos, i.worldPos, i.worldNormal); \
        InitRaymarchParams(ray, loop, minDistance); \
        UseCameraDepthTextureForMaxDistance(ray, i.projPos);
#else
#define INITIALIZE_RAYMARCH_INFO(ray, i, loop, minDistance) \
        InitRaymarchObject(ray, i.projPos, i.worldPos, i.worldNormal); \
        InitRaymarchParams(ray, loop, minDistance);
#endif

float _DistanceMultiplier;

// inline bool _Raymarch(inout RaymarchInfo ray)
// {
//     ray.endPos = ray.startPos;
//     ray.lastDistance = 0.0;
//     ray.totalLength = length(ray.endPos - GetCameraPosition());
//
//     float multiplier = _DistanceMultiplier;
//     #ifdef OBJECT_SCALE
//     float3 localRayDir = normalize(mul(unity_WorldToObject, ray.rayDir));
//     multiplier *= length(mul(unity_ObjectToWorld, localRayDir));
//     #endif
//
//     for (ray.loop = 0; ray.loop < ray.maxLoop; ++ray.loop)
//     {
//         ray.lastDistance = _DistanceFunction(ray.endPos) * multiplier;
//         ray.totalLength += ray.lastDistance;
//         ray.endPos += ray.rayDir * ray.lastDistance;
//         if (_ShouldRaymarchFinish(ray)) break;
//     }
//
//     return ray.lastDistance < ray.minDistance; // && ray.totalLength < ray.maxDistance;
// }

#define SDF_NEAR_SHAPES_TYPE(res, p, aiNearShape, numNearShapes, nodeIndex)             \
{                                                                                       \
    res = FLOAT_MAX;                                                                    \
    for (int i = 0; i < numNearShapes; ++i)                                             \
    {                                                                                   \
        const int iShape = aiNearShape[i];                                              \
        float new_res = SdCurve(p,iShape);                                              \
        if(new_res < res)                                                               \
        {                                                                               \
            res = new_res;                                                              \
            nodeIndex = iShape;                                                         \
        }                                                                               \
    }\
float sdf_sphere = length(p-_SomaPos) - _SomaRadius;\
res = CubicMin(sdf_sphere,res,_SomaBlend);\
}


#define SDF_NEAR_SHAPES(res, p, aiNearShape,numNearShapes)                              \
{                                                                                       \
    res = FLOAT_MAX;                                                                    \
    for (int i = 0; i < numNearShapes; ++i)                                             \
    {                                                                                   \
        const int iShape = aiNearShape[i];                                              \
        float new_result = SdCurve(p,iShape);                                           \
        if(new_result < res)                                                            \
        {                                                                               \
            res = new_result;                                                           \
        }                                                                               \
    }                                                                                   \
float sdf_sphere = length(p-_SomaPos) - _SomaRadius;\
res = CubicMin(sdf_sphere,res,_SomaBlend);\
} 

    // float sdf_sphere = length(p-float3(0.01,0.02,0.94)) - 0.06;\
    // res = CubicMin(sdf_sphere,res,_BlendDistance);\

// void Raymarch(inout RaymarchInfo ray)
// {
//     if (!_Raymarch(ray)) discard;
//
//     // #ifdef FULL_SCREEN
//     //     float3 normal = GetDistanceFunctionNormal(ray.endPos);
//     //     ray.normal = EncodeNormal(normal);
//     //     ray.depth = EncodeDepth(ray.endPos);
//     //     return;
//     // #endif
//     //
//     // #ifdef CAMERA_INSIDE_OBJECT
//     //     if (IsInnerObject(GetCameraPosition()) && ray.totalLength < GetCameraNearClip()) {
//     //         ray.normal = EncodeNormal(-ray.rayDir);
//     //         ray.depth = EncodeDepth(ray.startPos);
//     //         return;
//     //     }
//     // #endif
//
//     float initLength = length(ray.startPos - GetCameraPosition());
//     if (ray.totalLength - initLength < ray.minDistance)
//     {
//         ray.normal = EncodeNormal(ray.polyNormal);
//         ray.depth = EncodeDepth(ray.startPos) - 1e-6;
//     }
//     else
//     {
//         float3 normal = GetDistanceFunctionNormal(ray.endPos);
//         ray.normal = EncodeNormal(normal);
//         ray.depth = EncodeDepth(ray.endPos);
//     }
// }

#define kAabbTreeStackSize (256)
#define kMaxShapesPerRay   (128)
#define kSqrt3Inv    (0.5773502691)
#define DISTANCE_FUNCTION DistanceFunction
#include "UnityCG.cginc"
#include "./include/Structs.cginc"
#include "./include/Utils.cginc"
#include "./include/Camera.cginc"
#include "./include/Primitives.cginc"
#include "./include/Math.cginc"
#include "./include/AabbTree.cginc"

StructuredBuffer<AabbNode> _AabbTree;
int _AabbTreeRoot;

struct SdfNode
{
    float4 posRad;
    int parentIndex;
    int type;
};

StructuredBuffer<SdfNode> _SdfNodes;
int _NumSdfNodes = -1;
float _BlendDistance;
float4 _Color1;
float4 _Color2;
float4 _Color3;
float4 _Color4;
float4 _Color5;
float _NormalPrecise;
float _SDFPrecise;

float3 _SomaPos;
float _SomaRadius;
float _SomaBlend;


float SdRoundCone(float3 p, float3 a, float3 b, float r1, float r2)
{
    // sampling independent computations (only depend on shape)
    float3 ba = b - a;
    float l2 = dot(ba, ba);
    float rr = r1 - r2;
    float a2 = l2 - rr * rr;
    float il2 = 1.0 / l2;

    // sampling dependant computations
    float3 pa = p - a;
    float y = dot(pa, ba);
    float z = y - l2;
    float x2 = dot2(pa * l2 - ba * y);
    float y2 = y * y * l2;
    float z2 = z * z * l2;

    // single square root!
    float k = sign(rr) * rr * rr * x2;
    if (sign(z) * a2 * z2 > k) return sqrt(x2 + z2) * il2 - r2;
    if (sign(y) * a2 * y2 < k) return sqrt(x2 + y2) * il2 - r1;
    return (sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
}

inline float SdNode(float3 pos, int nodeIndex)
{
    SdfNode node = _SdfNodes[nodeIndex];
    SdfNode parent = _SdfNodes[node.parentIndex]; //root parent is itself
    if (nodeIndex == node.parentIndex) return length(pos - node.posRad.xyz) - node.posRad.w;
    float d1 = SdRoundCone(pos, node.posRad.xyz, parent.posRad.xyz, node.posRad.w, parent.posRad.w);
    float d2 = length(pos - parent.posRad.xyz) - parent.posRad.w;
    return d1;
    return max(d1, -d2);
}

float addv(float2 a) { return a.x + a.y; }

float2 refineSolution(double3 coeff, double2 initialGuess)
{
    // Newton-Raphson iteration for improved precision
    const int maxIterations = 5; // You can adjust this for more precision
    double2 refined = initialGuess;
    for (int i = 0; i < maxIterations; ++i) {
        double f = ((coeff.x * refined.x + coeff.y) * refined.x + coeff.z) * refined.x;
        double f_prime = (3.0 * coeff.x * refined.x + 2.0 * coeff.y) * refined.x + coeff.z;
        refined.x -= f / f_prime;
    }
    return refined;
}



float2 solveCubic2(float3 a)
{
    float p = a.y - a.x * a.x / 3.;
    float p3 = p * p * p;
    float q = a.x * (2. * a.x * a.x - 9. * a.y) / 27. + a.z;
    float d = q * q + 4. * p3 / 27.;
    if (d > .0)
    {
        float2 x = (float2(1, -1) * sqrt(d) - q) * .5;
        return float2(addv(sign(x) * pow(abs(x), float2(1. / 3., 1. / 3.))) - a.x / 3.,
                      addv(sign(x) * pow(abs(x), float2(1. / 3., 1. / 3.))) - a.x / 3.);
    }
    float v = acos(-sqrt(-27. / p3) * q * .5) / 3.;
    float m = cos(v);
    float n = sin(v) * 1.732050808;
    return float2(m + m, -n - m) * sqrt(-p / 3.) - a.x / 3.;
}


#define dd(a) dot(a,a)

float DistancePointToDisk(float3 pos, float3 diskCenter, float3 diskNormal, float diskRadius)
{
    // 计算点到圆盘中心的向量
    float3 pointToCenter = pos - diskCenter;

    // 计算点到圆盘平面的垂直距离
    float distanceToPlane = abs(dot(pointToCenter, diskNormal));

    // 计算点在圆盘平面上的投影
    float3 projectionOnPlane = pos - distanceToPlane * diskNormal;

    // 计算投影点到圆盘中心的距离
    float distanceToCenter = length(projectionOnPlane - diskCenter);

    // 根据投影点与圆盘中心的距离，确定最终的距离
    if (distanceToCenter <= diskRadius)
    {
        // 点在圆盘上方或下方
        return distanceToPlane;
    }
    else
    {
        // 点在圆盘边缘外，使用勾股定理计算
        return sqrt(distanceToPlane * distanceToPlane + (distanceToCenter - diskRadius) * (distanceToCenter - diskRadius));
    }
}

float calculateRadius(float radius_a, float radius_b, float radius_c, float t)
{
    return (1 - t) * (1 - t) * radius_a + 2 * (1 - t) * t * radius_b + t * t * radius_c;
}

float SdfBezier(float3 pos, const float4 a, const float4 b, const float4 c, float bias=0)
{
    float3 A = b - a;
    float3 B = c - b - A;
    float3 C = pos - a;
    float3 D = A * 2.0;
    float AB_dot = dot(A, B);
    float CA_dot = dot(C, A);
    float CB_dot = dot(C, B);
    float A_dd = dd(A);
    float B_dd = dd(B);
    // Solve cubic equation to find t values where the distance is minimal
    float2 T = clamp(solveCubic2(float3(-3.0 * AB_dot, CB_dot - 2.0 * A_dd, CA_dot) / -B_dd), 0.0, 1.0);
    float dist1 = dd(C - (D + B * T.x) * T.x);
    float dist2 = dd(C - (D + B * T.y) * T.y);
    // Choose the t value that gives the smaller distanc
    float t = dist1 < dist2 ? T.x : T.y;
    float dist = sqrt(min(dist1, dist2));
    // Special handling for the case when Bezier curve becomes a straight line
    if(length(b.xyz * 2. - a.xyz - c.xyz) <1e-6)
    {
        float3 ap = pos-a.xyz;
        float3 ac = (c.xyz-a.xyz);
        float3 projection = dot(ap,normalize(ac)) * normalize(ac);
        t = dot(ap,normalize(ac))/length(ac);
        dist = length(ap - projection); 
    }
    // Calculate the radius at the given t value
    float radius = calculateRadius(a.w, b.w,c.w, t);
    dist -= radius;
    // Additional handling for when t is outside [0,1] range
    // Handling the start
    if (t <= 0)
    {
        float radius_A = a.w;
        float radius_B = b.w;
        if(bias>0)
        {
            
            float3 line_dir = normalize(a.xyz-b.xyz);
            float3 pos_leaf = a.xyz + line_dir * bias;
            if(radius_A==radius_B)
            {
                //dist = DistancePointToDisk(p,a_bias,normalize(a-b),r0);
                if(dot(pos-a.xyz,line_dir)>bias) dist = distance(pos,pos_leaf)-radius_A;
                else
                {
                    float3 projection = dot(pos-a.xyz, line_dir) * line_dir;
                    float3 perpVector = pos-a.xyz - projection;
                    dist = length(perpVector)-radius_A;
                 }
            }
            else
            {
                float delta_radius =  (radius_A-radius_B)/distance(a.xyz,b.xyz);
                float len = radius_A/ abs(delta_radius);
                float len_hypotenuse = sqrt((len*len)+(radius_A*radius_A));
                float cos_theta = len/len_hypotenuse;
                float sin_theta = radius_A/len_hypotenuse;
                
                float tan_radius = (radius_A<radius_B?(len-bias):(len+bias)) * sin_theta;
                float limit = radius_A<radius_B?len-tan_radius * cos_theta / radius_A * len: bias-tan_radius * sin_theta;
                float3 projection = dot(pos-a.xyz, line_dir) * line_dir;
                radius =  radius_A +  delta_radius * length(projection); 
                float3 perpVector = pos-a.xyz - projection;
                if(length(projection) > limit) dist = distance(pos,pos_leaf) - tan_radius;
                else dist = length(perpVector) -  radius;
            }
        }
        else dist = DistancePointToDisk(pos,a,normalize(a-b),radius_A);
    }
    // Handling the end
    else if(t>=1) dist = DistancePointToDisk(pos,c,normalize(c-b),c.w);
    return dist;
}


inline float SdCurve(float3 pos, int nodeIndex)
{
    SdfNode node = _SdfNodes[nodeIndex];
    SdfNode parent = _SdfNodes[node.parentIndex];
    SdfNode pparent = _SdfNodes[parent.parentIndex];
    if (nodeIndex == node.parentIndex) return length(pos - node.posRad.xyz) - node.posRad.w;
    float4 posRadA = (node.posRad + parent.posRad) / 2;
    float4 posRadB = parent.posRad;
    float4 posRadC = (parent.posRad + pparent.posRad) / 2;
    float bias = 0;
    if(node.type<0) bias = length(posRadA.xyz-posRadB.xyz);
    float sdf_bezier = SdfBezier(pos, posRadA, posRadB, posRadC, bias);
    return sdf_bezier;
}


// polynomial smooth min 1 (k=0.1)
float smin(float a, float b, float k)
{
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * (1.0 / 4.0);
}

inline float DistanceFunction(float3 pos)
{
    float ret;
    if (_NumSdfNodes <= 0)
    {
        ret = CubicMin(length(pos) - 0.05f,
                       SdRoundCone(
                           pos, float3(0, 0, -0.2), float3(0, 0, 0.2), 0.01,
                           0.02),
                       _BlendDistance);
    }
    else
    {
        ret = SdNode(pos, 0);
        for (int i = 1; i < _NumSdfNodes; i++)
        {
            // ret = smin(ret, SdNode(pos, i), _Smoothness);
            ret = CubicMin(ret, SdNode(pos, i), _BlendDistance);
        }
    }
    return ret;
}

inline float3 find_ortho(float3 v)
{
    if (v.x >= kSqrt3Inv)
        return float3(v.y, -v.x, 0.0);
    else
        return float3(0.0, v.z, -v.y);
}

bool march(inout RaymarchInfo ray)
{
    ray.endPos = ray.startPos;
    ray.lastDistance = FLOAT_MAX;
    ray.totalLength = length(ray.endPos - GetCameraPosition());

    // gather shapes around ray by casting it against AABB tree
    int aiNearShape[kMaxShapesPerRay];
    int numNearShapes = 0;
    aabb_tree_ray_cast(_AabbTree, _AabbTreeRoot, ray.startPos, ray.startPos + ray.maxDistance * ray.rayDir,
                       kAabbTreeStackSize,
                       numNearShapes = min(numNearShapes + 1, kMaxShapesPerRay);
                       aiNearShape[numNearShapes - 1] = shapeIndex;
    );

    // march ray
    int node_index;
    for (ray.loop = 0; ray.loop < ray.maxLoop; ++ray.loop)
    {
        // sample SDf
        SDF_NEAR_SHAPES_TYPE(ray.lastDistance, ray.endPos, aiNearShape, numNearShapes, node_index);
        //ray.lastDistance = _DistanceFunction(ray.endPos);
        ray.totalLength += ray.lastDistance;
        ray.endPos += ray.rayDir * ray.lastDistance;
        if (_ShouldRaymarchFinish(ray)) break;
    }
    int type = abs(_SdfNodes[_SdfNodes[node_index].parentIndex].type);
    switch (type)
    {
    case 1:
        ray.color = _Color1;
        break;
    case 2:
        ray.color = _Color2;
        break;
    case 3:
        ray.color = _Color3;
        break;
    case 4:
        ray.color = _Color4;
        break;
    default:
        ray.color = _Color5;
        break;
    }

    float initLength = length(ray.startPos - GetCameraPosition());
    if (ray.totalLength - initLength < ray.minDistance)
    {
        ray.normal = EncodeNormal(ray.polyNormal);
        ray.depth = EncodeDepth(ray.startPos) - 1e-6;
    }
    else
    {
        // compute differential normal
        const float h = _NormalPrecise;
        float n0, n1, n2, n,n3;
        // SDF_NEAR_SHAPES(n0, ray.endPos + float3( h, 0.0, 0.0), aiNearShape, numNearShapes);
        // SDF_NEAR_SHAPES(n1, ray.endPos + float3( 0.0, h, 0.0), aiNearShape, numNearShapes);
        // SDF_NEAR_SHAPES(n2, ray.endPos + float3( 0.0, 0.0, h), aiNearShape, numNearShapes);
        // SDF_NEAR_SHAPES(n, ray.endPos, aiNearShape, numNearShapes);
        // float3 normal = normalize(float3(n0 - n, n1 - n, n2 - n));
        // ray.normal = EncodeNormal(normal);
        // ray.depth = EncodeDepth(ray.endPos);
        SDF_NEAR_SHAPES(n0, ray.endPos + float3( (h), -(h), -(h)), aiNearShape, numNearShapes);
        SDF_NEAR_SHAPES(n1, ray.endPos + float3(-(h), -(h),  (h)), aiNearShape, numNearShapes);
        SDF_NEAR_SHAPES(n2, ray.endPos + float3(-(h),  (h), -(h)), aiNearShape, numNearShapes);
        SDF_NEAR_SHAPES(n3, ray.endPos + float3( (h),  (h),  (h)), aiNearShape, numNearShapes);
        float3 normal = 
          normalize
          (
              float3( 1.0f, -1.0f, -1.0f) * n0 
            + float3(-1.0f, -1.0f,  1.0f) * n1 
            + float3(-1.0f,  1.0f, -1.0f) * n2 
            + float3( 1.0f,  1.0f,  1.0f) * n3
          );
        ray.normal = EncodeNormal(normal);
        ray.depth = EncodeDepth(ray.endPos);
    }
    return ray.lastDistance < ray.minDistance;
}

#endif
