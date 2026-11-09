using System;
using System.Collections.Generic;
using System.Text;
using GamePlay.HoldSystem;
using GamePlay.Player;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FPSGame.Editor.Validation
{
    /// <summary>隔离验证共享枪械资源包、通用模型绑定和真实迁移资产，不播放声音或动画。</summary>
    public static class GunResourcesValidation
    {
        private static bool _running;
        private static readonly WeaponPresentationAction[] Actions =
        {
            WeaponPresentationAction.Prepare, WeaponPresentationAction.Hold,
            WeaponPresentationAction.Fire, WeaponPresentationAction.Reload
        };
        private static readonly WeaponAnimationTarget[] Targets =
        {
            WeaponAnimationTarget.FirstPerson, WeaponAnimationTarget.ThirdPerson, WeaponAnimationTarget.Model
        };

        [CliCommand("gun_resources_validate", "Validate shared gun presentation resources and generic model bindings", Tags = new[] { "fpsgame/validation" })]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "REFUSED：请先退出 Play Mode，再验证枪械资源包。";
            if (_running) return "REFUSED：枪械资源包验证已经在执行。";
            _running = true;
            var report = new StringBuilder();
            int passed = 0;
            try
            {
                RunCase("资源包按动作及三个动画目标正确提供不同状态名", TestResourceLookup, report, ref passed);
                RunCase("不同枪械类型共用通用模型，切换后资源及独立实例正确更新", TestGenericModelsAndSwitching, report, ref passed);
                RunCase("绑定明确使用传入装备条目，而非角色尚未更新的当前装备", TestStagedBinding, report, ref passed);
                RunCase("枪械缺资源包或模型时拒绝装备，不回退旧字段且保留原装备", TestInvalidResources, report, ref passed);
                RunCase("音效接口完整保留只读变体引用，不影响共享枪械的独立运行态", TestAudioReferences, report, ref passed);
                RunCase("真实AK资源包的Actor和Model动画状态均存在，模型绑定完整", TestMigratedAnimations, report, ref passed);
                RunCase("真实Player不再挂载测试音频播放器，原音频引用已进入资源包", TestMigratedAudio, report, ref passed);
                return "PASS " + passed + " checks\n" + report;
            }
            finally { _running = false; }
        }

        private static AK47SO Definition()
        {
            AK47SO definition = AssetDatabase.LoadAssetAtPath<AK47SO>("Assets/Scripts/AK47SO.asset");
            Require(definition != null && definition.ResourcePack != null, "请先完成真实 AK47 资源包迁移。");
            Require(definition.ResourcePack.ModelPrefab is GunModelController, "真实 AK 资源包必须使用通用枪械模型控制器。");
            return definition;
        }

        private static void TestResourceLookup(PreviewScope scope)
        {
            GunResourcePackSO pack = scope.Pack("Lookup");
            foreach (WeaponAnimationTarget target in Targets)
            foreach (WeaponPresentationAction action in Actions)
                Require(pack.TryGetAnimation(action, target, out string name) && name == Name("Lookup", action, target),
                    "资源读取必须区分动作及角色第一人称、第三人称和模型三个目标。");
            Require(pack.ModelPrefab == Definition().ControllerPrefab, "模型资源应是包中配置的真实通用控制器引用。");
        }

        private static void TestGenericModelsAndSwitching(PreviewScope scope)
        {
            GunResourcePackSO firstPack = scope.Pack("First");
            GunResourcePackSO secondPack = scope.Pack("Second");
            AK47SO first = scope.AK(firstPack);
            ResourceValidationGunSO second = scope.Other(secondPack);
            PlayerHolder left = scope.Holder("Left");
            PlayerHolder right = scope.Holder("Right");
            Require(left.EquipWeapon(first) && right.EquipWeapon(first), "两个角色应能使用同一枪械定义和资源包。");
            var leftGun = (Gun)left.CurrentWeapon;
            var rightGun = (Gun)right.CurrentWeapon;
            WeaponController firstModel = left.CurrentController;
            Require(!ReferenceEquals(leftGun, rightGun) && left.CurrentController != right.CurrentController &&
                    ReferenceEquals(left.CurrentPresentationResources, firstPack) &&
                    ReferenceEquals(right.CurrentPresentationResources, firstPack),
                "共享包应共享资源引用，但逻辑、弹药和模型实例必须独立。");
            Ready(leftGun);
            Require(leftGun.TryFire() && leftGun.Magazine.Rounds == rightGun.Magazine.Rounds - 1 && rightGun.GunState == GunState.None,
                "共享资源时，一名角色开火不得修改另一名角色的枪械运行态。");
            int remaining = leftGun.Magazine.Rounds;
            Require(left.EquipWeapon(second) && left.CurrentWeapon is ResourceValidationGun && left.CurrentController is GunModelController,
                "通用模型必须接受另一种 Gun 子类，不依赖 AK47 专用控制器。");
            Require(ReferenceEquals(left.CurrentPresentationResources, secondPack) &&
                    ReferenceEquals(left.CurrentEntry.PresentationResources, secondPack) &&
                    ReferenceEquals(left.CurrentController.PresentationResources, secondPack) && firstModel == null,
                "切换枪械必须更新条目、角色和新模型的资源，并清理旧模型。");
            Require(left.CurrentPresentationResources.TryGetAnimation(WeaponPresentationAction.Fire, WeaponAnimationTarget.FirstPerson, out string next) &&
                    next == Name("Second", WeaponPresentationAction.Fire, WeaponAnimationTarget.FirstPerson),
                "新的动画来源必须是当前资源包。");
            Require(left.SelectSlot(WeaponSlot.Rifle) && ReferenceEquals(left.CurrentWeapon, leftGun) &&
                    leftGun.Magazine.Rounds == remaining && ReferenceEquals(left.CurrentPresentationResources, firstPack),
                "切回原栏位必须恢复原资源和已有枪械，保留消耗后的弹药。");
        }

        private static void TestStagedBinding(PreviewScope scope)
        {
            GunResourcePackSO firstPack = scope.Pack("Current");
            GunResourcePackSO secondPack = scope.Pack("Staged");
            PlayerHolder holder = scope.Holder("StagedBinding");
            Require(holder.EquipWeapon(scope.AK(firstPack)) && holder.AddWeapon(scope.Other(secondPack)),
                "测试必须在角色持有旧枪时，将另一种枪加入非当前栏位。");
            WeaponEntry old = holder.CurrentEntry;
            WeaponEntry incoming = holder.Pistol;
            Require(incoming != null && !ReferenceEquals(old, incoming), "待绑定条目不能已经是当前条目。");
            WeaponController model = scope.InstantiateModel(secondPack.ModelPrefab);
            model.Bind(holder, incoming);
            Require(ReferenceEquals(holder.CurrentEntry, old) && ReferenceEquals(model.Entry, incoming) &&
                    ReferenceEquals(model.Weapon, incoming.Weapon) && model.Holder == holder &&
                    ReferenceEquals(model.PresentationResources, secondPack),
                "Bind 必须通过传入 Entry 选择资源，不能错误读取角色旧 CurrentEntry。");
            model.Unbind();
            Require(model.Entry == null && model.Weapon == null && model.Holder == null && model.PresentationResources == null,
                "解绑后不得保留旧装备或资源引用。");
        }

        private static void TestInvalidResources(PreviewScope scope)
        {
            PlayerHolder holder = scope.Holder("InvalidResources");
            AK47SO original = scope.AK(scope.Pack("Valid"));
            Require(holder.EquipWeapon(original), "基准枪械必须能装备。");
            WeaponEntry entry = holder.CurrentEntry;
            WeaponController model = holder.CurrentController;
            AK47SO missingPack = scope.AK(null);
            SetReference(missingPack, "_controllerPrefab", Definition().ControllerPrefab);
            Require(missingPack.ControllerPrefab == null && missingPack.PresentationResources == null,
                "GunSO 不得在缺资源包时回退继承的旧模型字段。");
            Reject(() => holder.EquipWeapon(missingPack));
            GunResourcePackSO missingModel = scope.Pack("MissingModel");
            SetReference(missingModel, "_modelPrefab", null);
            Reject(() => holder.EquipWeapon(scope.AK(missingModel)));
            Require(ReferenceEquals(holder.CurrentEntry, entry) && holder.CurrentController == model &&
                    ReferenceEquals(holder.CurrentPresentationResources, original.ResourcePack) &&
                    model.Holder == holder && holder.WeaponMount.childCount == 1,
                "失败装备请求必须完整保留旧条目、模型、绑定与资源，不能遗留半成品。");
        }

        private static void TestAudioReferences(PreviewScope scope)
        {
            GunResourcePackSO pack = scope.Pack("Audio");
            AudioClip prepare = scope.Clip("prepare");
            AudioClip firstFire = scope.Clip("fire1");
            AudioClip secondFire = scope.Clip("fire2");
            AudioClip reload = scope.Clip("reload");
            SetAudio(pack, "_prepareAudio", prepare);
            SetAudio(pack, "_fireAudio", firstFire, secondFire);
            SetAudio(pack, "_reloadAudio", reload);
            IReadOnlyList<AudioClip> fire = pack.GetAudioClips(WeaponPresentationAction.Fire);
            Require(pack.GetAudioClips(WeaponPresentationAction.Prepare).Count == 1 &&
                    pack.GetAudioClips(WeaponPresentationAction.Prepare)[0] == prepare &&
                    fire.Count == 2 && fire[0] == firstFire && fire[1] == secondFire &&
                    pack.GetAudioClips(WeaponPresentationAction.Reload).Count == 1 &&
                    pack.GetAudioClips(WeaponPresentationAction.Reload)[0] == reload,
                "接口必须原样提供所有音频变体引用，不能随机挑选一项、复制音频或播放声音。");
            if (fire is ICollection<AudioClip> mutableView)
            {
                bool refused = false;
                try { mutableView.Add(reload); }
                catch (NotSupportedException) { refused = true; }
                Require(refused && fire.Count == 2, "公开的音频视图必须防止调用方修改共享配置。");
            }
            AK47SO definition = scope.AK(pack);
            var first = (Gun)definition.CreateWeapon();
            var second = (Gun)definition.CreateWeapon();
            Ready(first);
            Require(first.TryFire() && first.Magazine.Rounds == second.Magazine.Rounds - 1 &&
                    second.GunState == GunState.None && pack.GetAudioClips(WeaponPresentationAction.Fire).Count == 2,
                "读取共享音频资源和某把枪的射击进度不能相互修改，也不能共享枪械运行状态。");
            Require(scope.Scene.GetRootGameObjects().Length == 0,
                "只读取音频与创建纯逻辑枪械不得额外创建播放器或场景对象。");
        }

        private static void TestMigratedAnimations(PreviewScope scope)
        {
            AK47SO definition = Definition();
            GunResourcePackSO pack = definition.ResourcePack;
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            PlayerAnimatorController actor = player != null ? player.GetComponent<PlayerAnimatorController>() : null;
            Require(actor != null, "真实 Player 必须保留角色动画组件。");
            var serialized = new SerializedObject(actor);
            Animator actorAnimator = serialized.FindProperty("_mainAnimator").objectReferenceValue as Animator;
            AnimatorController actorController = Controller(actorAnimator);
            AnimatorController modelController = Controller(pack.ModelPrefab.GetComponentInChildren<Animator>(true));
            foreach (WeaponAnimationTarget target in Targets)
            foreach (WeaponPresentationAction action in Actions)
            {
                bool configured = pack.TryGetAnimation(action, target, out string name);
                // 当前枪械模型没有独立拔枪素材；角色准备动画仍须完整配置。
                if (!configured && target == WeaponAnimationTarget.Model && action == WeaponPresentationAction.Prepare)
                    continue;
                Require(configured, "真实资源包必须完整配置 " + target + "/" + action + "。");
                AnimatorController controller = target == WeaponAnimationTarget.Model ? modelController : actorController;
                int layer = target == WeaponAnimationTarget.Model ? 0 :
                    target == WeaponAnimationTarget.FirstPerson ? PlayerAnimatorController.FPLayer : PlayerAnimatorController.UpLayer;
                Require(controller != null && layer < controller.layers.Length &&
                        HasState(controller.layers[layer].stateMachine, controller.layers[layer].name, name),
                    "配置的动画状态必须存在于目标 Animator 的正确层：" + target + "/" + action + "=" + name);
            }
            PlayerHolder holder = scope.Holder("RealModelBinding");
            Require(holder.EquipWeapon(definition) && holder.CurrentController is GunModelController model &&
                    model.ModelAnimator != null && model.Gun == holder.CurrentWeapon &&
                    ReferenceEquals(model.Entry, holder.CurrentEntry) && ReferenceEquals(model.PresentationResources, pack),
                "真实通用模型应绑定实际条目、独立枪械、资源包和模型 Animator。");
            // 状态存在和骨骼有变化不能证明姿势正确，还需检查真实第三人称骨长与分层求值。
            ThirdPersonFirePoseValidation.Validate();
        }

        private static void TestMigratedAudio(PreviewScope scope)
        {
            GunResourcePackSO pack = Definition().ResourcePack;
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Require(player != null && player.GetComponentsInChildren<PlayerFireAudioTester>(true).Length == 0,
                "真实 Player 不能保留旧测试音频播放器。");
            IReadOnlyList<AudioClip> prepare = pack.GetAudioClips(WeaponPresentationAction.Prepare);
            IReadOnlyList<AudioClip> fire = pack.GetAudioClips(WeaponPresentationAction.Fire);
            Require(prepare.Count > 0 && fire.Count > 1, "原准备音频和完整开火变体必须迁入资源包。");
            foreach (AudioClip clip in prepare) Require(clip != null && AssetDatabase.Contains(clip), "准备音频应保留真实资源引用。");
            foreach (AudioClip clip in fire) Require(clip != null && AssetDatabase.Contains(clip), "每个开火变体应保留真实资源引用。");
        }

        private static AnimatorController Controller(Animator animator)
        {
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
            return controller as AnimatorController;
        }

        private static bool HasState(AnimatorStateMachine machine, string prefix, string requested)
        {
            foreach (ChildAnimatorState child in machine.states)
                if (requested == child.state.name || requested == prefix + "." + child.state.name) return true;
            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
                if (HasState(child.stateMachine, prefix + "." + child.stateMachine.name, requested)) return true;
            return false;
        }

        private static string Name(string prefix, WeaponPresentationAction action, WeaponAnimationTarget target) =>
            prefix + "." + target + "." + action;

        private static void Ready(Gun gun)
        {
            gun.Tick(0);
            Require(gun.TryPrepare(), "新枪应能开始准备。");
            gun.Tick(gun.Config.PrepareTicks);
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            Require(property != null, "缺少资源配置字段 " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAudio(GunResourcePackSO pack, string field, params AudioClip[] clips)
        {
            var serialized = new SerializedObject(pack);
            SerializedProperty property = serialized.FindProperty(field);
            property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Reject(Func<bool> operation)
        {
            bool logging = Debug.unityLogger.logEnabled;
            try
            {
                Debug.unityLogger.logEnabled = false;
                Require(!operation(), "无效资源必须拒绝装备。");
            }
            finally { Debug.unityLogger.logEnabled = logging; }
        }

        private static void RunCase(string name, Action<PreviewScope> test, StringBuilder report, ref int passed)
        {
            using (var scope = new PreviewScope()) test(scope);
            passed++;
            report.AppendLine("PASS " + name);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private sealed class PreviewScope : IDisposable
        {
            public Scene Scene { get; } = EditorSceneManager.NewPreviewScene();
            private readonly List<Object> _assets = new();

            public GunResourcePackSO Pack(string prefix)
            {
                GunResourcePackSO pack = Track(ScriptableObject.CreateInstance<GunResourcePackSO>());
                SetReference(pack, "_modelPrefab", Definition().ControllerPrefab);
                var serialized = new SerializedObject(pack);
                foreach (WeaponAnimationTarget target in Targets)
                {
                    string field = target == WeaponAnimationTarget.FirstPerson ? "_firstPersonAnimations" :
                        target == WeaponAnimationTarget.ThirdPerson ? "_thirdPersonAnimations" : "_modelAnimations";
                    SerializedProperty names = serialized.FindProperty(field);
                    foreach (WeaponPresentationAction action in Actions)
                        names.FindPropertyRelative("_" + action.ToString().ToLowerInvariant()).stringValue = Name(prefix, action, target);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return pack;
            }

            public AK47SO AK(GunResourcePackSO pack)
            {
                AK47SO definition = Track(Object.Instantiate(Definition()));
                SetReference(definition, "_resourcePack", pack);
                return definition;
            }

            public ResourceValidationGunSO Other(GunResourcePackSO pack)
            {
                ResourceValidationGunSO definition = Track(ScriptableObject.CreateInstance<ResourceValidationGunSO>());
                SetReference(definition, "_resourcePack", pack);
                return definition;
            }

            public AudioClip Clip(string name) => Track(AudioClip.Create(name, 16, 1, 16000, false));

            public PlayerHolder Holder(string name)
            {
                GameObject root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(root, Scene);
                GameObject mount = EditorUtility.CreateGameObjectWithHideFlags("wpn", HideFlags.HideAndDontSave);
                SceneManager.MoveGameObjectToScene(mount, Scene);
                mount.transform.SetParent(root.transform, false);
                return root.AddComponent<PlayerHolder>();
            }

            public WeaponController InstantiateModel(WeaponController prefab)
            {
                GameObject root = Object.Instantiate(prefab.gameObject);
                root.hideFlags = HideFlags.HideAndDontSave;
                SceneManager.MoveGameObjectToScene(root, Scene);
                return root.GetComponent<WeaponController>();
            }

            private T Track<T>(T value) where T : Object
            {
                value.hideFlags = HideFlags.HideAndDontSave;
                _assets.Add(value);
                return value;
            }

            public void Dispose()
            {
                try { if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene); }
                finally
                {
                    foreach (Object asset in _assets) if (asset != null) Object.DestroyImmediate(asset);
                }
            }
        }
    }

    internal sealed class ResourceValidationGunSO : GunSO
    {
        public override WeaponSlot Slot => WeaponSlot.Pistol;
        public override WeaponBase CreateWeapon() => new ResourceValidationGun(CreateConfig());
    }

    internal sealed class ResourceValidationGun : Gun
    {
        public ResourceValidationGun(GunConfig config) : base(config) { }
        public override string Name => "Resource validation gun";
        public override string Info => "Editor-only gun type for generic presentation validation";
    }
}
