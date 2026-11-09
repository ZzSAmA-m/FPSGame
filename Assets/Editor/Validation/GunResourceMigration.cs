using System;
using System.Collections.Generic;
using System.Linq;
using GamePlay.HoldSystem;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FPSGame.Editor.Validation
{
    /// <summary>将当前 AK47 的既有模型、动作和音频迁移到资源包；不保存其他脏资产或当前场景。</summary>
    public static class GunResourceMigration
    {
        private const string PackPath = "Assets/Resources/Weapons/AK47ResourcePack.asset";
        private const string GunPath = "Assets/Scripts/AK47SO.asset";
        private const string ModelPath = "Assets/Prefabs/Weapon/AK47.prefab";
        private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
        private const string ActorControllerPath = "Assets/Resources/CS2/Agents/ctm_fbi_meter/meter_fbi_controller.controller";
        private const string ModelControllerPath = "Assets/Resources/CS2/Weapons/ak47/ak47 1.controller";
        private const string AgentRoot = "Assets/Resources/CS2/Agents/ctm_fbi_meter/";
        public const string ThirdPersonFirePath = AgentRoot + "PresentationAnims/tp_shoot_ak.anim";
        private const string WeaponRoot = "Assets/Resources/CS2/Weapons/ak47/";
        private const string AudioRoot = "Assets/Resources/CS2/Audio/Weapons/ak47/";
        private const string ModelFire = "weapons_models_ak47_weapon_rif_ak47_vmdl_c_shoot";
        private const string ModelReload = "weapons_models_ak47_weapon_rif_ak47_vmdl_c_reload";

        [CliCommand("gun_resources_migrate", "Migrate existing AK47 model, animation and audio references to its resource pack", Tags = new[] { "fpsgame/validation" })]
        public static JObject Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode，再迁移枪械资源。");

            // 先确认全部来源，避免发现缺少资源时已经移除了旧组件。
            var gun = Require<GunSO>(GunPath);
            var actor = Require<AnimatorController>(ActorControllerPath);
            var model = Require<AnimatorController>(ModelControllerPath);
            Require<GameObject>(PlayerPath);
            Require<GameObject>(ModelPath);
            AnimationClip fpReload = Require<AnimationClip>(AgentRoot + "FPAnims/fp_reload_ak.anim");
            AnimationClip tpFireSource = Require<AnimationClip>(AgentRoot + "TPAnims/tp_shoot_ak.anim");
            AnimationClip tpHold = Require<AnimationClip>(AgentRoot + "TPAnims/tp_draw_ak.anim");
            AnimationClip tpReload = Require<AnimationClip>(AgentRoot + "TPAnims/tp_reload_ak.anim");
            AnimationClip modelFire = Require<AnimationClip>(WeaponRoot + "weapons_models_ak47_weapon_rif_ak47.vmdl_c_shoot.anim");
            AnimationClip modelReload = Require<AnimationClip>(WeaponRoot + "weapons_models_ak47_weapon_rif_ak47.vmdl_c_reload.anim");
            var defaultFire = new[]
            {
                Require<AudioClip>(AudioRoot + "ak47_01.wav"), Require<AudioClip>(AudioRoot + "ak47_02.wav"),
                Require<AudioClip>(AudioRoot + "ak47_03.wav"), Require<AudioClip>(AudioRoot + "ak47_04.wav")
            };
            AudioClip defaultPrepare = Require<AudioClip>(AudioRoot + "ak47_boltpull_01.wav");
            var reloadAudio = new[]
            {
                Require<AudioClip>(AudioRoot + "ak47_clipout_01.wav"),
                Require<AudioClip>(AudioRoot + "ak47_addammo_02.wav"),
                Require<AudioClip>(AudioRoot + "ak47_boltpull.wav")
            };
            AnimatorStateMachine down = Layer(actor, "Down Layer");
            AnimatorStateMachine up = Layer(actor, "Up Layer");
            AnimatorStateMachine fp = Layer(actor, "FP Layer");
            AnimatorStateMachine modelBase = Layer(model, "Base Layer");
            ReadLegacyAudio(out List<AudioClip> legacyFire, out AudioClip legacyPrepare);

            AnimationClip tpFire = SaveThirdPersonFire(tpFireSource, tpHold);
            EnsureState(fp, "fp_reload_ak", fpReload);
            EnsureState(up, "tp_shoot_ak", tpFire);
            EnsureState(up, "tp_reload_ak", tpReload);
            RemoveStates(down, "tp_reload_ak");
            AnimatorState fireState = EnsureState(modelBase, ModelFire, modelFire);
            EnsureState(modelBase, ModelReload, modelReload);
            modelBase.defaultState = fireState;
            EditorUtility.SetDirty(actor);
            EditorUtility.SetDirty(model);
            AssetDatabase.SaveAssetIfDirty(actor);
            AssetDatabase.SaveAssetIfDirty(model);

            WeaponController modelPrefab = MigrateModel(model, modelFire, modelReload, out string animatorRoot);
            GunResourcePackSO pack = AssetDatabase.LoadAssetAtPath<GunResourcePackSO>(PackPath);
            bool created = pack == null;
            if (created)
            {
                EnsureFolder("Assets/Resources/Weapons");
                pack = ScriptableObject.CreateInstance<GunResourcePackSO>();
                pack.name = "AK47ResourcePack";
                AssetDatabase.CreateAsset(pack, PackPath);
            }

            var resources = new SerializedObject(pack);
            Property(resources, "_modelPrefab").objectReferenceValue = modelPrefab;
            SetAnimations(resources, "_firstPersonAnimations", "FP Layer.fp_draw_ak", "FP Layer.fp_draw_ak",
                "FP Layer.fp_shoot1_ak", "FP Layer.fp_reload_ak");
            SetAnimations(resources, "_thirdPersonAnimations", "Up Layer.tp_draw_ak", "Up Layer.tp_draw_ak",
                "Up Layer.tp_shoot_ak", "Up Layer.tp_reload_ak");
            SetAnimations(resources, "_modelAnimations", "", "Base Layer." + ModelFire,
                "Base Layer." + ModelFire, "Base Layer." + ModelReload);
            SetAudio(resources, "_fireAudio", legacyFire.Count > 0 ? legacyFire : defaultFire, legacyFire.Count > 0);
            SetAudio(resources, "_prepareAudio", new[] { legacyPrepare != null ? legacyPrepare : defaultPrepare }, legacyPrepare != null);
            SetAudio(resources, "_reloadAudio", reloadAudio, false);
            resources.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(pack);

            var definition = new SerializedObject(gun);
            Property(definition, "_resourcePack").objectReferenceValue = pack;
            Property(definition, "_controllerPrefab").objectReferenceValue = null;
            definition.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(gun);
            int removedAudioComponents = RemoveLegacyAudio();

            return new JObject
            {
                ["ok"] = true,
                ["resourcePack"] = PackPath,
                ["created"] = created,
                ["modelPrefab"] = ModelPath,
                ["modelAnimatorRoot"] = animatorRoot,
                ["actorController"] = ActorControllerPath,
                ["modelController"] = ModelControllerPath,
                ["removedPlayerAudioComponents"] = removedAudioComponents,
                ["prepareAudioCount"] = Property(resources, "_prepareAudio").arraySize,
                ["fireAudioCount"] = Property(resources, "_fireAudio").arraySize,
                ["reloadAudioCount"] = Property(resources, "_reloadAudio").arraySize
            };
        }

        [CliCommand("gun_tp_fire_repair", "Bake AK third-person recoil onto its held pose and update only its Animator state", Tags = new[] { "fpsgame/validation" })]
        public static JObject RepairThirdPersonFire()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出 Play Mode，再修复第三人称开火动画。");
            var actor = Require<AnimatorController>(ActorControllerPath);
            AnimatorStateMachine up = Layer(actor, "Up Layer");
            var source = Require<AnimationClip>(AgentRoot + "TPAnims/tp_shoot_ak.anim");
            var hold = Require<AnimationClip>(AgentRoot + "TPAnims/tp_draw_ak.anim");
            AnimationClip clip = SaveThirdPersonFire(source, hold);
            EnsureState(up, "tp_shoot_ak", clip);
            EditorUtility.SetDirty(actor);
            AssetDatabase.SaveAssetIfDirty(actor);
            return new JObject
            {
                ["ok"] = true,
                ["source"] = AssetDatabase.GetAssetPath(source),
                ["holdPose"] = AssetDatabase.GetAssetPath(hold),
                ["clip"] = ThirdPersonFirePath,
                ["duration"] = clip.length,
                ["frameRate"] = clip.frameRate
            };
        }

        private static AnimationClip SaveThirdPersonFire(AnimationClip source, AnimationClip hold)
        {
            AnimationClip baked = GunRecoilClipBaker.Bake(source, hold, "tp_shoot_ak");
            AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(ThirdPersonFirePath);
            if (saved == null)
            {
                EnsureFolder(AgentRoot + "PresentationAnims");
                AssetDatabase.CreateAsset(baked, ThirdPersonFirePath);
                saved = baked;
            }
            else
            {
                // 保留引用 GUID，使重复迁移不会把 Animator 的绑定打断。
                try { EditorUtility.CopySerialized(baked, saved); }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
                EditorUtility.SetDirty(saved);
            }
            AssetDatabase.SaveAssetIfDirty(saved);
            return saved;
        }

        private static WeaponController MigrateModel(RuntimeAnimatorController controller, AnimationClip fire,
            AnimationClip reload, out string animatorRoot)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ModelPath);
            try
            {
                Transform bindingRoot = FindBindingRoot(root.transform, fire, reload);
                Animator animator = bindingRoot.GetComponent<Animator>();
                if (animator == null) animator = bindingRoot.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.enabled = true;
                animator.applyRootMotion = false;
                animatorRoot = AnimationUtility.CalculateTransformPath(bindingRoot, root.transform);

                GunModelController generic = root.GetComponent<GunModelController>();
                foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
                    if (component != null && component.GetType().Name == "AK47Controller")
                        UnityEngine.Object.DestroyImmediate(component);
                if (generic == null) generic = root.AddComponent<GunModelController>();
                var serialized = new SerializedObject(generic);
                Property(serialized, "_animator").objectReferenceValue = animator;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, ModelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            GunModelController result = Require<GameObject>(ModelPath).GetComponent<GunModelController>();
            if (result == null) throw new InvalidOperationException("迁移后的 AK47 预制体缺少通用枪械模型控制器。");
            return result;
        }

        private static Transform FindBindingRoot(Transform root, params AnimationClip[] clips)
        {
            string[] paths = clips.SelectMany(AnimationUtility.GetCurveBindings)
                .Where(binding => binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path))
                .Select(binding => binding.path).Distinct().ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("武器动画没有可验证的 Transform 绑定。");
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                // 原始 FBX 节点名可包含字面的 '/'；Transform.Find 会把它错误拆成层级。
                // 与动画系统一致地比较 CalculateTransformPath，保留模型名称和导入层级。
                var relativePaths = new HashSet<string>(candidate.GetComponentsInChildren<Transform>(true)
                    .Select(child => AnimationUtility.CalculateTransformPath(child, candidate)));
                if (paths.All(relativePaths.Contains))
                    return candidate;
            }
            throw new InvalidOperationException("AK47 预制体内找不到与现有模型动画绑定路径匹配的 Animator 根；保留原模型层级，不猜测挂点。");
        }

        private static void ReadLegacyAudio(out List<AudioClip> fire, out AudioClip prepare)
        {
            fire = new List<AudioClip>();
            prepare = null;
            GameObject source = Require<GameObject>(PlayerPath);
            Component legacy = source.GetComponents<Component>().FirstOrDefault(component =>
                component != null && component.GetType().Name == "PlayerFireAudioTester");
            if (legacy == null) return;
            var serialized = new SerializedObject(legacy);
            SerializedProperty clips = Property(serialized, "_fireAudios");
            for (int i = 0; i < clips.arraySize; i++)
                if (clips.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip clip) fire.Add(clip);
            prepare = Property(serialized, "_prepareAudio").objectReferenceValue as AudioClip;
        }

        private static int RemoveLegacyAudio()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            int count = 0;
            try
            {
                foreach (MonoBehaviour component in root.GetComponents<MonoBehaviour>())
                {
                    if (component == null || component.GetType().Name != "PlayerFireAudioTester") continue;
                    UnityEngine.Object.DestroyImmediate(component);
                    count++;
                }
                if (count > 0) PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
                return count;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static AnimatorStateMachine Layer(AnimatorController controller, string name) =>
            controller.layers.FirstOrDefault(layer => layer.name == name)?.stateMachine ??
            throw new InvalidOperationException(controller.name + " 缺少动画层 " + name);

        private static AnimatorState EnsureState(AnimatorStateMachine machine, string name, Motion clip)
        {
            AnimatorState[] matches = machine.states.Where(child => child.state.name == name).Select(child => child.state).ToArray();
            AnimatorState keep = matches.FirstOrDefault() ?? machine.AddState(name);
            foreach (AnimatorState duplicate in matches.Skip(1))
            {
                if (machine.defaultState == duplicate) machine.defaultState = keep;
                machine.RemoveState(duplicate);
                if (duplicate != null) UnityEngine.Object.DestroyImmediate(duplicate, true);
            }
            keep.motion = clip;
            EditorUtility.SetDirty(keep);
            EditorUtility.SetDirty(machine);
            return keep;
        }

        private static void RemoveStates(AnimatorStateMachine machine, string name)
        {
            foreach (AnimatorState state in machine.states.Where(child => child.state.name == name).Select(child => child.state).ToArray())
            {
                machine.RemoveState(state);
                if (state != null) UnityEngine.Object.DestroyImmediate(state, true);
            }
            EditorUtility.SetDirty(machine);
        }

        private static void SetAnimations(SerializedObject serialized, string name, string prepare, string hold, string fire, string reload)
        {
            SerializedProperty actions = Property(serialized, name);
            actions.FindPropertyRelative("_prepare").stringValue = prepare;
            actions.FindPropertyRelative("_hold").stringValue = hold;
            actions.FindPropertyRelative("_fire").stringValue = fire;
            actions.FindPropertyRelative("_reload").stringValue = reload;
        }

        private static void SetAudio(SerializedObject serialized, string name, IEnumerable<AudioClip> clips, bool replace)
        {
            SerializedProperty list = Property(serialized, name);
            if (!replace && list.arraySize > 0) return;
            AudioClip[] values = clips.ToArray();
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static SerializedProperty Property(SerializedObject serialized, string name) =>
            serialized.FindProperty(name) ?? throw new InvalidOperationException(serialized.targetObject.GetType().Name + " 缺少序列化字段 " + name);

        private static T Require<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("缺少迁移所需资源：" + path);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
