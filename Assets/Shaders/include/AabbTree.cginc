#ifndef RAY_MARCHING_AABB_TREE
#define RAY_MARCHING_AABB_TREE
#define FLOAT_MAX   (1e32f)
#define EPSILON     (1e-16f)

struct Aabb
{
  float4 boundsMin;
  float4 boundsMax;
};

struct AabbNode
{
  Aabb aabb;
  int parent;
  int childA;
  int childB;
  int shapeIndex;
};

inline float3 aabb_center(Aabb aabb)
{
  return 0.5f * (aabb.boundsMin.xyz + aabb.boundsMax.xyz);
}

inline float3 aabb_extents(Aabb aabb)
{
  return aabb.boundsMax.xyz - aabb.boundsMin.xyz;
}

inline float3 aabb_half_extents(Aabb aabb)
{
  return 0.5f * (aabb.boundsMax.xyz - aabb.boundsMin.xyz);
}

inline bool aabb_intersects(Aabb a, Aabb b)
{
  return all(a.boundsMin <= b.boundsMax && a.boundsMax >= b.boundsMin);
}

float aabb_ray_cast(Aabb aabb, float3 from, float3 to)
{
  float tMin = -FLOAT_MAX;
  float tMax = +FLOAT_MAX;

  float3 d = to - from;
  float3 absD = abs(d);

  if (any(absD < EPSILON))
  {
    // parallel?
    if (any(from < aabb.boundsMin.xyz) || any(aabb.boundsMax.xyz < from))
      return -FLOAT_MAX;
  }
  else
  {
    float3 invD = 1.0f / d;
    float3 t1 = (aabb.boundsMin.xyz - from) * invD;
    float3 t2 = (aabb.boundsMax.xyz - from) * invD;
    float3 minComps = min(t1, t2);
    float3 maxComps = max(t1, t2);

    tMin = max(minComps.x, max(minComps.y, minComps.z));
    tMax = min(maxComps.x, min(maxComps.y, maxComps.z));
  }

  if (tMin > tMax)
    return -FLOAT_MAX;

  if (tMin < 0.0f)
    return -FLOAT_MAX;

  return tMin;
}

// stmt = statements processing shapeIndex of hit leaf AABB nodes
#define aabb_tree_ray_cast(tree, root, rayFrom, rayTo, stackSize, stmt)       \
{                                                                             \
  float3 rayDir = normalize(rayTo - rayFrom);                                 \
  float3 rayDirOrtho = normalize(find_ortho(rayDir));                         \
  float3 rayDirOrthoAbs = abs(rayDirOrtho);                                   \
                                                                              \
  Aabb rayBounds;                                                             \
  rayBounds.boundsMin = float4(min(rayFrom, rayTo), 0.0f);                    \
  rayBounds.boundsMax = float4(max(rayFrom, rayTo), 0.0f);                    \
                                                                              \
  int stackTop = 0;                                                           \
  int stack[stackSize];                                                       \
  stack[stackTop] = root;                                                     \
                                                                              \
  while (stackTop >= 0)                                                       \
  {                                                                           \
    int index = stack[stackTop--];                                            \
    if (index < 0)                                                            \
      continue;                                                               \
                                                                              \
    if (!aabb_intersects(tree[index].aabb, rayBounds))                        \
      continue;                                                               \
                                                                              \
    float3 aabbCenter = aabb_center(tree[index].aabb);                        \
    float3 aabbHalfExtents = aabb_half_extents(tree[index].aabb);             \
    float separation =                                                        \
      abs(dot(rayDirOrtho, rayFrom - aabbCenter))                             \
      - dot(rayDirOrthoAbs, aabbHalfExtents);                                 \
    if (separation > 0.0f)                                                    \
      continue;                                                               \
                                                                              \
    if (tree[index].childA < 0)                                               \
    {                                                                         \
      float t = aabb_ray_cast(tree[index].aabb, rayFrom, rayTo);              \
      if (t < 0.0f)                                                           \
        continue;                                                             \
                                                                              \
      const int shapeIndex = tree[index].shapeIndex;                          \
                                                                              \
      stmt                                                                    \
    }                                                                         \
    else                                                                      \
    {                                                                         \
      stackTop = min(stackTop + 1, stackSize - 1);                            \
      stack[stackTop] = tree[index].childA;                                   \
      stackTop = min(stackTop + 1, stackSize - 1);                            \
      stack[stackTop] = tree[index].childB;                                   \
    }                                                                         \
  }                                                                           \
}

#endif

