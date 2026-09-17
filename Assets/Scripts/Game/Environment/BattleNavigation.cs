using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡一的平面移动与约 2m 静态网格。只识别 BattleObstacle，不依赖 Layer/Tag。
/// Move 返回新位置，由调用者赋给 transform；半径必须已换算为世界单位。
/// GetNextDirection 供敌人低频、错帧查询，调用者缓存方向；动态树精只由 Move 处理。
/// </summary>
public class BattleNavigation : MonoBehaviour
{
    private const float CellSize = 2f;
    private const float Skin = 0.01f;
    private const float Epsilon = 0.0001f;
    private const int MaxRadiusGrids = 8;

    private sealed class RadiusGrid
    {
        public readonly bool[] Free;
        // 四邻接避免对角切角；每条边还检查整个圆盘扫过的净空。
        public readonly byte[] Links;

        public RadiusGrid(int count)
        {
            Free = new bool[count];
            Links = new byte[count];
        }
    }

    private StageEnvironment mStage;
    private Vector3 mCenter;
    private float mHalfSize;
    private float mCellSize;
    private int mWidth;
    private readonly List<Bounds> mStaticBounds = new List<Bounds>();
    private readonly Dictionary<float, RadiusGrid> mGrids = new Dictionary<float, RadiusGrid>();
    private int[] mParents;
    private int[] mQueue;
    private RaycastHit[] mHits = new RaycastHit[32];
    private Collider[] mOverlaps = new Collider[32];

    public void Initialize(StageEnvironment stage)
    {
        mStage = stage;
        Rebuild();
    }

    public void Rebuild()
    {
        mGrids.Clear();
        mStaticBounds.Clear();
        mWidth = 0;
        if (mStage == null) mStage = GetComponent<StageEnvironment>();
        if (mStage == null || !Finite(mStage.HalfSize) || mStage.HalfSize <= 0f) return;

        // kinematic/trigger 角色通过 transform 移动；不假设 autoSyncTransforms 已开启。
        Physics.SyncTransforms();
        mCenter = mStage.transform.position;
        mHalfSize = mStage.HalfSize;
        mWidth = Mathf.Max(1, Mathf.CeilToInt(2f * mHalfSize / CellSize));
        mCellSize = 2f * mHalfSize / mWidth;
        mParents = new int[mWidth * mWidth];
        mQueue = new int[mParents.Length];

        // 只烘焙此地图子层级的静态障碍。世界 XZ AABB 是保守足迹，支持旋转/缩放的岩石。
        // 运行中移动或增删静态墙岩后必须 Rebuild；树精不会触发每帧烘焙。
        foreach (var shape in mStage.GetComponentsInChildren<Collider>())
        {
            if (IsObstacle(shape, null, true)) mStaticBounds.Add(shape.bounds);
        }
    }

    public Vector3 Move(Vector3 position, Vector3 displacement, float radius, Collider self = null)
    {
        if (!Ready() || !Finite(position) || !Finite(displacement) || !Finite(radius)) return position;
        radius = Mathf.Max(0f, radius);
        var current = ClampToBounds(position, radius);
        if (radius >= mHalfSize) return current;
        displacement.y = 0f;
        Physics.SyncTransforms();

        // 对完整位移扫掠，而非只测试终点；边界也作为碰撞平面参与滑移。
        // 最多四次接触，尖角中宁可停下也不穿透。
        for (var iteration = 0; iteration < 4; iteration++)
        {
            var distance = displacement.magnitude;
            if (!Finite(distance) || distance <= Epsilon) break;
            var direction = displacement / distance;
            var travel = distance;
            var normal = Vector3.zero;
            var blocked = SweepBoundary(current, direction, radius, ref travel, ref normal);
            RaycastHit hit;
            if (Sweep(current, direction, distance, radius, self, false, out hit) && hit.distance <= travel)
            {
                travel = Mathf.Max(0f, hit.distance);
                normal = hit.normal;
                normal.y = 0f;
                // 球扫到顶部时没有可用的平面法线，保守停下。
                if (normal.sqrMagnitude <= Epsilon * Epsilon) normal = -direction;
                else normal.Normalize();
                blocked = true;
            }

            if (!blocked)
            {
                current += displacement;
                break;
            }

            var advance = Mathf.Max(0f, travel - Skin);
            current += direction * advance;
            displacement -= direction * advance;
            var intoSurface = Vector3.Dot(displacement, normal);
            if (intoSurface < 0f) displacement -= normal * intoSurface;
            else break;
        }

        current = ClampToBounds(current, radius);
        current.y = position.y;
        return current;
    }

    public bool TrySweepObstacle(Vector3 origin, Vector3 direction, float distance, float radius,
        Collider self, out RaycastHit hit)
    {
        hit = default(RaycastHit);
        if (!Finite(origin) || !Finite(direction) || !Finite(distance) || !Finite(radius) || distance <= 0f)
            return false;
        direction.y = 0f;
        var length = direction.magnitude;
        if (!Finite(length) || length <= Epsilon) return false;
        Physics.SyncTransforms();
        return Sweep(origin, direction / length, distance, Mathf.Max(0f, radius), self, false, out hit);
    }

    /// <summary>修盾视线只被静态墙岩阻挡；动态树精/友军和没有标记的地面永不挡视线。</summary>
    public bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        if (!Finite(from) || !Finite(to)) return false;
        Physics.SyncTransforms();
        if (OverlapsObstacle(from, Epsilon, null, true) || OverlapsObstacle(to, Epsilon, null, true))
            return false;
        var offset = to - from;
        var distance = offset.magnitude;
        if (!Finite(distance)) return false;
        if (distance <= Epsilon) return true;
        RaycastHit hit;
        return !Sweep(from, offset / distance, distance, 0f, null, true, out hit);
    }

    public bool IsFree(Vector3 position, float radius, Collider self = null)
    {
        if (!Ready() || !Finite(position) || !Finite(radius)) return false;
        radius = Mathf.Max(0f, radius);
        if (!InsideBounds(position, radius)) return false;
        Physics.SyncTransforms();
        return !OverlapsObstacle(position, Mathf.Max(radius, Epsilon), self, false);
    }

    public Vector3 GetNextDirection(Vector3 position, Vector3 target, float radius)
    {
        if (!Ready() || !Finite(position) || !Finite(target) || !Finite(radius)) return Vector3.zero;
        radius = Mathf.Max(0f, radius);
        if (radius + Skin >= mHalfSize) return Vector3.zero;
        target = ClampToBounds(target, radius + Skin);
        target.y = position.y;
        if ((target - position).sqrMagnitude <= Epsilon * Epsilon) return Vector3.zero;

        // 这里必须检查带半径的通路，而不是只凭零宽度视线就直走。
        if (StaticSegmentFree(position, target, radius)) return FlatDirection(position, target);

        // 向上取整到 0.25m，绝不把小体型的净空缓存复用于大体型。
        var gridRadius = Mathf.Ceil(radius * 4f) / 4f;
        var grid = GetGrid(gridRadius);
        var start = FindStart(grid, position, radius);
        if (start < 0) return Vector3.zero;
        for (var i = 0; i < mParents.Length; i++) mParents[i] = -1;
        var read = 0;
        var write = 1;
        mQueue[0] = start;
        mParents[start] = start;
        var nearest = start;
        var nearestDistance = float.PositiveInfinity;
        var goal = -1;
        var targetFree = StaticPointFree(target, radius);

        // BFS 仅访问起点所在可达分量；目标在岩中或另一分量时选邻近可达格。
        // 优先选能安全接到真实目标的格子，防止薄墙两侧同格时停在错误一边。
        while (read < write)
        {
            var node = mQueue[read++];
            var point = CellPosition(node, position.y);
            var score = (point - target).sqrMagnitude;
            if (score < nearestDistance)
            {
                nearestDistance = score;
                nearest = node;
            }
            if (targetFree && StaticSegmentFree(point, target, radius))
            {
                goal = node;
                break;
            }
            var links = grid.Links[node];
            if ((links & 1) != 0) Enqueue(node, node - 1, ref write);
            if ((links & 2) != 0) Enqueue(node, node + 1, ref write);
            if ((links & 4) != 0) Enqueue(node, node - mWidth, ref write);
            if ((links & 8) != 0) Enqueue(node, node + mWidth, ref write);
        }

        if (goal < 0) goal = nearest;
        // 从终点向回找最远的可见路径节点；整段圆盘净空检测保证平滑不切岩角。
        for (var node = goal; ; node = mParents[node])
        {
            var waypoint = CellPosition(node, position.y);
            if (StaticSegmentFree(position, waypoint, radius)) return FlatDirection(position, waypoint);
            if (node == start) break;
        }
        return Vector3.zero;
    }

    private bool Ready()
    {
        if (mWidth == 0) Rebuild();
        return mWidth > 0;
    }

    private void Enqueue(int parent, int node, ref int write)
    {
        if (mParents[node] >= 0) return;
        mParents[node] = parent;
        mQueue[write++] = node;
    }

    private RadiusGrid GetGrid(float radius)
    {
        RadiusGrid grid;
        if (mGrids.TryGetValue(radius, out grid)) return grid;
        // 关一只有少量体型，限制缓存规模；无每个敌人的独立整张网格。
        if (mGrids.Count >= MaxRadiusGrids) mGrids.Clear();
        grid = new RadiusGrid(mWidth * mWidth);
        for (var i = 0; i < grid.Free.Length; i++) grid.Free[i] = StaticPointFree(CellPosition(i, 0f), radius);
        for (var i = 0; i < grid.Free.Length; i++)
        {
            if (!grid.Free[i]) continue;
            var point = CellPosition(i, 0f);
            if (i % mWidth + 1 < mWidth && grid.Free[i + 1] &&
                StaticSegmentFree(point, CellPosition(i + 1, 0f), radius))
            {
                grid.Links[i] |= 2;
                grid.Links[i + 1] |= 1;
            }
            if (i + mWidth < grid.Free.Length && grid.Free[i + mWidth] &&
                StaticSegmentFree(point, CellPosition(i + mWidth, 0f), radius))
            {
                grid.Links[i] |= 8;
                grid.Links[i + mWidth] |= 4;
            }
        }
        mGrids.Add(radius, grid);
        return grid;
    }

    private int FindStart(RadiusGrid grid, Vector3 position, float radius)
    {
        var x = Mathf.Clamp(Mathf.FloorToInt((position.x - mCenter.x + mHalfSize) / mCellSize), 0, mWidth - 1);
        var z = Mathf.Clamp(Mathf.FloorToInt((position.z - mCenter.z + mHalfSize) / mCellSize), 0, mWidth - 1);
        var cell = z * mWidth + x;
        if (grid.Free[cell] && StaticSegmentFree(position, CellPosition(cell, position.y), radius)) return cell;
        var best = -1;
        var bestDistance = float.PositiveInfinity;
        for (var i = 0; i < grid.Free.Length; i++)
        {
            if (!grid.Free[i]) continue;
            var point = CellPosition(i, position.y);
            var distance = (point - position).sqrMagnitude;
            if (distance < bestDistance && StaticSegmentFree(position, point, radius))
            {
                bestDistance = distance;
                best = i;
            }
        }
        return best;
    }

    private Vector3 CellPosition(int index, float y)
    {
        return new Vector3(mCenter.x - mHalfSize + (index % mWidth + 0.5f) * mCellSize, y,
            mCenter.z - mHalfSize + (index / mWidth + 0.5f) * mCellSize);
    }

    private bool StaticPointFree(Vector3 point, float radius)
    {
        if (!InsideBounds(point, radius)) return false;
        var clearance = radius + Epsilon;
        foreach (var bounds in mStaticBounds)
        {
            if (PointBoxDistanceSquared(point, bounds) <= clearance * clearance) return false;
        }
        return true;
    }

    private bool StaticSegmentFree(Vector3 from, Vector3 to, float radius)
    {
        if (!InsideBounds(from, radius) || !InsideBounds(to, radius)) return false;
        var clearance = radius + Epsilon;
        var squared = clearance * clearance;
        foreach (var bounds in mStaticBounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            if (Mathf.Max(from.x, to.x) + clearance < min.x || Mathf.Min(from.x, to.x) - clearance > max.x ||
                Mathf.Max(from.z, to.z) + clearance < min.z || Mathf.Min(from.z, to.z) - clearance > max.z)
                continue;
            var enter = 0f;
            var exit = 1f;
            if (ClipAxis(from.x, to.x - from.x, min.x, max.x, ref enter, ref exit) &&
                ClipAxis(from.z, to.z - from.z, min.z, max.z, ref enter, ref exit)) return false;
            // 不相交时，线段到矩形的最近点必在端点或矩形顶点上。
            if (PointBoxDistanceSquared(from, bounds) <= squared || PointBoxDistanceSquared(to, bounds) <= squared ||
                PointSegmentDistanceSquared(min.x, min.z, from, to) <= squared ||
                PointSegmentDistanceSquared(min.x, max.z, from, to) <= squared ||
                PointSegmentDistanceSquared(max.x, min.z, from, to) <= squared ||
                PointSegmentDistanceSquared(max.x, max.z, from, to) <= squared) return false;
        }
        return true;
    }

    private static bool ClipAxis(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) <= Epsilon) return origin >= min && origin <= max;
        var first = (min - origin) / delta;
        var last = (max - origin) / delta;
        if (first > last) { var swap = first; first = last; last = swap; }
        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, last);
        return enter <= exit;
    }

    private static float PointBoxDistanceSquared(Vector3 point, Bounds bounds)
    {
        var min = bounds.min;
        var max = bounds.max;
        var x = Mathf.Max(Mathf.Max(min.x - point.x, 0f), point.x - max.x);
        var z = Mathf.Max(Mathf.Max(min.z - point.z, 0f), point.z - max.z);
        return x * x + z * z;
    }

    private static float PointSegmentDistanceSquared(float x, float z, Vector3 from, Vector3 to)
    {
        var dx = to.x - from.x;
        var dz = to.z - from.z;
        var length = dx * dx + dz * dz;
        var t = length <= Epsilon * Epsilon ? 0f : Mathf.Clamp01(((x - from.x) * dx + (z - from.z) * dz) / length);
        x -= from.x + t * dx;
        z -= from.z + t * dz;
        return x * x + z * z;
    }

    private bool SweepBoundary(Vector3 point, Vector3 direction, float radius, ref float distance, ref Vector3 normal)
    {
        var limit = Mathf.Max(0f, mHalfSize - radius);
        var blocked = false;
        if (Mathf.Abs(direction.x) > Epsilon)
        {
            var edge = mCenter.x + (direction.x > 0f ? limit : -limit);
            var travel = Mathf.Max(0f, (edge - point.x) / direction.x);
            if (travel <= distance)
            {
                distance = travel;
                normal = direction.x > 0f ? Vector3.left : Vector3.right;
                blocked = true;
            }
        }
        if (Mathf.Abs(direction.z) > Epsilon)
        {
            var edge = mCenter.z + (direction.z > 0f ? limit : -limit);
            var travel = Mathf.Max(0f, (edge - point.z) / direction.z);
            if (travel <= distance)
            {
                distance = travel;
                normal = direction.z > 0f ? Vector3.back : Vector3.forward;
                blocked = true;
            }
        }
        return blocked;
    }

    private bool Sweep(Vector3 origin, Vector3 direction, float distance, float radius, Collider self,
        bool staticOnly, out RaycastHit closest)
    {
        closest = default(RaycastHit);
        int count;
        // NonAlloc 返回满数组时可能丢失最近障碍，扩大后重查，绝不使用被截断的结果。
        while (true)
        {
            count = radius > Epsilon
                ? Physics.SphereCastNonAlloc(origin, radius, direction, mHits, distance, ~0, QueryTriggerInteraction.Collide)
                : Physics.RaycastNonAlloc(origin, direction, mHits, distance, ~0, QueryTriggerInteraction.Collide);
            if (count < mHits.Length) break;
            Array.Resize(ref mHits, mHits.Length * 2);
        }
        var found = false;
        var nearest = float.PositiveInfinity;
        for (var i = 0; i < count; i++)
        {
            var candidate = mHits[i];
            if (!IsObstacle(candidate.collider, self, staticOnly) || candidate.distance >= nearest) continue;
            if (radius > Epsilon && candidate.distance <= Epsilon)
            {
                // SphereCast 的初始接触可能给出 -direction 法线。允许离开/沿着接触面走，
                // 但中心已在障碍内部时保守阻止；出生/瞬移须先通过 IsFree，不做穿墙脱困。
                var outward = origin - candidate.collider.ClosestPoint(origin);
                outward.y = 0f;
                if (outward.sqrMagnitude > Epsilon * Epsilon)
                {
                    outward.Normalize();
                    if (Vector3.Dot(outward, direction) >= 0f) continue;
                    candidate.normal = outward;
                }
            }
            nearest = candidate.distance;
            closest = candidate;
            found = true;
        }
        return found;
    }

    private bool OverlapsObstacle(Vector3 position, float radius, Collider self, bool staticOnly)
    {
        int count;
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(position, radius, mOverlaps, ~0, QueryTriggerInteraction.Collide);
            if (count < mOverlaps.Length) break;
            Array.Resize(ref mOverlaps, mOverlaps.Length * 2);
        }
        for (var i = 0; i < count; i++)
        {
            if (IsObstacle(mOverlaps[i], self, staticOnly)) return true;
        }
        return false;
    }

    private static bool IsObstacle(Collider shape, Collider self, bool staticOnly)
    {
        if (shape == null || shape == self || !shape.enabled || !shape.gameObject.activeInHierarchy) return false;
        var obstacle = shape.GetComponentInParent<BattleObstacle>();
        if (obstacle == null || !obstacle.isActiveAndEnabled || (staticOnly && obstacle.IsDynamic)) return false;
        if (self != null)
        {
            // self 可以是子 Collider；同一个刚体/障碍标记下的复合碰撞体都属于自身。
            if (self.attachedRigidbody != null && self.attachedRigidbody == shape.attachedRigidbody) return false;
            if (self.GetComponentInParent<BattleObstacle>() == obstacle) return false;
        }
        return true;
    }

    private bool InsideBounds(Vector3 point, float radius)
    {
        var limit = mHalfSize - radius;
        return limit >= 0f && Mathf.Abs(point.x - mCenter.x) <= limit && Mathf.Abs(point.z - mCenter.z) <= limit;
    }

    private Vector3 ClampToBounds(Vector3 point, float radius)
    {
        var limit = Mathf.Max(0f, mHalfSize - radius);
        point.x = Mathf.Clamp(point.x, mCenter.x - limit, mCenter.x + limit);
        point.z = Mathf.Clamp(point.z, mCenter.z - limit, mCenter.z + limit);
        return point;
    }

    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        var offset = to - from;
        offset.y = 0f;
        return offset.sqrMagnitude <= Epsilon * Epsilon ? Vector3.zero : offset.normalized;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
}
