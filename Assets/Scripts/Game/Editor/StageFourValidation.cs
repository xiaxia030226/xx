#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QFramework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StageFourValidation
{
    private const string ResourceMenu = "Game/阶段四/校验资源（只读）"; // 只读资源检查菜单路径。
    private const string LogicMenu = "Game/阶段四/运行逻辑断言"; // 隔离逻辑断言菜单路径。
    private const string BulletPrefabPath = "Prefabs/Bullet"; // 逻辑断言只读依赖的子弹预制体 Resources 路径。

    // 作用：只读检查冲锋枪、应急补给配置和环境静态装配，不等同完整 Play 验收；返回：无返回值。
    [MenuItem(ResourceMenu)]
    public static void ValidateResources()
    {
        // 只读取正式资源与字段，不实例化场地、不保存或修复资产。
        Guard();
        var report = new Report();
        report.Check("G3 SMG：S/30/1.8/8/720/160", () =>
        {
            var gun = Resources.LoadAll<WeaponConfig>("Configs/Weapons").Single(c => c.Id == "smg");
            Require(gun.Caliber == Caliber.S && gun.Magazine == 30 && gun.IsAutomatic, "SMG 口径/容量/自动模式");
            Near(gun.ReloadTime, 1.8f); Near(gun.Damage, 8f); Near(gun.RoundsPerMinute, 720f); Near(gun.DurabilityMax, 160f);
        });
        report.Check("Stage1：三种枪与应急 Supply 机枪/AR 0×40、1×20", () =>
        {
            var stage = StageOne();
            Require(stage.WeaponIds != null && stage.WeaponIds.OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(new[] { "machinegun", "pistol", "smg" }), "枪池必须恰好三种");
            Require(stage.EmergencyWeaponId == "machinegun" && stage.EmergencyAmmoLevel0 == 40
                && stage.EmergencyAmmoLevel1 == 20, "Stage1 应急补给配置");
            Require(Resources.LoadAll<WeaponConfig>("Configs/Weapons").Single(c => c.Id == stage.EmergencyWeaponId)
                .Caliber == Caliber.AR, "应急机枪应使用 AR");
        });
        report.Check("环境：箱引用/子层级/视觉与出生点范围", () =>
        {
            var path = StageOne().EnvironmentPath;
            Require(!string.IsNullOrWhiteSpace(path), "环境路径为空");
            var prefab = Resources.Load<GameObject>(path);
            Require(prefab != null && AssetDatabase.GetAssetPath(prefab).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase), "环境 prefab 缺失");
            var environment = prefab.GetComponent<StageEnvironment>();
            Require(environment != null, "根节点 StageEnvironment 缺失");
            var crate = environment.EmergencySupplyCrate;
            Require(crate != null && crate.transform != environment.transform && crate.transform.IsChildOf(environment.transform), "箱必须绑定环境子节点");
            foreach (var name in new[] { "mClosedVisual", "mOpenedVisual" })
            {
                var visual = Get<GameObject>(crate, name);
                Require(visual == null || (visual.transform != crate.transform && visual.transform.IsChildOf(crate.transform)), name + " 只能为空或箱子节点");
            }
            var closed = Get<GameObject>(crate, "mClosedVisual");
            var opened = Get<GameObject>(crate, "mOpenedVisual");
            Require(closed == null || opened == null ||
                (!closed.transform.IsChildOf(opened.transform) && !opened.transform.IsChildOf(closed.transform)), "两种外观不能重叠或互为父子");
            Require(crate.enabled && crate.gameObject.activeSelf, "箱子组件与根对象必须启用");
            Require(FinitePositive(environment.HalfSize) && FinitePositive(crate.OpenRadius), "场地尺寸/触发半径非法");
            var spawn = new Vector3(0f, 1f, 0f);
            foreach (var point in new[] { spawn, crate.transform.position })
            {
                var offset = point - environment.transform.position;
                Require(Mathf.Abs(offset.x) < environment.HalfSize && Mathf.Abs(offset.z) < environment.HalfSize, "出生点或箱在场外");
            }
            var distance = spawn - crate.transform.position; distance.y = 0f;
            Require(distance.sqrMagnitude > crate.OpenRadius * crate.OpenRadius, "出生点 (0,1,0) 位于开启范围内");
        });
        Debug.Log("[阶段四][INFO] 只读 Resources/AssetDatabase；未实例化场地、保存资产或执行导航/视觉验收。");
        report.Finish();
    }

    // 作用：使用临时配置和独立架构执行枪械、弹源与补给箱逻辑案例；返回：无返回值。
    [MenuItem(LogicMenu)]
    public static void RunLogicAssertions()
    {
        // 外层配置作用域负责还原静态表；每条案例另建夹具，隔离库存、事件和临时对象。
        Guard();
        var report = new Report();
        Debug.Log("[阶段四][INFO] 隔离架构；EnsureBulletPool 只读依赖 Resources/" + BulletPrefabPath
            + " 根节点 Bullet。临时池不执行预热工厂；不调用 Bullet/拾取物 Update，不代表碰撞或自动拾取验收。");
        report.Check("隔离配置与逻辑检查生命周期", () =>
        {
            using (var configs = new MemoryConfigs())
            {
                Case(report, "初始仅 Supply 手枪，首装包含在总量60内", f =>
                {
                    Require(f.System.Weapons.Count == 1 && f.Gun.Id == "pistol" && f.Gun.Origin == ItemOrigin.Supply, "初始枪");
                    f.Count(48, 0); Loaded(f.Gun, 0, 0); Batch(f.Gun.PendingAmmo, 12, 0);
                    foreach (var caliber in new[] { Caliber.S, Caliber.AR, Caliber.L })
                    for (var level = 0; level < 6; level++)
                        if (caliber != Caliber.S || level != 0) Batch(f.Inventory.GetAmmo(caliber, level), 0, 0);
                    f.System.Tick(1.51f); Loaded(f.Gun, 12, 0); Batch(f.Gun.PendingAmmo, 0, 0); f.Count(48, 0);
                });
                Case(report, "Supply10+Loot10，装12射5换级退回为Supply5+Loot10", f =>
                {
                    f.Empty(); f.Seed(10, 10); Load(f.Gun); Loaded(f.Gun, 10, 2); f.Count(0, 8);
                    f.Shoot(f.Gun, 5); Loaded(f.Gun, 5, 2);
                    f.Gun.CycleNextLoadLevel(); f.Gun.RequestReload();
                    Loaded(f.Gun, 0, 0); f.Count(5, 10); Batch(f.Gun.PendingAmmo, 0, 0);
                });
                Case(report, "同级混合pending、B不改预扣、取消幂等", ValidatePending);
                Case(report, "枪来源与弹来源反向独立，卸夹清零且幂等", f =>
                {
                    f.Empty(); var supplyGun = f.Gun;
                    Require(f.System.TryPickupWeapon("pistol", ItemOrigin.Loot), "拾取 Loot 枪");
                    var lootGun = (GunWeapon)f.System.Weapons[1];
                    f.Seed(0, 12); Load(supplyGun); f.Seed(12, 0); Load(lootGun);
                    f.Shoot(supplyGun, 2); f.Shoot(lootGun, 2);
                    Require(supplyGun.Origin == ItemOrigin.Supply && lootGun.Origin == ItemOrigin.Loot, "枪来源被弹源覆盖");
                    Loaded(supplyGun, 0, 10); Loaded(lootGun, 10, 0);
                    Batch(supplyGun.UnloadMagazine(), 0, 10); Batch(lootGun.UnloadMagazine(), 10, 0);
                    Loaded(supplyGun, 0, 0); Loaded(lootGun, 0, 0); Batch(lootGun.UnloadMagazine(), 0, 0);
                });
                Case(report, "真实系统报废：余弹事件/pending返库/先腾槽/不重复/不切枪", ValidateBreak);
                Case(report, "耐久最后一发仍生成弹丸，不随报废消失", f =>
                {
                    f.System.Tick(2f); var gun = f.Gun;
                    Require(f.System.TryPickupWeapon("smg", ItemOrigin.Loot), "备用枪");
                    gun.Wear(gun.Durability - 1f);
                    Require(f.System.TryAttackCurrent(), "最后一发未射出");
                    Require(f.Pool.Spawned.Count == 1 && Get<bool>(f.Pool.Spawned[0].GetComponent<Bullet>(), "mFlying"), "最后弹丸被取消");
                    Require(f.System.CurrentWeapon == null && f.System.CurrentIndex == 0 && f.System.Weapons[1] != null, "报废后自动切枪");
                    Require(f.Drops.Count == 1 && f.Broken.Count == 1, "报废事件次数"); Batch(f.Drops[0].Ammo, 11, 0);
                    Loaded(gun, 0, 0); f.Count(48, 0); f.System.Tick(10f);
                    Require(!f.System.TryAttackCurrent() && f.Drops.Count == 1 && f.Broken.Count == 1, "空槽重复报废");
                    Require(Get<bool>(f.Pool.Spawned[0].GetComponent<Bullet>(), "mFlying"), "Tick 清除了最后一发");
                });
                Case(report, "九栏满拒收，两个空槽优先填最小下标", ValidateSlots);
                Case(report, "同一系统 Reset+Setup 不把旧pending退入新库存", f =>
                {
                    var old = f.Gun; Batch(old.PendingAmmo, 12, 0);
                    f.Inventory.Reset(); f.System.Setup(f.Owner, f.Root.transform);
                    Require(f.System.Weapons.Count == 1 && !ReferenceEquals(old, f.Gun), "未重建枪栏");
                    f.Count(48, 0); Batch(f.Gun.PendingAmmo, 12, 0); f.System.Tick(2f); f.Count(48, 0); Loaded(f.Gun, 12, 0);
                });
                Case(report, "Pause：系统不推进装填/冷却、不射击", ValidatePause);
                Case(report, "打空只自动补弹，后台部分装填保留来源且暂停冻结", f =>
                {
                    f.Empty(); f.Seed(2, 14); var gun = f.Gun; Load(gun);
                    Require(f.System.TryPickupWeapon("smg", ItemOrigin.Loot), "后台测试备用枪");
                    f.Shoot(gun, 12); Loaded(gun, 0, 0); Batch(gun.PendingAmmo, 0, 4); f.Count(0, 0);
                    Require(gun.IsReloading && f.System.CurrentIndex == 0, "打空未换弹或发生自动切枪");
                    f.System.SwitchTo(1); f.State.State.Value = GameState.Paused; f.System.Tick(10f);
                    Loaded(gun, 0, 0); Batch(gun.PendingAmmo, 0, 4);
                    f.State.State.Value = GameState.Playing; f.System.Tick(1.51f);
                    Loaded(gun, 0, 4); Batch(gun.PendingAmmo, 0, 0);
                    Require(!gun.IsReloading && f.System.CurrentIndex == 1 && f.Gun.Resource == 0f, "后台装填串枪或切枪");
                });
                Case(report, "手枪0.25s/机枪0.1s/G3 1÷12s真实开火门控", ValidateIntervals);
                Case(report, "箱：距离/状态/重复/满栏/新局重置/三份Supply产物", f => ValidateCrate(f, configs.Stage));
                Case(report, "拾取组件二次OnSpawn覆盖来源与目标", ValidatePickupReuse);
            }
        });
        report.Finish();
    }

    // 作用：验证混合来源预扣、切换下次等级以及取消装填幂等；返回：无返回值。
    private static void ValidatePending(Fixture f)
    {
        // 先保留 Loot 余弹再混合预扣；切级只影响下一次，取消必须按原来源退回。
        f.Empty(); f.Seed(0, 4); Load(f.Gun); f.Seed(3, 7); f.Gun.RequestReload();
        Loaded(f.Gun, 0, 4); Batch(f.Gun.PendingAmmo, 3, 5); f.Count(0, 2);
        f.Gun.RequestReload(); f.Gun.CycleNextLoadLevel(); Batch(f.Gun.PendingAmmo, 3, 5); f.Count(0, 2);
        f.Gun.RefundPendingLoad(); f.Gun.RefundPendingLoad(); f.Gun.Tick(10f);
        Loaded(f.Gun, 0, 4); Batch(f.Gun.PendingAmmo, 0, 0); f.Count(3, 7); f.Count(0, 0, 1);
        Require(!f.Gun.IsReloading && f.Gun.NextLoadLevel == 1, "取消改变下次选择或未结束");
        for (var i = 0; i < 5; i++) f.Gun.CycleNextLoadLevel();
        f.Gun.RequestReload(); f.Gun.CycleNextLoadLevel(); f.Gun.Tick(2f);
        Loaded(f.Gun, 3, 9); Batch(f.Gun.PendingAmmo, 0, 0); f.Count(0, 2);
        Require(f.Gun.LoadedLevel == 0 && f.Gun.NextLoadLevel == 1, "B 改写本次预扣等级");
    }

    // 作用：验证报废时腾槽、退预扣、卸夹掉落的顺序与事件幂等；返回：无返回值。
    private static void ValidateBreak(Fixture f)
    {
        // 监听回调发生瞬间的槽位与弹夹状态，不只检查操作后的最终数量。
        f.Empty(); f.Seed(10, 10); Load(f.Gun); var gun = f.Gun; f.Shoot(gun, 5);
        Require(f.System.TryPickupWeapon("smg", ItemOrigin.Loot), "备用枪");
        f.System.RequestReloadCurrent(); Loaded(gun, 5, 2); Batch(gun.PendingAmmo, 0, 5); f.Count(0, 3);
        var emptyOnRefund = false; var clearedOnDrop = false;
        f.Listen<BulletInventoryChangedEvent>(_ => emptyOnRefund = f.System.Weapons[0] == null);
        f.Listen<WeaponAmmoDroppedEvent>(_ => clearedOnDrop = f.System.Weapons[0] == null && gun.LoadedAmmo.Count == 0 && gun.PendingAmmo.Count == 0);
        gun.Wear(100f); f.System.Tick(0f);
        Require(emptyOnRefund && clearedOnDrop, "必须先腾槽，退pending，清夹再发掉落事件");
        Require(f.System.CurrentIndex == 0 && f.System.CurrentWeapon == null && f.System.Weapons[1] != null, "不得自动切枪");
        f.Count(0, 8); Loaded(gun, 0, 0); Batch(gun.PendingAmmo, 0, 0);
        Require(f.Drops.Count == 1 && f.Broken.Count == 1 && f.Broken[0].SlotIndex == 0, "应各发送一次报废/余弹事件");
        var drop = f.Drops[0]; Batch(drop.Ammo, 5, 2);
        Require(drop.Caliber == Caliber.S && drop.Level == 0, "掉落桶错误"); Near((drop.Position - f.Owner.position).sqrMagnitude, 0f);
        gun.RefundPendingLoad(); f.System.CancelReloads(); f.System.Tick(10f);
        Require(!f.System.TryAttackCurrent() && f.Drops.Count == 1 && f.Broken.Count == 1, "重复退弹或掉落"); f.Count(0, 8);
    }

    // 作用：验证九栏满时拒收、报废空槽优先补最小下标且不切枪；返回：无返回值。
    private static void ValidateSlots(Fixture f)
    {
        // 保存满栏快照证明拒收不改变列表，再制造两个空槽检查补位顺序。
        for (var i = 1; i < 9; i++) Require(f.System.TryPickupWeapon(i % 2 == 0 ? "smg" : "machinegun", ItemOrigin.Loot), "填槽失败");
        var before = f.System.Weapons.ToArray();
        Require(!f.System.TryPickupWeapon("pistol", ItemOrigin.Supply) && f.System.Weapons.SequenceEqual(before), "满栏改变了枪栏");
        f.Count(48, 0); Batch(f.Gun.PendingAmmo, 12, 0);
        f.System.Weapons[3].Wear(1000f); f.System.Weapons[7].Wear(1000f); f.System.Tick(0f);
        Require(f.System.TryPickupWeapon("smg", ItemOrigin.Supply), "空槽未接收");
        Require(f.System.Weapons.Count == 9 && f.System.Weapons[3].Id == "smg" && f.System.Weapons[3].SlotIndex == 3
            && ((GunWeapon)f.System.Weapons[3]).Origin == ItemOrigin.Supply && f.System.Weapons[7] == null && f.System.CurrentIndex == 0, "未优先填较小空槽");
        Require(f.Drops.Count == 0, "空夹报废不应掉弹");
    }

    // 作用：验证暂停时装填、冷却与射击输入冻结，恢复后继续计时；返回：无返回值。
    private static void ValidatePause(Fixture f)
    {
        // 分别暂停在装填期与开火冷却期，大步 Tick 也不得消耗剩余时间。
        f.State.State.Value = GameState.Paused; f.System.Tick(100f); f.System.RequestReloadCurrent(); f.System.CycleNextLoadLevelCurrent();
        Loaded(f.Gun, 0, 0); Batch(f.Gun.PendingAmmo, 12, 0); f.Count(48, 0);
        Require(!f.System.TryAttackCurrent() && f.Pool.Spawned.Count == 0 && f.Gun.NextLoadLevel == 0, "暂停仍处理输入");
        f.State.State.Value = GameState.Playing; f.System.Tick(2f); Require(f.System.TryAttackCurrent(), "恢复后无法开火");
        f.State.State.Value = GameState.Paused; f.System.Tick(100f);
        Require(!f.Gun.CanAttack && !f.System.TryAttackCurrent() && f.Pool.Spawned.Count == 1, "暂停推进冷却或射击");
        Loaded(f.Gun, 11, 0); f.State.State.Value = GameState.Playing;
        Require(!f.System.TryAttackCurrent(), "恢复跳过剩余冷却"); f.System.Tick(0.26f); Require(f.System.TryAttackCurrent(), "恢复后冷却不推进");
    }

    // 作用：验证三种枪的开火冷却边界和真实 Bullet.Setup 载荷；返回：无返回值。
    private static void ValidateIntervals(Fixture f)
    {
        // 在间隔前后手动 Tick 并统计临时弹丸；反射只读载荷，不执行弹丸 Update 或碰撞。
        f.Empty(); var ids = new[] { "pistol", "machinegun", "smg" }; var intervals = new[] { 0.25f, 0.1f, 1f / 12f };
        for (var i = 0; i < ids.Length; i++)
        {
            var config = WeaponConfigTable.Get(ids[i]); Require(f.System.TryPickupWeapon(ids[i], ItemOrigin.Loot), "射速测试拾取 " + ids[i]);
            var gun = (GunWeapon)f.System.Weapons.Last();
            f.Seed(config.Magazine, 0, 0, config.Caliber); Load(gun); Near(gun.AttackInterval, intervals[i]);
            var count = f.Pool.Spawned.Count;
            Require(gun.TryAttack(f.Owner) && !gun.TryAttack(f.Owner), ids[i] + " 即时冷却");
            gun.Tick(intervals[i] - 0.001f); Require(!gun.TryAttack(f.Owner), ids[i] + " 提前射击");
            gun.Tick(0.002f); Require(gun.TryAttack(f.Owner) && f.Pool.Spawned.Count == count + 2, ids[i] + " 间隔后射击");
            var bullet = f.Pool.Spawned.Last().GetComponent<Bullet>(); var hit = Get<DamageInfo>(bullet, "mHit");
            Require(hit.Faction == CombatFaction.Player && hit.PenetrationLevel == 0, "真实 Bullet.Setup 载荷");
            Near(hit.Amount, config.Damage); Near(Get<float>(bullet, "mSpeed"), 30f);
            Near((Get<Vector3>(bullet, "mDirection") - f.Owner.forward).sqrMagnitude, 0f);
        }
    }

    // 作用：验证补给箱距离与状态门控、重复开启、重置以及三份 Supply 产物；返回：无返回值。
    private static void ValidateCrate(Fixture f, StageConfig stage)
    {
        // 只手动调用箱逻辑，禁用自动更新；用临时外观检查显隐状态，不代替实际视觉验收。
        var crate = Temp("crate", f.Root.transform).AddComponent<EmergencySupplyCrate>(); crate.enabled = false; crate.gameObject.SetActive(true);
        var closed = Temp("closed", crate.transform); var opened = Temp("opened", crate.transform);
        Set(crate, "mClosedVisual", closed); Set(crate, "mOpenedVisual", opened);
        crate.transform.position = new Vector3(6f, 0f, 0f); crate.Initialize(stage, f.Owner, f.Architecture, f.Root.transform);
        Require(!crate.IsOpened && closed.activeSelf && !opened.activeSelf && !crate.TryOpen(), "远处或初始视觉");
        f.State.State.Value = GameState.SafeLoot; Require(!crate.TryOpen(), "SafeLoot 远处开箱");
        f.Owner.position = crate.transform.position + Vector3.up;
        f.State.State.Value = GameState.Paused; Require(!crate.TryOpen() && f.Pool.Spawned.Count == 0 && !crate.IsOpened, "Pause 开箱");
        foreach (var state in new[] { GameState.Playing, GameState.SafeLoot, GameState.Playing })
        {
            if (f.Pool.Spawned.Count == 6)
                for (var i = 1; i < 9; i++) Require(f.System.TryPickupWeapon("pistol", ItemOrigin.Loot), "准备满栏");
            crate.Initialize(stage, f.Owner, f.Architecture, f.Root.transform);
            Require(!crate.IsOpened && closed.activeSelf && !opened.activeSelf, "Initialize 未重置箱");
            f.State.State.Value = state; var start = f.Pool.Spawned.Count;
            Require(crate.TryOpen() && crate.IsOpened && !closed.activeSelf && opened.activeSelf && crate.gameObject.activeSelf, "近处状态或视觉错误");
            Require(!crate.TryOpen() && f.Pool.Spawned.Count == start + 3, "重复吐出或产物不是三份");
            var weapon = f.Pool.Spawned[start].GetComponent<WeaponPickup>();
            Require(Get<string>(weapon, "mWeaponId") == "machinegun" && Get<ItemOrigin>(weapon, "mOrigin") == ItemOrigin.Supply, "箱枪来源");
            Require(Get<Transform>(weapon, "mTarget") == f.Owner && weapon.transform.parent == f.Root.transform, "箱枪目标/挂载点");
            for (var level = 0; level < 2; level++)
            {
                var pickup = f.Pool.Spawned[start + level + 1].GetComponent<AmmoPackPickup>();
                Require(Get<Caliber>(pickup, "mCaliber") == Caliber.AR && Get<int>(pickup, "mLevel") == level, "箱弹口径/等级");
                Batch(Get<AmmoBatch>(pickup, "mAmmo"), level == 0 ? 40 : 20, 0);
                Require(Get<Transform>(pickup, "mTarget") == f.Owner && pickup.transform.parent == f.Root.transform, "箱弹目标/挂载点");
            }
            if (f.System.Weapons.Count == 9)
                Require(!f.System.TryPickupWeapon(Get<string>(weapon, "mWeaponId"), Get<ItemOrigin>(weapon, "mOrigin")) && weapon != null, "满栏箱枪应留作拾取物");
        }
    }

    // 作用：验证拾取组件重复 OnSpawn 时覆盖弹药、武器来源及目标引用；返回：无返回值。
    private static void ValidatePickupReuse(Fixture f)
    {
        // 对同一临时组件连续赋不同出生数据，反射核查旧数据未残留；不执行自动拾取 Update。
        var target = Temp("secondTarget", f.Root.transform).transform;
        var ammo = f.Pool.Spawn(AmmoPackPickup.PoolKey, Vector3.zero, Quaternion.identity, f.Root.transform).GetComponent<AmmoPackPickup>();
        ammo.OnSpawn(Caliber.AR, 0, AmmoBatch.Supply(40), f.Owner); Batch(Get<AmmoBatch>(ammo, "mAmmo"), 40, 0);
        ammo.OnSpawn(Caliber.S, 1, AmmoBatch.Loot(7), target); Batch(Get<AmmoBatch>(ammo, "mAmmo"), 0, 7);
        Require(Get<Caliber>(ammo, "mCaliber") == Caliber.S && Get<int>(ammo, "mLevel") == 1 && Get<Transform>(ammo, "mTarget") == target, "弹包残留上次信息");
        ammo.OnSpawn(Caliber.L, 2, new AmmoBatch(5, 2), f.Owner); Batch(Get<AmmoBatch>(ammo, "mAmmo"), 2, 3);
        var weapon = f.Pool.Spawn(WeaponPickup.PoolKey, Vector3.zero, Quaternion.identity, f.Root.transform).GetComponent<WeaponPickup>();
        weapon.OnSpawn("machinegun", ItemOrigin.Supply, f.Owner); Require(Get<ItemOrigin>(weapon, "mOrigin") == ItemOrigin.Supply, "首次枪源");
        weapon.OnSpawn("pistol", ItemOrigin.Loot, target);
        Require(Get<ItemOrigin>(weapon, "mOrigin") == ItemOrigin.Loot && Get<string>(weapon, "mWeaponId") == "pistol" && Get<Transform>(weapon, "mTarget") == target, "枪拾取物残留上次来源");
        weapon.OnSpawn("smg", ItemOrigin.Supply, f.Owner); Require(Get<ItemOrigin>(weapon, "mOrigin") == ItemOrigin.Supply, "反向复用来源未覆盖");
    }

    // 独立泛型架构只服务本文件测试；不访问正式游戏架构，夹具结束时 Deinit 重置实例。
    private sealed class TestArchitecture : Architecture<TestArchitecture>
    {
        // 作用：提供独立测试架构的空构造入口，注册延后到 Init；返回：无返回值（构造函数）。
        public TestArchitecture()
        {
            // 依赖注册统一放在架构初始化钩子，构造时不提前执行。
        }
        // 作用：注册测试所需库存、游戏状态、临时池及真实武器系统；返回：无返回值。
        protected override void Init()
        {
            // 先备齐独立模型和临时池，再注册依赖它们的真实武器系统。
            RegisterModel<IBulletInventoryModel>(new BulletInventoryModel());
            RegisterModel<IGameStateModel>(new GameStateModel());
            RegisterSystem<IGameObjectPoolSystem>(new TestPool());
            RegisterSystem<IWeaponSystem>(new WeaponSystem());
        }
    }

    private sealed class TestPool : AbstractSystem, IGameObjectPoolSystem
    {
        public readonly List<GameObject> Spawned = new List<GameObject>(); // 本夹具创建的临时对象，供断言计数与最终清理。
        private readonly HashSet<string> mKeys = new HashSet<string>(); // 已注册的池键，仅验证调用契约，不存储工厂或缓存。
        // 作用：保留空初始化入口，不创建对象或预热；返回：无返回值。
        protected override void OnInit()
        {
            // 替身只在显式 Spawn 时创建临时对象，初始化不产生额外实例。
        }
        // 作用：直接登记池键，忽略 factory 与 initialCount，不调用预热工厂；返回：无返回值。
        public void Register(string key, Func<GameObject> factory, int initialCount = 0) => mKeys.Add(key); // 只保留注册凭据，不触发工厂和预热副作用。
        // 作用：检查池键已注册并提供无缓存的替身结果；返回：固定为 0，未注册时抛出异常。
        public int GetCachedCount(string key)
        {
            // 先验证调用使用已登记的池键，再以零缓存维持替身契约。
            Require(mKeys.Contains(key), "未注册测试池 " + key); return 0;
        }
        // 作用：按池键创建禁用自动更新的临时组件对象；返回：新建的非激活 GameObject。
        public GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            // 不实例化正式 prefab，也不调用注册的工厂；组件不执行 Update，只供手动逻辑断言。
            GetCachedCount(key); var go = Temp(key, parent); Spawned.Add(go); go.transform.SetPositionAndRotation(position, rotation);
            if (key == AmmoPackPickup.PoolKey) go.AddComponent<AmmoPackPickup>().enabled = false;
            else if (key == WeaponPickup.PoolKey) go.AddComponent<WeaponPickup>().enabled = false;
            else if (new[] { Caliber.S, Caliber.AR, Caliber.L }.Any(c => AmmoTypes.PoolKey(c) == key)) go.AddComponent<Bullet>().enabled = false;
            else throw new InvalidOperationException("未知测试池 " + key);
            return go;
        }
        // 作用：验证池键并隐藏实例，不放入真实复用缓存；返回：无返回值。
        public void Recycle(string key, GameObject instance)
        {
            // 先核实池键，再仅停用仍存在的对象，保留记录供最终销毁。
            GetCachedCount(key); if (instance != null) instance.SetActive(false);
        }
        // 作用：销毁所有已创建临时对象并清空记录和池键；返回：无返回值。
        public void ClearAll()
        {
            // 先销毁记录中的存活对象，再清空实例与注册信息以结束夹具生命周期。
            foreach (var go in Spawned) if (go != null) Object.DestroyImmediate(go); Spawned.Clear(); mKeys.Clear();
        }
        // 作用：在系统反初始化时直接委托 ClearAll 清理临时对象；返回：无返回值。
        protected override void OnDeinit() => ClearAll(); // 复用集中清理入口，避免架构退出后遗留临时对象。
    }

    private sealed class Fixture : IDisposable
    {
        public IArchitecture Architecture; // 本条案例的独立测试架构。
        public GameObject Root; // 承载本案例临时节点的根对象，结束时销毁。
        public Transform Owner; // 提供射击方向、位置和拾取目标的临时持有者。
        public IWeaponSystem System; // 被测的真实武器系统。
        public IBulletInventoryModel Inventory; // 独立架构内按口径、等级和来源管理的弹药库存。
        public IGameStateModel State; // 控制播放、暂停与安全拾取状态的测试模型。
        public TestPool Pool; // 不执行工厂预热或 Update 的临时对象池替身。
        public GunWeapon Gun => (GunWeapon)System.CurrentWeapon; // 当前选中枪械，直接读取武器系统；空槽为 null。
        public readonly List<WeaponAmmoDroppedEvent> Drops = new List<WeaponAmmoDroppedEvent>(); // 捕获的报废余弹掉落事件。
        public readonly List<WeaponBrokenEvent> Broken = new List<WeaponBrokenEvent>(); // 捕获的武器报废事件。
        private readonly List<IUnRegister> mListeners = new List<IUnRegister>(); // 本案例事件订阅的注销句柄。
        // 作用：创建临时持有者、独立架构与初始武器系统并监听事件；返回：无返回值（构造函数）。
        public Fixture()
        {
            try
            {
                // 所有节点由本夹具拥有，初始化失败也走 Dispose，避免留下架构实例或订阅。
                Root = Temp("StageFourValidation", null); Owner = Temp("owner", Root.transform).transform; Owner.position = new Vector3(0f, 1f, 0f);
                Architecture = TestArchitecture.Interface; Inventory = Architecture.GetModel<IBulletInventoryModel>(); State = Architecture.GetModel<IGameStateModel>();
                System = Architecture.GetSystem<IWeaponSystem>(); Pool = (TestPool)Architecture.GetSystem<IGameObjectPoolSystem>();
                Pool.Register(AmmoPackPickup.PoolKey, () => null); Pool.Register(WeaponPickup.PoolKey, () => null);
                State.State.Value = GameState.Playing; Listen<WeaponAmmoDroppedEvent>(Drops.Add); Listen<WeaponBrokenEvent>(Broken.Add);
                System.Setup(Owner, Root.transform);
            }
            catch { Dispose(); throw; }
        }
        // 作用：直接向独立架构注册监听并保存注销句柄；返回：无返回值。
        public void Listen<T>(Action<T> listener) => mListeners.Add(Architecture.RegisterEvent(listener)); // 注册后立即收集注销句柄，确保案例结束可统一退订。
        // 作用：取消装填后清空全部库存桶，不卸除已装入弹夹的弹药；返回：无返回值。
        public void Empty()
        {
            // 先退回 pending 再清库存，避免取消操作给已清空的桶重新添弹。
            System.CancelReloads();
            foreach (var caliber in new[] { Caliber.S, Caliber.AR, Caliber.L })
            for (var level = 0; level < 6; level++) Architecture.SendCommand(new TakeBulletsCommand(caliber, level, Inventory.GetCount(caliber, level)));
        }
        // 作用：通过真实命令向指定口径等级分别加入 Supply 与 Loot 弹药；返回：无返回值。
        public void Seed(int supply, int loot, int level = 0, Caliber caliber = Caliber.S)
        {
            // 将两种来源分批送入同一库存桶，保留可用于来源守恒断言的份额。
            Architecture.SendCommand(new AddBulletsCommand(caliber, level, AmmoBatch.Supply(supply)));
            Architecture.SendCommand(new AddBulletsCommand(caliber, level, AmmoBatch.Loot(loot)));
        }
        // 作用：断言 S 口径指定等级的来源份额与库存总量；返回：无返回值。
        public void Count(int supply, int loot, int level = 0)
        {
            // 先核对批次来源拆分，再用份额之和交叉检查库存总量接口。
            Batch(Inventory.GetAmmo(Caliber.S, level), supply, loot); Require(Inventory.GetCount(Caliber.S, level) == supply + loot, "GetCount 与批次不一致");
        }
        // 作用：逐发推进冷却并调用真实枪械攻击逻辑，不运行弹丸 Update；返回：无返回值。
        public void Shoot(GunWeapon gun, int count)
        {
            // 每发先推进略超开火间隔的时间，再确认真实攻击成功以准确消耗弹药。
            for (var i = 0; i < count; i++) { gun.Tick(gun.AttackInterval + 0.001f); Require(gun.TryAttack(Owner), "真实枪开火失败"); }
        }
        // 作用：注销监听、反初始化独立架构并销毁临时根节点；返回：无返回值。
        public void Dispose()
        {
            // 分层 finally 确保前序清理失败也继续释放架构和 HideAndDontSave 节点。
            try { foreach (var listener in mListeners) listener.UnRegister(); }
            finally
            {
                try { Architecture?.Deinit(); }
                finally { Architecture = null; if (Root != null) Object.DestroyImmediate(Root); }
            }
        }
    }

    // 用内存配置暂时替换两张静态表；快照恢复后才销毁自有对象，绝不把测试数据写回资产。
    private sealed class MemoryConfigs : IDisposable
    {
        private readonly FieldInfo mWeapons = Field(typeof(WeaponConfigTable), "sConfigs"), mBullets = Field(typeof(BulletConfigTable), "sConfigs"); // mWeapons：武器静态缓存字段；mBullets：子弹静态缓存字段。
        private readonly object mOldWeapons, mOldBullets; // mOldWeapons：原武器表引用快照；mOldBullets：原子弹表引用快照，均允许为 null。
        private readonly List<Object> mOwned = new List<Object>(); // 本作用域拥有的 HideAndDontSave 配置，结束时逐个销毁。
        public StageConfig Stage; // 用于补给箱断言的临时关卡默认配置。
        // 作用：快照静态表、创建内存配置并临时替换缓存供逻辑测试使用；返回：无返回值（构造函数）。
        public MemoryConfigs()
        {
            // 保存原引用（包括尚未加载时的 null），不加载正式表来伪造恢复值。
            mOldWeapons = mWeapons.GetValue(null); mOldBullets = mBullets.GetValue(null);
            try
            {
                // 正式子弹 prefab 仅检查根组件；后续 TestPool 不调用它的实例化工厂。
                var prefab = Resources.Load<GameObject>(BulletPrefabPath);
                Require(prefab != null && prefab.GetComponent<Bullet>() != null, "只读依赖 Resources/" + BulletPrefabPath + " 缺失根节点Bullet");
                var weapons = new Dictionary<string, WeaponConfig>();
                var ids = new[] { "pistol", "machinegun", "smg" }; var magazines = new[] { 12, 50, 30 };
                for (var i = 0; i < ids.Length; i++)
                {
                    var c = Create<WeaponConfig>(); Set(c, "mId", ids[i]); Set(c, "mName", ids[i]); Set(c, "mCaliber", i == 1 ? Caliber.AR : Caliber.S);
                    Set(c, "mMagazine", magazines[i]); Set(c, "mReloadTime", new[] { 1.5f, 2.5f, 1.8f }[i]); Set(c, "mDamage", new[] { 10f, 6f, 8f }[i]);
                    Set(c, "mDurabilityMax", new[] { 100f, 240f, 160f }[i]); Set(c, "mRoundsPerMinute", new[] { 0f, 600f, 720f }[i]); Set(c, "mIsAutomatic", i != 0);
                    Near(c.SemiAutoInterval, 0.25f); weapons.Add(c.Id, c);
                }
                var bullets = new Dictionary<string, BulletConfig>();
                foreach (var caliber in new[] { Caliber.S, Caliber.AR, Caliber.L })
                for (var level = 0; level < 6; level++)
                {
                    var c = Create<BulletConfig>(); var id = "bullet_" + caliber.ToString().ToLowerInvariant() + "_" + level;
                    Set(c, "mId", id); Set(c, "mName", id); Set(c, "mCaliber", caliber); Set(c, "mPenetrationLevel", level);
                    Set(c, "mSpeed", 30f); Set(c, "mPrefabPath", BulletPrefabPath); bullets.Add(id, c);
                }
                Stage = Create<StageConfig>();
                Require(Stage.EmergencyWeaponId == "machinegun" && Stage.EmergencyAmmoLevel0 == 40 && Stage.EmergencyAmmoLevel1 == 20, "应急配置新建缺省不符");
                // 全部内存数据就绪后替换静态引用；若构造失败，同样恢复快照并释放已创建配置。
                mWeapons.SetValue(null, weapons); mBullets.SetValue(null, bullets);
            }
            catch { Dispose(); throw; }
        }
        // 作用：创建隐藏且不保存的配置并登记所有权，生命周期由 Dispose 显式结束；返回：新建的临时配置。
        private T Create<T>() where T : ScriptableObject
        {
            // 为内存配置设置不保存标记并登记所有权，确保作用域退出时能逐一销毁。
            var value = ScriptableObject.CreateInstance<T>(); value.hideFlags = HideFlags.HideAndDontSave; mOwned.Add(value); return value;
        }
        // 作用：恢复原武器与子弹静态表引用，再销毁全部自有内存配置；返回：无返回值。
        public void Dispose()
        {
            // 嵌套 finally 保证两张表都尝试恢复；HideAndDontSave 不代替显式 DestroyImmediate。
            try { mWeapons.SetValue(null, mOldWeapons); }
            finally { try { mBullets.SetValue(null, mOldBullets); } finally { foreach (var value in mOwned) if (value != null) Object.DestroyImmediate(value); } }
        }
    }

    // 作用：直接只读查询 Resources 中唯一的第一关配置；返回：等级为 1 的配置，缺失或重复时抛出异常。
    private static StageConfig StageOne() => Resources.LoadAll<StageConfig>("Configs/Stages").Single(c => c.Level == 1); // 按关卡等级筛选并强制唯一，拒绝缺失或重复配置。
    // 作用：直接委托报告器运行独立夹具案例，using 确保异常时也释放夹具；返回：无返回值。
    private static void Case(Report report, string name, Action<Fixture> test) => report.Check(name, () => { using (var f = new Fixture()) test(f); }); // 将案例交给报告器捕获异常，夹具用完即释放。
    // 作用：请求装填并手动推进足够时间，断言结束装填；返回：无返回值。
    private static void Load(GunWeapon gun)
    {
        // 发起真实装填后推进略超装填时长的 Tick，再确认计时已完成。
        gun.RequestReload(); gun.Tick(gun.ReloadTime + 0.01f); Require(!gun.IsReloading, "装填未完成");
    }
    // 作用：断言弹夹来源份额及 Resource 总量一致；返回：无返回值。
    private static void Loaded(GunWeapon gun, int supply, int loot)
    {
        // 先检查已装弹药的来源拆分，再核对武器资源值是否等于两份额之和。
        Batch(gun.LoadedAmmo, supply, loot); Near(gun.Resource, supply + loot);
    }
    // 作用：直接委托 Require 核对弹药总数及 Supply、Loot 份额；返回：无返回值。
    private static void Batch(AmmoBatch ammo, int supply, int loot) => Require(ammo.Count == supply + loot && ammo.SupplyCount == supply && ammo.LootCount == loot, $"批次实际 {ammo.Count}/Supply{ammo.SupplyCount}/Loot{ammo.LootCount}，预期 Supply{supply}/Loot{loot}"); // 同时比较总量与来源份额，避免来源错配被总数掩盖。
    // 作用：直接按名称反射查找非公开实例或静态字段，不猜测替代名；返回：FieldInfo，字段缺失时抛出异常。
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic) ?? throw new MissingFieldException(type.Name, name); // 精确定位非公开字段，缺失即报错而不尝试替代字段。
    // 作用：直接通过反射读取字段并转换类型，不调用属性 getter；返回：字段的 T 类型值。
    private static T Get<T>(object target, string name) => (T)Field(target.GetType(), name).GetValue(target); // 从目标实际类型定位字段后读取原值，绕过属性访问副作用。
    // 作用：直接通过反射设置测试对象字段，不执行序列化保存；返回：无返回值。
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value); // 精确定位后直接写入测试字段，不走资产保存流程。
    // 作用：创建隐藏且不保存的未激活子节点，由夹具或测试池显式销毁；返回：新建临时 GameObject。
    private static GameObject Temp(string name, Transform parent)
    {
        // 先设置临时对象标记并停用，再挂到指定父节点，供手动逻辑测试使用。
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave }; go.SetActive(false); go.transform.SetParent(parent, false); return go;
    }
    // 作用：直接判断数值是否为有限正数；返回：true 表示有限且大于零，false 表示零、负数、无穷或 NaN。
    private static bool FinitePositive(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value); // 将正数约束与有限性约束合并，过滤非法尺寸和半径。
    // 作用：直接断言实际值有限且与预期的误差小于 0.0001；返回：无返回值。
    private static void Near(float actual, float expected) => Require(!float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Abs(actual - expected) < 0.0001f, $"实际 {actual}，预期 {expected}"); // 先排除非有限值，再用绝对误差容忍浮点计算偏差。
    // 作用：条件不成立时抛出带说明的断言异常；返回：无返回值。
    private static void Require(bool value, string message)
    {
        // 将失败条件转换为带业务说明的异常，交由外层报告器统一记账。
        if (!value) throw new InvalidOperationException(message);
    }
    // 作用：直接委托 Require 阻止在播放或编译期间执行校验；返回：无返回值。
    private static void Guard() => Require(CanValidate(), "只允许在未编译的 EditMode 执行"); // 复用菜单可用条件，在直接调用入口时同样阻止不合时机的校验。
    // 作用：直接计算校验菜单可用状态；返回：true 表示未播放、未切换到播放且未编译，false 表示禁止校验。
    [MenuItem(ResourceMenu, true), MenuItem(LogicMenu, true)]
    private static bool CanValidate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling; // 同时排除播放切换与编译阶段，只在稳定编辑状态放行。

    private sealed class Report
    {
        private int mPassed, mFailed; // mPassed：已通过的检查数；mFailed：失败的检查数。
        // 作用：捕获单项检查异常、累计通过或失败数并输出日志；返回：无返回值。
        public void Check(string name, Action check)
        {
            // 单项失败只记账，不阻断后续案例；最终由 Finish 汇总失败状态。
            try { check(); mPassed++; Debug.Log("[阶段四][PASS] " + name); }
            catch (Exception e) { mFailed++; Debug.LogError("[阶段四][FAIL] " + name + "：" + e.GetBaseException().Message); }
        }
        // 作用：输出本轮逐项结果，存在失败时抛出汇总异常；返回：无返回值。
        public void Finish()
        {
            // 先输出完整通过与失败计数，再以汇总异常向调用方传递本轮失败状态。
            Debug.Log($"[阶段四] PASS {mPassed} / FAIL {mFailed}；仅本次逐项检查，不代表端到端验收。");
            if (mFailed > 0) throw new InvalidOperationException($"阶段四校验失败：{mFailed} 项。");
        }
    }
}
#endif
