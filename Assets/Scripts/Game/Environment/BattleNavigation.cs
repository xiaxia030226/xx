using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡一的平面移动与约 2m 静态网格。只识别 BattleObstacle，不依赖 Layer/Tag。
/// Move 返回新位置，由调用者赋给 transform；半径必须已换算为世界单位。
/// GetNextDirection 供敌人低频、错帧查询；动态障碍参与实时移动、扫掠和空位查询，不进入静态寻路。
/// </summary>
public class BattleNavigation : MonoBehaviour
{
    private const float CellSize = 2f; // 网格目标边长，实际尺寸会按场地宽度均分。
    private const float Skin = 0.01f; // 接触障碍前保留的安全间隙。
    private const float Epsilon = 0.0001f; // 几何判定的极小量及净空容差。
    private const int MaxRadiusGrids = 8; // 不同体型半径网格的缓存数量上限。

    private sealed class RadiusGrid
    {
        public readonly bool[] Free; // 指定体型可占据的网格中心标记。
        public readonly byte[] Links; // 四邻接安全通路位：1 左、2 右、4 后、8 前，避免对角切角。

        // 作用：按格子总数分配占用和邻接缓存；返回：无返回值（构造函数）。
        public RadiusGrid(int count)
        {
            // 两个数组共享格子下标，初始均不可占据且无连接，等待烘焙填充。
            Free = new bool[count];
            Links = new byte[count];
        }
    }

    private StageEnvironment mStage; // 提供边界及静态障碍层级的场地。
    private Vector3 mCenter; // 重建时缓存的场地世界中心。
    private float mHalfSize; // 重建时缓存的场地半边长。
    private float mCellSize; // 网格按场地均分后的实际边长。
    private int mWidth; // 网格单边格数，零表示尚未就绪。
    private readonly List<Bounds> mStaticBounds = new List<Bounds>(); // 静态障碍世界包围盒，寻路仅使用 XZ 足迹。
    private readonly Dictionary<float, RadiusGrid> mGrids = new Dictionary<float, RadiusGrid>(); // 按体型半径共享的静态可达网格。
    private int[] mParents; // BFS 前驱下标，负一表示未访问。
    private int[] mQueue; // BFS 复用队列，容量覆盖全部格子。
    private RaycastHit[] mHits = new RaycastHit[32]; // 实时扫掠命中缓冲，结果满时扩容重查。
    private Collider[] mOverlaps = new Collider[32]; // 实时球重叠缓冲，结果满时扩容重查。

    // 作用：绑定场地并重建静态导航基础数据；返回：无返回值。
    public void Initialize(StageEnvironment stage)
    {
        // 先切换场地引用，再统一重建边界与网格，避免沿用上一个场地的数据。
        mStage = stage;
        Rebuild();
    }

    // 作用：清除体型缓存并重新采集场地和静态障碍；返回：无返回值。
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

    // 作用：按世界半径扫掠障碍和边界并沿接触面滑移；返回：保留原高度的新位置，未就绪或参数无效时为原位置。
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

    // 作用：沿水平方向查询最近实时障碍，不检测场地边界；返回：命中有效障碍为真并输出 hit，否则为假。
    public bool TrySweepObstacle(Vector3 origin, Vector3 direction, float distance, float radius,
        Collider self, out RaycastHit hit)
    {
        // 拒绝无效和零方向输入，再归一化方向以保证 distance 使用世界距离。
        hit = default(RaycastHit);
        if (!Finite(origin) || !Finite(direction) || !Finite(distance) || !Finite(radius) || distance <= 0f)
            return false;
        direction.y = 0f;
        var length = direction.magnitude;
        if (!Finite(length) || length <= Epsilon) return false;
        Physics.SyncTransforms();
        return Sweep(origin, direction / length, distance, Mathf.Max(0f, radius), self, false, out hit);
    }

    // 作用：检查两点间是否被静态障碍遮挡；返回：坐标有效且端点及连线均无静态阻挡时为真。
    public bool HasLineOfSight(Vector3 from, Vector3 to)
    {
        // 先检查端点落在障碍中的情况；动态障碍、友军及无标记地面不挡视线。
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

    // 作用：检查指定体型能否放置在目标位置；返回：导航可用、参数有效、位于边界内且无实时障碍重叠时为真。
    public bool IsFree(Vector3 position, float radius, Collider self = null)
    {
        // 占位检查包含动态障碍但排除自身，适用于出生和瞬移前复核。
        if (!Ready() || !Finite(position) || !Finite(radius)) return false;
        radius = Mathf.Max(0f, radius);
        if (!InsideBounds(position, radius)) return false;
        Physics.SyncTransforms();
        return !OverlapsObstacle(position, Mathf.Max(radius, Epsilon), self, false);
    }

    // 作用：基于静态障碍计算向目标推进的下一段方向；返回：水平单位方向，无法推进或参数无效时为零向量。
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

    // 作用：在尚未就绪时尝试重建导航；返回：重建后网格宽度大于零时为真。
    private bool Ready()
    {
        // 零宽度代表尚未成功初始化；只在此时重建，查询本身不重复烘焙已有网格。
        if (mWidth == 0) Rebuild();
        return mWidth > 0;
    }

    // 作用：记录未访问格的前驱并加入 BFS 队列；返回：无返回值。
    private void Enqueue(int parent, int node, ref int write)
    {
        // 用前驱标记去重，保证每格最多入队一次。
        if (mParents[node] >= 0) return;
        mParents[node] = parent;
        mQueue[write++] = node;
    }

    // 作用：复用或创建指定体型的静态可达网格；返回：该半径对应的占用和四邻接缓存。
    private RadiusGrid GetGrid(float radius)
    {
        RadiusGrid grid;
        if (mGrids.TryGetValue(radius, out grid)) return grid;
        // 关一只有少量体型，限制缓存规模；无每个敌人的独立整张网格。
        if (mGrids.Count >= MaxRadiusGrids) mGrids.Clear();
        grid = new RadiusGrid(mWidth * mWidth);
        // 先判定格中心能否容纳圆盘，再为整段通路无障碍的相邻格建立双向连接。
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

    // 作用：寻找可从实际位置安全接入的寻路起点；返回：优先本格，否则最近可接入格的下标，无可用格时为负一。
    private int FindStart(RadiusGrid grid, Vector3 position, float radius)
    {
        var x = Mathf.Clamp(Mathf.FloorToInt((position.x - mCenter.x + mHalfSize) / mCellSize), 0, mWidth - 1);
        var z = Mathf.Clamp(Mathf.FloorToInt((position.z - mCenter.z + mHalfSize) / mCellSize), 0, mWidth - 1);
        var cell = z * mWidth + x;
        if (grid.Free[cell] && StaticSegmentFree(position, CellPosition(cell, position.y), radius)) return cell;
        // 所在格中心可能隔着薄墙，遍历候选时必须额外检查从实际位置接入的整段净空。
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

    // 作用：将一维格子下标换算为世界中心位置；返回：使用指定高度的格中心坐标。
    private Vector3 CellPosition(int index, float y)
    {
        // 余数取列、整除取行，从场地最小角偏移半格得到中心，保持调用方指定的高度。
        return new Vector3(mCenter.x - mHalfSize + (index % mWidth + 0.5f) * mCellSize, y,
            mCenter.z - mHalfSize + (index / mWidth + 0.5f) * mCellSize);
    }

    // 作用：以静态包围盒足迹检查圆盘能否占据某点；返回：在边界内且与所有静态障碍保持净空时为真。
    private bool StaticPointFree(Vector3 point, float radius)
    {
        // 先剔除越界圆盘，再以略大于半径的净空检查障碍足迹，边缘接触也视为阻挡。
        if (!InsideBounds(point, radius)) return false;
        var clearance = radius + Epsilon;
        foreach (var bounds in mStaticBounds)
        {
            if (PointBoxDistanceSquared(point, bounds) <= clearance * clearance) return false;
        }
        return true;
    }

    // 作用：检查圆盘沿线段移动的静态净空；返回：两端在边界内且整段不碰静态障碍时为真。
    private bool StaticSegmentFree(Vector3 from, Vector3 to, float radius)
    {
        if (!InsideBounds(from, radius) || !InsideBounds(to, radius)) return false;
        var clearance = radius + Epsilon;
        var squared = clearance * clearance;
        // 先用投影范围排除远处障碍，再做相交和最短距离检查，避免圆盘切过矩形角。
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

    // 作用：按单轴边界收缩线段的参数交集；返回：该轴仍有交集为真，否则为假，同时更新进入与离开参数。
    private static bool ClipAxis(float origin, float delta, float min, float max, ref float enter, ref float exit)
    {
        // 平行于轴边界时只查起点范围；其余情况统一正反方向后求区间交集。
        if (Mathf.Abs(delta) <= Epsilon) return origin >= min && origin <= max;
        var first = (min - origin) / delta;
        var last = (max - origin) / delta;
        if (first > last) { var swap = first; first = last; last = swap; }
        enter = Mathf.Max(enter, first);
        exit = Mathf.Min(exit, last);
        return enter <= exit;
    }

    // 作用：计算点到包围盒 XZ 矩形足迹的最近距离；返回：水平距离平方，点在足迹内时为零。
    private static float PointBoxDistanceSquared(Vector3 point, Bounds bounds)
    {
        // 每轴只保留超出矩形范围的距离，内部轴贡献零，再求两轴距离的平方和。
        var min = bounds.min;
        var max = bounds.max;
        var x = Mathf.Max(Mathf.Max(min.x - point.x, 0f), point.x - max.x);
        var z = Mathf.Max(Mathf.Max(min.z - point.z, 0f), point.z - max.z);
        return x * x + z * z;
    }

    // 作用：计算给定 XZ 点到平面线段的最近距离；返回：最近距离平方。
    private static float PointSegmentDistanceSquared(float x, float z, Vector3 from, Vector3 to)
    {
        // 将投影参数夹在端点之间，退化线段直接按起点处理。
        var dx = to.x - from.x;
        var dz = to.z - from.z;
        var length = dx * dx + dz * dz;
        var t = length <= Epsilon * Epsilon ? 0f : Mathf.Clamp01(((x - from.x) * dx + (z - from.z) * dz) / length);
        x -= from.x + t * dx;
        z -= from.z + t * dz;
        return x * x + z * z;
    }

    // 作用：计算圆盘沿方向到 XZ 场地边界的最近接触；返回：在给定距离内碰到边界时为真，并更新距离和向内法线。
    private bool SweepBoundary(Vector3 point, Vector3 direction, float radius, ref float distance, ref Vector3 normal)
    {
        // 按半径内缩边界，分别检查 X、Z 方向并保留更早的接触面。
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

    // 作用：按半径选择球扫或射线并筛选最近有效障碍；返回：找到阻挡时为真并输出最近命中，否则为假。
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

    // 作用：查询球形范围是否与筛选后的障碍重叠；返回：存在至少一个有效障碍时为真。
    private bool OverlapsObstacle(Vector3 position, float radius, Collider self, bool staticOnly)
    {
        int count;
        // 缓冲满时扩大并重查，防止被截断结果恰好漏掉唯一有效障碍。
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

    // 作用：按启用状态、障碍标记、自身与静态限定筛选碰撞体；返回：应参与此次障碍查询时为真，否则为假。
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

    // 作用：检查以指定点为圆心的圆盘是否完全位于场地内；返回：半径不超场地且 XZ 未越界时为真。
    private bool InsideBounds(Vector3 point, float radius)
    {
        // 将四边各内缩一个半径后检查圆心，负的可用半宽表示体型已无法容纳。
        var limit = mHalfSize - radius;
        return limit >= 0f && Mathf.Abs(point.x - mCenter.x) <= limit && Mathf.Abs(point.z - mCenter.z) <= limit;
    }

    // 作用：按体型半径将位置夹在场地内；返回：保留高度的修正位置，体型过大时 XZ 收到中心。
    private Vector3 ClampToBounds(Vector3 point, float radius)
    {
        // 只修正内缩边界外的 XZ 坐标，不改高度；半径过大时可用范围收缩成中心点。
        var limit = Mathf.Max(0f, mHalfSize - radius);
        point.x = Mathf.Clamp(point.x, mCenter.x - limit, mCenter.x + limit);
        point.z = Mathf.Clamp(point.z, mCenter.z - limit, mCenter.z + limit);
        return point;
    }

    // 作用：计算两点间的水平前进方向；返回：XZ 单位向量，水平距离近零时为零向量。
    private static Vector3 FlatDirection(Vector3 from, Vector3 to)
    {
        // 去掉高度差再归一化，近乎重合时返回零向量以避免无意义的微小方向。
        var offset = to - from;
        offset.y = 0f;
        return offset.sqrMagnitude <= Epsilon * Epsilon ? Vector3.zero : offset.normalized;
    }

    // 作用：检查标量是否为有限数；返回：既非 NaN 也非正负无穷时为真。
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value); // 分别排除未定义值与无穷值，不限制正负或大小。
    // 作用：检查向量各分量是否均为有限数；返回：三个分量全部有限时为真。
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z); // 逐轴复用标量检查，任意轴无效即短路返回假。
}
