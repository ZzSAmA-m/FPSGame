using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Core.Utility;
using GamePlay.HoldSystem;
using GamePlay.Player;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPSGame.Editor.Validation
{
    /// <summary>在独立 Preview Scene 中验证武器装备，不修改资源或用户场景。</summary>
    public static class WeaponEquipmentValidation
    {
        private const string DefinitionPath = "Assets/Scripts/AK47SO.asset";
        private const string KnifeDefinitionPath = "Assets/Resources/Weapons/DefaultKnife.asset";
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        private const string WeaponPath = "Assets/Prefabs/Weapon/AK47.prefab";
        private static bool _running;

        [CliCommand("weapon_equipment_validate", "Validate isolated weapon equipment and prefab bindings", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再验证武器装备。";
            if (_running)
                return "REFUSED：武器装备验证已经在执行。";

            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("工厂读取当前 SO 参数且不共享逻辑实例", TestCurrentConfiguration, report, ref passed);
                RunCase("初始装备刀且重复初始化不重建实例", TestDefaultKnife, report, ref passed);
                RunCase("三种武器栏各一件而道具栏允许多件", TestSlotCapacity, report, ref passed);
                RunCase("两个角色共享 SO 时模型、逻辑与弹药独立", TestIndependentEquipment, report, ref passed);
                RunCase("真实 AK47 预制体绑定 wpn 并保留握持偏移", TestModelBinding, report, ref passed);
                RunCase("切换栏位保留武器实例和弹药", TestSlotSwitching, report, ref passed);
                RunCase("重复换装和卸下清理旧模型并回退到刀", TestReplacementAndRemoval, report, ref passed);
                RunCase("道具不能代替最后一把武器的保留约束", TestLastWeaponProtection, report, ref passed);
                RunCase("缺少预制体和类型错误保留原装备", TestInvalidDefinition, report, ref passed);
                RunCase("缺少 wpn 拒绝装备且保留原装备", TestMissingMount, report, ref passed);
                RunCase("Player 默认引用、FP/TP 挂点与旧模型显示", TestPlayerPrefab, report, ref passed);
                RunCase("销毁角色同时销毁装备模型", TestDestroyOwner, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally
            {
                _running = false;
            }
        }

        private static void RunCase(string name, Action<PreviewScope> test, StringBuilder report, ref int passed)
        {
            using (var scope = new PreviewScope())
                test(scope);
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static void TestCurrentConfiguration(PreviewScope scope)
        {
            AK47SO definition = scope.CloneDefinition();
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_initialTotalBullets").intValue = 83;
            serialized.FindProperty("_capacity").intValue = 7;
            serialized.FindProperty("_prepareTicks").intValue = 5;
            serialized.FindProperty("_shotIntervalTicks").intValue = 3;
            serialized.FindProperty("_reloadTicks").intValue = 11;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var first = definition.CreateWeapon() as AK47;
            var second = definition.Item as AK47;
            Require(first != null && second != null && !ReferenceEquals(first, second), "每次工厂调用应创建独立 AK47。");
            Require(first.Config.InitialTotalBullets == 83 && first.Config.Capacity == 7 &&
                    first.Config.PrepareTicks == 5 && first.Config.ShotIntervalTicks == 3 && first.Config.ReloadTicks == 11,
                "工厂必须使用本次序列化参数，不能使用构造时的默认值。");
            Require(first.Magazine.Rounds == 7 && first.Magazine.ReserveRounds == 76, "弹匣和备弹应采用当前 SO 配置。");
            Require(!ReferenceEquals(first.Magazine, second.Magazine), "不同武器不得共享弹匣。");
        }

        private static void TestDefaultKnife(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("DefaultKnife");
            Require(holder.InitializeLoadout(), "有效挂点应能初始化默认装备栏。");
            Require(holder.Knife != null && holder.Rifle == null && holder.Pistol == null && holder.Utilities.Count == 0,
                "未指定其他初始装备时应只持有一把默认刀。");
            Require(holder.WeaponCount == 1 && holder.ItemCount == 1 && ReferenceEquals(holder.CurrentEntry, holder.Knife),
                "初始化后必须有至少一把武器，且当前选中刀。");
            Require(holder.CurrentWeaponSO == LoadKnifeDefinition() && holder.CurrentController is KnifeController,
                "默认刀必须使用实际 KnifeSO 和配套刀控制器。");
            Require(holder.CurrentController.GetComponentsInChildren<Renderer>(true).Length > 0 &&
                    holder.CurrentController.GetComponentInChildren<Animator>(true)?.runtimeAnimatorController != null,
                "默认刀应保留真实模型和刀自身的动画控制器。");
            WeaponEntry knife = holder.Knife;
            WeaponController controller = holder.CurrentController;
            Require(holder.InitializeLoadout() && ReferenceEquals(holder.Knife, knife) && holder.CurrentController == controller,
                "重复初始化不能创建额外刀具或重置当前实例。");
            Require(!holder.SelectSlot(WeaponSlot.Pistol) && !holder.SelectSlot(WeaponSlot.Utility, -1) &&
                    !holder.RemoveWeapon(WeaponSlot.Utility, 0) && ReferenceEquals(holder.CurrentEntry, knife) && holder.CurrentController == controller,
                "选择空栏或无效道具下标不能使当前装备丢失。");
        }

        private static void TestSlotCapacity(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("SlotCapacity");
            Require(holder.InitializeLoadout(), "初始化应成功。");
            WeaponEntry initialKnife = holder.Knife;
            Require(holder.AddWeapon(LoadDefinition()), "应能把 AK47 放入长枪栏。");
            AK47SO replacementRifle = scope.CloneDefinition();
            Require(holder.AddWeapon(replacementRifle), "新长枪应能替换长枪栏。");
            Require(holder.WeaponCount == 2 && holder.Rifle.Definition == replacementRifle && ReferenceEquals(holder.CurrentEntry, initialKnife),
                "长枪替换不能累加栏位，也不能抢占未要求切换的当前刀。");

            WeaponSO pistol = scope.CreateSlotDefinition(WeaponSlot.Pistol);
            WeaponSO replacementPistol = scope.CreateSlotDefinition(WeaponSlot.Pistol);
            Require(holder.AddWeapon(pistol) && holder.AddWeapon(replacementPistol), "手枪栏应允许替换。");
            Require(holder.WeaponCount == 3 && holder.Pistol.Definition == replacementPistol,
                "手枪栏应始终只有最后加入的一把。");

            WeaponSO replacementKnife = scope.Track(Object.Instantiate(LoadKnifeDefinition()));
            Require(holder.AddWeapon(replacementKnife), "刀栏应允许替换当前刀。");
            Require(holder.WeaponCount == 3 && holder.Knife.Definition == replacementKnife &&
                    ReferenceEquals(holder.CurrentEntry, holder.Knife) && holder.CurrentWeaponSO == replacementKnife,
                "替换当前刀栏后应同时更新当前装备，不能保留脱离栏位的旧条目。");

            WeaponSO utility = scope.CreateSlotDefinition(WeaponSlot.Utility);
            Require(holder.AddWeapon(utility) && holder.AddWeapon(utility), "道具栏应允许同配置的多个独立道具。");
            Require(holder.Utilities.Count == 2 && holder.WeaponCount == 3 && holder.ItemCount == 5 &&
                    !ReferenceEquals(holder.Utilities[0].Weapon, holder.Utilities[1].Weapon),
                "道具应追加为独立条目，且不计入武器数量。");
            Require(holder.SelectSlot(WeaponSlot.Utility, 1) && ReferenceEquals(holder.CurrentEntry, holder.Utilities[1]),
                "道具栏应能按索引选择具体条目。");
            Require(holder.RemoveWeapon(WeaponSlot.Utility, 0) && holder.Utilities.Count == 1 &&
                    ReferenceEquals(holder.CurrentEntry, holder.Utilities[0]),
                "移除另一个道具后应继续持有原选中条目，不能按旧索引误换道具。");
        }

        private static void TestIndependentEquipment(PreviewScope scope)
        {
            AK47SO definition = scope.CloneDefinition();
            PlayerHolder first = scope.CreateHolder("First");
            PlayerHolder second = scope.CreateHolder("Second");
            Require(first.EquipWeapon(definition) && second.EquipWeapon(definition), "两个角色均应能装备相同 SO。");
            Require(first.CurrentWeaponSO == definition && second.CurrentWeaponSO == definition, "两个角色应引用同一配置。");
            Require(!ReferenceEquals(first.CurrentWeapon, second.CurrentWeapon) && first.CurrentController != second.CurrentController,
                "共享配置不得共享运行时对象或控制器。");
            var firstGun = (AK47)first.CurrentWeapon;
            var secondGun = (AK47)second.CurrentWeapon;
            int secondRounds = secondGun.Magazine.Rounds;
            int firstRounds = firstGun.Magazine.Rounds;
            Require(firstRounds > 0, "真实 AK 配置应有可用于隔离验证的初始弹药。");
            Require(firstGun.TryPrepare(), "新武器应能进入准备状态。");
            firstGun.Tick(firstGun.Config.PrepareTicks);
            Require(firstGun.TryFire(), "准备完成后应能开火。");
            Require(firstGun.Magazine.Rounds == firstRounds - 1 && secondGun.Magazine.Rounds == secondRounds,
                "一名角色射击不得改变另一名角色的弹药。");
            Require(secondGun.GunState == GunState.None, "另一名角色的状态机也应保持独立。");
        }

        private static void TestModelBinding(PreviewScope scope)
        {
            AK47SO definition = LoadDefinition();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPath);
            Require(prefab != null && definition.ResourcePack != null &&
                    definition.ControllerPrefab == definition.ResourcePack.ModelPrefab &&
                    definition.ControllerPrefab == prefab.GetComponent<GunModelController>(),
                "AK47SO 应通过资源包引用真实装备预制体的通用枪械根控制器。");
            Require(prefab.transform.parent == null && prefab.activeSelf, "装备预制体根节点必须启用。");
            PlayerHolder holder = scope.CreateHolder("Binding");
            Require(holder.EquipWeapon(definition), "真实 AK 预制体应能装备。");
            var controller = holder.CurrentController as GunModelController;
            Require(controller != null && controller.Holder == holder && ReferenceEquals(controller.Gun, holder.CurrentWeapon),
                "控制器必须绑定所属角色及其独立逻辑对象。");
            Require(controller.transform.parent == holder.WeaponMount && holder.WeaponMount.name == "wpn",
                "模型应成为 wpn 的直接子节点。");
            Require(controller.transform.localPosition == Vector3.zero && Quaternion.Angle(controller.transform.localRotation, Quaternion.identity) < 0.001f,
                "装备包装根应对齐挂点。");
            Require(controller.GetComponentsInChildren<Renderer>(true).Length > 0, "装备实例应包含真实渲染模型。");
            Require(controller.transform.childCount == prefab.transform.childCount && controller.transform.childCount > 0,
                "实例应保留模型子层级。");
            Transform model = controller.transform.GetChild(0);
            Transform source = prefab.transform.GetChild(0);
            Require(Vector3.Distance(model.localPosition, source.localPosition) < 0.00001f &&
                    Quaternion.Angle(model.localRotation, source.localRotation) < 0.001f &&
                    Vector3.Distance(model.localScale, source.localScale) < 0.00001f,
                "挂接时不能清空预制体模型内部的握持偏移与缩放。");
        }

        private static void TestSlotSwitching(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("SlotSwitching");
            Require(holder.EquipWeapon(LoadDefinition()), "应能收到并选中长枪。");
            WeaponEntry rifle = holder.Rifle;
            AK47 gun = (AK47)rifle.Weapon;
            Require(gun.TryPrepare(), "新枪应可开始准备。");
            gun.Tick(gun.Config.PrepareTicks);
            Require(gun.TryFire(), "准备后应可开火。");
            int rounds = gun.Magazine.Rounds;
            WeaponController previousController = holder.CurrentController;
            Require(holder.SelectSlot(WeaponSlot.Knife) && ReferenceEquals(holder.CurrentEntry, holder.Knife),
                "切刀应选择已有刀条目。");
            Require(previousController == null && gun.GunState == GunState.None, "收枪应释放旧模型并取消进行中的动作。");
            Require(holder.SelectSlot(WeaponSlot.Rifle) && ReferenceEquals(holder.CurrentEntry, rifle) &&
                    ReferenceEquals(holder.CurrentWeapon, gun) && gun.Magazine.Rounds == rounds,
                "切回长枪必须复用原枪逻辑和已消耗后的弹药，不能从 SO 重建。");
            WeaponController currentController = holder.CurrentController;
            int changes = 0;
            Action changed = () => changes++;
            holder.OnWeaponChanged += changed;
            try
            {
                Require(holder.SelectSlot(WeaponSlot.Rifle) && changes == 0 && holder.CurrentController == currentController,
                    "重复选择当前栏位应幂等，不重建模型、不重播持握动作。");
            }
            finally
            {
                holder.OnWeaponChanged -= changed;
            }
        }

        private static void TestReplacementAndRemoval(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("Replacement");
            AK47SO definition = LoadDefinition();
            Require(holder.EquipWeapon(definition), "初次装备应成功。");
            var previousGun = (AK47)holder.CurrentWeapon;
            WeaponController previousController = holder.CurrentController;
            Require(previousGun.TryPrepare(), "旧枪应能开始准备动作。");
            Require(holder.EquipWeapon(definition), "同一 SO 再次装备应成功。");
            Require(previousController == null && !ReferenceEquals(holder.CurrentWeapon, previousGun), "换装应销毁旧模型并创建新逻辑。");
            Require(previousGun.GunState == GunState.None && previousController.Weapon == null && previousController.Holder == null,
                "旧武器动作和控制器绑定应清理。");
            Require(holder.WeaponMount.childCount == 1, "重复装备后挂点只能保留一个装备模型。");

            WeaponController removed = holder.CurrentController;
            Require(holder.UnequipWeapon(), "还有刀时应允许移除当前长枪。");
            Require(holder.Rifle == null && holder.WeaponCount == 1 && ReferenceEquals(holder.CurrentEntry, holder.Knife) &&
                    holder.CurrentController is KnifeController,
                "移除当前长枪应自动持有剩下的刀，不能让角色空手。");
            Require(removed == null && holder.WeaponMount.childCount == 1, "卸下长枪后挂点只应保留新的刀模型。");
            Require(!holder.UnequipWeapon(), "最后一把武器应拒绝卸下。");
            Require(holder.WeaponCount == 1 && ReferenceEquals(holder.CurrentEntry, holder.Knife), "最后一把刀不能被移除。");
        }

        private static void TestLastWeaponProtection(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("LastWeapon");
            Require(holder.InitializeLoadout(), "初始化应成功。");
            Require(holder.AddWeapon(scope.CreateSlotDefinition(WeaponSlot.Utility), true), "应能添加并持有道具。");
            WeaponEntry utility = holder.CurrentEntry;
            Require(!holder.RemoveWeapon(WeaponSlot.Knife), "只有刀和道具时不能移除刀。");
            Require(holder.WeaponCount == 1 && holder.Knife != null && ReferenceEquals(holder.CurrentEntry, utility),
                "道具不算武器，不能以还有道具为理由移除唯一的刀。");
            Require(holder.UnequipWeapon() && holder.Utilities.Count == 0 && ReferenceEquals(holder.CurrentEntry, holder.Knife),
                "移除当前道具后应回退到仍在装备栏中的刀。");
            Require(holder.AddWeapon(LoadDefinition()) && holder.RemoveWeapon(WeaponSlot.Knife),
                "还有长枪时应允许移除刀，约束是至少一把武器而不是强制永久保留刀。");
            Require(holder.Knife == null && holder.WeaponCount == 1 && ReferenceEquals(holder.CurrentEntry, holder.Rifle),
                "移除当前刀后应自动选择剩余长枪。");
            Require(!holder.RemoveWeapon(WeaponSlot.Rifle), "最后一把长枪应拒绝移除。");
            Require(holder.Rifle != null && holder.WeaponCount == 1, "最后一把长枪同样不能被移除。");
        }

        private static void TestInvalidDefinition(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("InvalidDefinition");
            Require(holder.EquipWeapon(LoadDefinition()), "基准装备应成功。");
            WeaponBase weapon = holder.CurrentWeapon;
            WeaponController controller = holder.CurrentController;
            WeaponSO original = holder.CurrentWeaponSO;
            WeaponEntry originalRifle = holder.Rifle;
            int itemCount = holder.ItemCount;
            AK47SO missingPrefab = scope.CloneDefinition();
            SetReference(missingPrefab, "_resourcePack", null);
            ExpectRejected(() => holder.EquipWeapon(missingPrefab));
            var mismatch = scope.Track(ScriptableObject.CreateInstance<EquipmentMismatchSO>());
            SetReference(mismatch, "_controllerPrefab", LoadDefinition().ControllerPrefab);
            ExpectRejected(() => holder.EquipWeapon(mismatch));
            Require(ReferenceEquals(holder.CurrentWeapon, weapon) && holder.CurrentController == controller && holder.CurrentWeaponSO == original,
                "无效配置不得破坏原装备。");
            Require(ReferenceEquals(holder.Rifle, originalRifle) && holder.ItemCount == itemCount && holder.Knife != null,
                "添加失败不能先清空原栏位或破坏默认刀。");
            Require(holder.WeaponMount.childCount == 1 && controller.Holder == holder, "失败不能遗留新模型或解除旧绑定。");
        }

        private static void TestMissingMount(PreviewScope scope)
        {
            PlayerHolder empty = scope.CreateHolder("NoMount", false);
            ExpectRejected(() => empty.EquipWeapon(LoadDefinition()));
            Require(empty.CurrentWeapon == null && empty.transform.childCount == 0, "缺挂点不能创建逻辑装备或模型。");

            PlayerHolder holder = scope.CreateHolder("LostMount");
            Require(holder.EquipWeapon(LoadDefinition()), "基准装备应成功。");
            WeaponBase oldWeapon = holder.CurrentWeapon;
            WeaponController oldController = holder.CurrentController;
            Transform mount = holder.WeaponMount;
            mount.name = "UnavailableMount";
            SetReference(holder, "_weaponMount", null);
            ExpectRejected(() => holder.EquipWeapon(LoadDefinition()));
            Require(ReferenceEquals(holder.CurrentWeapon, oldWeapon) && holder.CurrentController == oldController &&
                    oldController.transform.parent == mount && mount.childCount == 1,
                "重新找不到挂点时必须保留原装备及其模型。");
        }

        private static void TestPlayerPrefab(PreviewScope scope)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            Require(prefab != null, "Player 预制体必须存在。");
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scope.Scene);
            // 编辑模式禁止重挂预制体实例内部节点；解包这份临时副本以模拟运行时层级。
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            PlayerHolder holder = player.GetComponent<PlayerHolder>();
            Require(holder != null && holder.WeaponMount != null && holder.WeaponMount.name == "wpn" && holder.WeaponMount.IsChildOf(player.transform),
                "真实 Player 预制体必须配置自身的 wpn 挂点。");
            var serialized = new SerializedObject(holder);
            Require(serialized.FindProperty("_defaultKnife").objectReferenceValue == LoadKnifeDefinition(), "Player 应配置默认刀资源。");
            SerializedProperty initialWeapons = serialized.FindProperty("_initialWeapons");
            bool hasRifle = false;
            for (int i = 0; i < initialWeapons.arraySize; i++)
                hasRifle |= initialWeapons.GetArrayElementAtIndex(i).objectReferenceValue == LoadDefinition();
            Require(hasRifle, "原 AK47 应保留为长枪栏的初始配置，而不覆盖默认选中的刀。");
            PlayerModelController model = player.GetComponent<PlayerModelController>();
            PlayerMotionController motion = player.GetComponent<PlayerMotionController>();
            Require(model != null && motion != null, "Player 必须配置独立动作组件和模型订阅者。");
            Require(model.RifleModel != null && model.KnifeModel != null, "旧调试模型引用应完整迁移到模型控制器。");
            Require(new SerializedObject(motion).FindProperty("_movementThreshold").floatValue == 2f,
                "现有 Player 的动作速度阈值 2 应保留到新的动作组件。");

            // 普通 MonoBehaviour 在编辑模式不会启动；只启动动作桥接与模型监听。
            // 不启动 Animator 订阅者，避免编辑模式下的 Animator.Play 警告。
            InvokeLifecycle(model, "OnDisable");
            InvokeLifecycle(motion, "OnDisable");
            try
            {
                InvokeLifecycle(motion, "OnEnable");
                InvokeLifecycle(model, "OnEnable");
                Require(holder.InitializeLoadout(), "真实 Player 应能初始化装备栏。");
                Require(ReferenceEquals(holder.CurrentEntry, holder.Knife) && holder.CurrentWeaponSO == LoadKnifeDefinition() &&
                        holder.Rifle != null && holder.Rifle.Definition == LoadDefinition(),
                    "真实 Player 应同时拥有刀和原 AK47，并默认选中刀。");
                Transform mount = holder.WeaponMount;
                Require(!model.RifleModel.activeSelf && !model.KnifeModel.activeSelf,
                    "装备变化应沿动作事件链自动隐藏旧步枪和刀模型。");
                motion.StateMachine.HandleWeaponInput(WeaponInputAction.Key3);
                motion.StateMachine.HandleWeaponInput(WeaponInputAction.Key1);
                Require(!model.RifleModel.activeSelf && !model.KnifeModel.activeSelf,
                    "旧切换按键不得重新显示真实装备之外的调试模型。");
                Require(holder.SelectSlot(WeaponSlot.Rifle) && motion.StateMachine.HoldPose == GamePlay.Player.MotionFSM.PlayerHoldPose.Rifle,
                    "选择长枪栏应通过装备事件切换动作姿态。");
                Require(holder.SelectSlot(WeaponSlot.Knife) && motion.StateMachine.HoldPose == GamePlay.Player.MotionFSM.PlayerHoldPose.Knife,
                    "选择刀栏应通过装备事件恢复持刀姿态。");

                motion.SetView(EPlayerModelType.FP);
                Require(mount.IsChildOf(model.FirstPersonRoot) && holder.CurrentController.transform.parent == mount,
                    "切到 FP 后模型订阅者必须让装备继续跟随同一 wpn。");
                motion.SetView(EPlayerModelType.TP);
                Require(mount.IsChildOf(model.ThirdPersonRoot) && holder.CurrentController.transform.parent == mount,
                    "切回 TP 后模型订阅者必须让装备继续跟随同一 wpn。");

                InvokeLifecycle(model, "OnDisable");
                motion.SetView(EPlayerModelType.FP);
                Require(mount.IsChildOf(model.ThirdPersonRoot), "模型组件退订后不应继续处理视角事件。");
                InvokeLifecycle(model, "OnEnable");
                Require(mount.IsChildOf(model.FirstPersonRoot), "模型重新启用时应读取当前视角补齐表现。");
            }
            finally
            {
                InvokeLifecycle(model, "OnDisable");
                InvokeLifecycle(motion, "OnDisable");
            }
        }

        private static void InvokeLifecycle(Component component, string name)
        {
            MethodInfo method = component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(method != null, "缺少临时生命周期入口 " + name);
            method.Invoke(component, null);
        }

        private static void TestDestroyOwner(PreviewScope scope)
        {
            PlayerHolder holder = scope.CreateHolder("DestroyedOwner");
            Require(holder.EquipWeapon(LoadDefinition()), "销毁验证前应能装备。");
            WeaponController controller = holder.CurrentController;
            GameObject model = controller.gameObject;
            Object.DestroyImmediate(holder.gameObject);
            Require(controller == null && model == null, "销毁角色不得留下挂接的模型或控制器。");
            Require(scope.Scene.rootCount == 0, "销毁角色后 preview scene 不应有游离装备。");
        }

        private static AK47SO LoadDefinition()
        {
            AK47SO definition = AssetDatabase.LoadAssetAtPath<AK47SO>(DefinitionPath);
            Require(definition != null, "真实 AK47SO 资产必须存在。");
            return definition;
        }

        private static KnifeSO LoadKnifeDefinition()
        {
            KnifeSO definition = AssetDatabase.LoadAssetAtPath<KnifeSO>(KnifeDefinitionPath);
            Require(definition != null, "默认刀资源必须存在。");
            return definition;
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            Require(property != null, "缺少序列化字段 " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ExpectRejected(Func<bool> action)
        {
            ILogHandler handler = Debug.unityLogger.logHandler;
            bool enabled = Debug.unityLogger.logEnabled;
            LogType filter = Debug.unityLogger.filterLogType;
            var capture = new CaptureLogHandler();
            try
            {
                Debug.unityLogger.logHandler = capture;
                Debug.unityLogger.logEnabled = true;
                Debug.unityLogger.filterLogType = LogType.Log;
                Require(!action(), "无效装备请求应返回 false。");
                Require(capture.Warnings > 0 && capture.Errors == 0, "无效配置应给出 warning，而非抛出异常。");
            }
            finally
            {
                Debug.unityLogger.logHandler = handler;
                Debug.unityLogger.logEnabled = enabled;
                Debug.unityLogger.filterLogType = filter;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class CaptureLogHandler : ILogHandler
        {
            public int Warnings;
            public int Errors;
            public void LogFormat(LogType type, Object context, string format, params object[] args)
            {
                if (type == LogType.Warning) Warnings++;
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) Errors++;
            }
            public void LogException(Exception exception, Object context) => Errors++;
        }

        private sealed class PreviewScope : IDisposable
        {
            public Scene Scene { get; } = EditorSceneManager.NewPreviewScene();
            private readonly List<Object> _temporaryAssets = new();

            public T Track<T>(T asset) where T : Object
            {
                asset.hideFlags = HideFlags.HideAndDontSave;
                _temporaryAssets.Add(asset);
                return asset;
            }

            public AK47SO CloneDefinition() => Track(Object.Instantiate(LoadDefinition()));

            public WeaponSO CreateSlotDefinition(WeaponSlot slot)
            {
                var definition = Track(ScriptableObject.CreateInstance<EquipmentSlotValidationSO>());
                definition.SlotValue = slot;
                SetReference(definition, "_resourcePack", LoadDefinition().ResourcePack);
                return definition;
            }

            public PlayerHolder CreateHolder(string name, bool withMount = true)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, Scene);
                PlayerHolder holder = root.AddComponent<PlayerHolder>();
                SetReference(holder, "_defaultKnife", LoadKnifeDefinition());
                if (withMount)
                {
                    GameObject mount = EditorUtility.CreateGameObjectWithHideFlags("wpn", HideFlags.HideAndDontSave);
                    SceneManager.MoveGameObjectToScene(mount, Scene);
                    mount.transform.SetParent(root.transform, false);
                }
                return holder;
            }

            public void Dispose()
            {
                try
                {
                    if (Scene.IsValid())
                        EditorSceneManager.ClosePreviewScene(Scene);
                }
                finally
                {
                    foreach (Object asset in _temporaryAssets)
                        if (asset != null) Object.DestroyImmediate(asset);
                }
            }
        }
    }

    /// <summary>仅用于类型不匹配检查的临时配置，不保存为资源。</summary>
    internal sealed class EquipmentMismatchSO : WeaponSO
    {
        public override WeaponSlot Slot => WeaponSlot.Rifle;
        public override WeaponBase CreateWeapon() => new EquipmentMismatchWeapon();
    }

    /// <summary>使用共享枪械资源包验证槽位规则，不宣称这些测试条目是成品手枪或道具。</summary>
    internal sealed class EquipmentSlotValidationSO : GunSO
    {
        internal WeaponSlot SlotValue;
        public override WeaponSlot Slot => SlotValue;
        public override WeaponBase CreateWeapon() => new AK47();
    }

    internal sealed class EquipmentMismatchWeapon : WeaponBase
    {
        public override string Name => "Validation mismatch";
        public override string Info => "Editor-only validation weapon";
    }
}
