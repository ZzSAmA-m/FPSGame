using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class Cs2ContentImportSetup
{
    const string P250 = "Assets/Resources/CS2/Weapons/p250/p250.fbx";
    const string M4 = "Assets/Resources/CS2/Weapons/m4a1_silencer/m4a1_silencer.fbx";
    const string Phoenix = "Assets/Resources/CS2/Agents/tm_phoenix_meter/tm_phoenix.fbx";
    const string PhoenixRoot = "Assets/Resources/CS2/Agents/tm_phoenix_meter";

    [MenuItem("Tools/CS2/Setup Imported Content")]
    public static void Setup()
    {
        ConfigureModel(P250);
        ConfigureModel(M4);
        ConfigureModel(Phoenix);
        AssetDatabase.Refresh();
        ExtractPhoenixClips();
        BuildPhoenixMasksAndController();
        AssetDatabase.SaveAssets();
        Debug.Log("[CS2 Import] setup finished");
    }

    static void ConfigureModel(string path, bool force = false)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new System.IO.FileNotFoundException(path);
        var already =
            importer.animationType == ModelImporterAnimationType.Generic &&
            importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel &&
            importer.importAnimation &&
            importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription &&
            importer.materialLocation == ModelImporterMaterialLocation.External;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.External;
        if (!already || force)
            importer.SaveAndReimport();
    }

    static void ExtractPhoenixClips()
    {
        var tp = PhoenixRoot + "/TPAnims";
        var fp = PhoenixRoot + "/FPAnims";
        Directory.CreateDirectory(tp);
        Directory.CreateDirectory(fp);
        var clips = AssetDatabase.LoadAllAssetsAtPath(Phoenix)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__"))
            .ToList();
        foreach (var src in clips)
        {
            if (src.length <= 0.001f)
                continue;
            var shortName = CleanClipName(src.name);
            if (string.IsNullOrEmpty(shortName) || shortName.StartsWith("__preview"))
                continue;
            var folder = shortName.StartsWith("fp_") ? fp : tp;
            var dest = $"{folder}/{shortName}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(dest);
            if (existing != null)
                AssetDatabase.DeleteAsset(dest);
            var copy = Object.Instantiate(src);
            copy.name = shortName;
            AssetDatabase.CreateAsset(copy, dest);
        }
        Debug.Log($"[CS2 Import] extracted {clips.Count} phoenix clips");
    }

    static void BuildPhoenixMasksAndController()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Phoenix);
        var instance = Object.Instantiate(root);
        instance.name = root.name;
        try
        {
            var paths = new List<string>();
            Collect(instance.transform, "", paths, true);
            WriteMask(PhoenixRoot + "/MaskDown.mask", paths, IsDown);
            WriteMask(PhoenixRoot + "/MaskArm.mask", paths, IsArm);
            AssetDatabase.Refresh();
            var down = AssetDatabase.LoadAssetAtPath<AvatarMask>(PhoenixRoot + "/MaskDown.mask");
            var arm = AssetDatabase.LoadAssetAtPath<AvatarMask>(PhoenixRoot + "/MaskArm.mask");
            var controllerPath = PhoenixRoot + "/meter_phoenix_controller.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var layers = controller.layers;
            layers[0].name = "Down Layer";
            layers[0].avatarMask = down;
            layers[0].defaultWeight = 0f;
            controller.layers = layers;
            controller.AddLayer("Up Layer");
            controller.AddLayer("FP Layer");
            layers = controller.layers;
            layers[1].avatarMask = arm;
            layers[1].defaultWeight = 1f;
            layers[2].defaultWeight = 1f;
            controller.layers = layers;
            AddState(controller, 0, "tp_idle_rifle");
            AddState(controller, 0, "tp_run_n_rifle");
            AddState(controller, 1, "tp_draw_ak");
            Debug.Log($"[CS2 Import] mask paths {paths.Count}");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static void AddState(AnimatorController controller, int layer, string clipName)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{PhoenixRoot}/TPAnims/{clipName}.anim");
        if (clip == null)
        {
            Debug.LogWarning("[CS2 Import] missing state clip " + clipName);
            return;
        }
        controller.AddMotion(clip, layer);
    }

    [MenuItem("Tools/CS2/Rebuild Phoenix Controller")]
    public static void RebuildController()
    {
        ExtractWeaponClips(M4, "Assets/Resources/CS2/Weapons/m4a1_silencer");
        BuildPhoenixMasksAndController();
        AssetDatabase.SaveAssets();
        Debug.Log("[CS2 Import] controller rebuilt");
    }

    [MenuItem("Tools/CS2/Verify Imported Content")]
    public static void Verify()
    {
        var errors = new List<string>();
        CheckModel(P250, errors);
        CheckModel(M4, errors);
        CheckModel(Phoenix, errors);
        CheckHeight(errors);
        CheckClip(Phoenix, PhoenixRoot + "/TPAnims/tp_run_n_rifle.anim", errors, "root_motion");
        CheckClip(Phoenix, PhoenixRoot + "/TPAnims/tp_draw_ak.anim", errors, "wpnPivot/wpn");
        CheckClip(P250, "Assets/Resources/CS2/Weapons/p250/shoot.anim", errors, "weapon");
        CheckClip(P250, "Assets/Resources/CS2/Weapons/p250/reload.anim", errors, "magazine");
        CheckClip(M4, "Assets/Resources/CS2/Weapons/m4a1_silencer/shoot.anim", errors, "bolt");
        CheckClip(M4, "Assets/Resources/CS2/Weapons/m4a1_silencer/reload.anim", errors, "bolt");
        CheckMask(Phoenix, PhoenixRoot + "/MaskDown.mask", errors);
        CheckMask(Phoenix, PhoenixRoot + "/MaskArm.mask", errors);
        CheckController(errors);
        if (errors.Count == 0)
            Debug.Log("[CS2 Import] VERIFY_OK");
        else
            Debug.LogError("[CS2 Import] VERIFY_FAIL\n" + string.Join("\n", errors));
    }

    static void CheckModel(string path, List<string> errors)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || importer.animationType != ModelImporterAnimationType.Generic)
            errors.Add(path + " is not Generic");
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var verts = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null)
                verts += filter.sharedMesh.vertexCount;
        foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh != null)
                verts += skin.sharedMesh.vertexCount;
            foreach (var mat in skin.sharedMaterials)
                CheckMaterial(path, skin.name, mat, errors);
        }
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            foreach (var mat in renderer.sharedMaterials)
                CheckMaterial(path, renderer.name, mat, errors);
        if (verts <= 0)
            errors.Add(path + " has no vertices");
        else
            Debug.Log($"[CS2 Import] {path} verts {verts}");
    }

    static void CheckMaterial(string model, string renderer, Material mat, List<string> errors)
    {
        if (mat == null || mat.mainTexture == null)
            errors.Add($"{model} renderer {renderer} material {mat?.name ?? "null"} has no main texture");
    }

    static void CheckHeight(List<string> errors)
    {
        var phoenix = MeshHeight(Phoenix, "thirdperson_body");
        var fbi = MeshHeight("Assets/Resources/CS2/Agents/ctm_fbi_meter/ctm_fbi.fbx", "thirdperson_body");
        Debug.Log($"[CS2 Import] phoenix mesh {phoenix} fbi mesh {fbi}");
        if (fbi.y <= 0.1f || Mathf.Abs(phoenix.y - fbi.y) / fbi.y > 0.05f)
            errors.Add($"phoenix height {phoenix.y} fbi {fbi.y}");
    }

    static Vector3 MeshHeight(string model, string namePart)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(model);
        var instance = Object.Instantiate(root);
        try
        {
            var body = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.name.Contains(namePart));
            if (body == null || body.sharedMesh == null)
                return Vector3.zero;
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var vertex in body.sharedMesh.vertices)
            {
                var world = body.transform.TransformPoint(vertex);
                min = Vector3.Min(min, world);
                max = Vector3.Max(max, world);
            }
            return max - min;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static void CheckClip(string model, string clipPath, List<string> errors, string required)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
        {
            errors.Add("missing clip " + clipPath);
            return;
        }
        var paths = Hierarchy(model);
        var bindings = AnimationUtility.GetCurveBindings(clip);
        var missing = bindings.Select(b => b.path).Where(p => !string.IsNullOrEmpty(p) && !paths.Contains(p)).Distinct().ToList();
        if (bindings.Length == 0)
            errors.Add(clipPath + " has no curves");
        if (!bindings.Any(b => b.path.Contains(required)))
            errors.Add(clipPath + " missing path " + required);
        if (missing.Count > 0)
            errors.Add(clipPath + " unbound " + string.Join(", ", missing.Take(6)));
        else
            Debug.Log($"[CS2 Import] {clip.name} curves {bindings.Length}");
    }

    static void CheckMask(string model, string maskPath, List<string> errors)
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        var paths = Hierarchy(model);
        var missing = new List<string>();
        for (var i = 0; i < mask.transformCount; i++)
        {
            if (!mask.GetTransformActive(i))
                continue;
            var path = mask.GetTransformPath(i);
            if (!paths.Contains(path))
                missing.Add(path);
        }
        if (missing.Count > 0)
            errors.Add(maskPath + " missing " + string.Join(", ", missing.Take(8)));
        else
            Debug.Log($"[CS2 Import] mask ok {maskPath}");
    }

    static void CheckController(List<string> errors)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PhoenixRoot + "/meter_phoenix_controller.controller");
        if (controller == null)
        {
            errors.Add("missing phoenix controller");
            return;
        }
        var names = string.Join(",", controller.layers.Select(l => l.name));
        if (names != "Down Layer,Up Layer,FP Layer")
            errors.Add("controller layers " + names);
        if (controller.layers.Length < 2 || controller.layers[0].avatarMask == null || controller.layers[1].avatarMask == null)
            errors.Add("controller masks missing");
        for (var i = 0; i < controller.layers.Length; i++)
        {
            foreach (var state in controller.layers[i].stateMachine.states)
            {
                if (state.state.motion == null)
                    errors.Add("null motion " + state.state.name);
            }
        }
    }

    static HashSet<string> Hierarchy(string model)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(model);
        var instance = Object.Instantiate(root);
        instance.name = root.name;
        var list = new List<string>();
        Collect(instance.transform, "", list, true);
        Object.DestroyImmediate(instance);
        return new HashSet<string>(list);
    }

    [MenuItem("Tools/CS2/Import AWP Grenades")]
    public static void ImportAwpGrenades()
    {
        var weapons = new[]
        {
            "Assets/Resources/CS2/Weapons/awp/awp.fbx",
            "Assets/Resources/CS2/Weapons/smokegrenade/smokegrenade.fbx",
            "Assets/Resources/CS2/Weapons/flashbang/flashbang.fbx",
            "Assets/Resources/CS2/Weapons/hegrenade/hegrenade.fbx",
        };
        foreach (var weapon in weapons)
        {
            ConfigureModel(weapon);
            ExtractWeaponClips(weapon, Path.GetDirectoryName(weapon).Replace('\\', '/'));
        }
        AssignWeaponMaterial(
            "Assets/Resources/CS2/Weapons/flashbang/flashbang.fbx",
            "weapon_flashbang",
            "Assets/Resources/CS2/Weapons/flashbang/Materials/weapon_flashbang.mat",
            "Assets/Resources/CS2/Weapons/flashbang/Textures/flashbang_albedo.png",
            "Assets/Resources/CS2/Weapons/flashbang/Textures/flashbang_normal.png",
            "Assets/Resources/CS2/Weapons/flashbang/Textures/flashbang_orm.png");
        AssignWeaponMaterial(
            "Assets/Resources/CS2/Weapons/hegrenade/hegrenade.fbx",
            "weapon_hegrenade",
            "Assets/Resources/CS2/Weapons/hegrenade/Materials/weapon_hegrenade.mat",
            "Assets/Resources/CS2/Weapons/hegrenade/Textures/hegrenade_albedo.png",
            "Assets/Resources/CS2/Weapons/hegrenade/Textures/hegrenade_normal.png",
            "Assets/Resources/CS2/Weapons/hegrenade/Textures/hegrenade_orm.png");
        ExtractAgentPack("Assets/Resources/CS2/Agents/ctm_fbi_meter/awp_grenades.fbx", "Assets/Resources/CS2/Agents/ctm_fbi_meter");
        ExtractAgentPack("Assets/Resources/CS2/Agents/tm_phoenix_meter/awp_grenades.fbx", PhoenixRoot);
        AssetDatabase.SaveAssets();
        Debug.Log("[CS2 Import] awp grenades imported");
    }

    [MenuItem("Tools/CS2/Import XM1014")]
    public static void ImportXm1014()
    {
        const string weapon = "Assets/Resources/CS2/Weapons/xm1014/xm1014.fbx";
        ConfigureModel(weapon, true);
        ExtractWeaponClips(weapon, "Assets/Resources/CS2/Weapons/xm1014");
        const string fbi = "Assets/Resources/CS2/Agents/ctm_fbi_meter/xm1014_anims.fbx";
        const string phoenix = "Assets/Resources/CS2/Agents/tm_phoenix_meter/xm1014_anims.fbx";
        ConfigureModel(fbi, true);
        ConfigureModel(phoenix, true);
        ExtractAgentPack(fbi, "Assets/Resources/CS2/Agents/ctm_fbi_meter");
        ExtractAgentPack(phoenix, PhoenixRoot);
        AssetDatabase.SaveAssets();
        Debug.Log("[CS2 Import] xm1014 imported");
    }

    static readonly string[] RemainingGuns =
    {
        "aug", "bizon", "cz75a", "deagle", "elite", "famas", "fiveseven", "g3sg1", "galilar",
        "glock18", "hkp2000", "m249", "m4a4", "mac10", "mag7", "mp5sd", "mp7", "mp9", "negev",
        "nova", "p90", "revolver", "sawedoff", "scar20", "sg556", "ssg08", "tec9", "ump45", "usp_silencer",
    };

    [MenuItem("Tools/CS2/Import Remaining Guns")]
    public static void ImportRemainingGuns()
    {
        foreach (var id in RemainingGuns)
        {
            var folder = $"Assets/Resources/CS2/Weapons/{id}";
            var weapon = $"{folder}/{id}.fbx";
            if (AssetImporter.GetAtPath(weapon) == null)
            {
                Debug.LogWarning("[CS2 Import] missing " + weapon);
                continue;
            }
            var hasClips = Directory.Exists(folder) && Directory.GetFiles(folder, "*.anim").Length > 0;
            ConfigureModel(weapon, !hasClips);
            if (!hasClips)
                ExtractWeaponClips(weapon, folder);
        }
        const string fbi = "Assets/Resources/CS2/Agents/ctm_fbi_meter/remaining_guns.fbx";
        const string phoenix = "Assets/Resources/CS2/Agents/tm_phoenix_meter/remaining_guns.fbx";
        if (AssetImporter.GetAtPath(fbi) != null)
        {
            ConfigureModel(fbi, true);
            ExtractAgentPack(fbi, "Assets/Resources/CS2/Agents/ctm_fbi_meter");
        }
        if (AssetImporter.GetAtPath(phoenix) != null)
        {
            ConfigureModel(phoenix, true);
            ExtractAgentPack(phoenix, PhoenixRoot);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[CS2 Import] remaining guns imported");
    }

    [MenuItem("Tools/CS2/Verify XM1014")]
    public static void VerifyXm1014()
    {
        var errors = new List<string>();
        const string weapon = "Assets/Resources/CS2/Weapons/xm1014/xm1014.fbx";
        CheckModel(weapon, errors);
        CheckClip(weapon, "Assets/Resources/CS2/Weapons/xm1014/shoot.anim", errors, "weapon");
        CheckClip(weapon, "Assets/Resources/CS2/Weapons/xm1014/reload.anim", errors, "weapon");
        CheckClip("Assets/Resources/CS2/Agents/ctm_fbi_meter/ctm_fbi.fbx",
            "Assets/Resources/CS2/Agents/ctm_fbi_meter/TPAnims/tp_draw_xm1014.anim", errors, "wpn");
        CheckClip("Assets/Resources/CS2/Agents/ctm_fbi_meter/ctm_fbi.fbx",
            "Assets/Resources/CS2/Agents/ctm_fbi_meter/FPAnims/fp_shoot1_xm1014.anim", errors, "wpn");
        CheckClip(Phoenix, PhoenixRoot + "/TPAnims/tp_reload_xm1014.anim", errors, "wpn");
        CheckClip(Phoenix, PhoenixRoot + "/FPAnims/fp_draw_xm1014.anim", errors, "wpn");
        if (errors.Count == 0)
            Debug.Log("[CS2 Import] XM1014_OK");
        else
            Debug.LogError("[CS2 Import] XM1014_FAIL\n" + string.Join("\n", errors));
    }

    static void AssignWeaponMaterial(string fbx, string sourceName, string matPath, string albedo, string normal, string orm)
    {
        var mat = MakeMat(matPath, albedo, normal, orm);
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        Remap(importer, sourceName, mat);
        importer.SaveAndReimport();
    }

    static void ExtractAgentPack(string fbx, string root)
    {
        ConfigureModel(fbx);
        var tp = root + "/TPAnims";
        var fp = root + "/FPAnims";
        Directory.CreateDirectory(tp);
        Directory.CreateDirectory(fp);
        var count = 0;
        foreach (var src in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>())
        {
            if (src == null || src.name.StartsWith("__preview") || src.name.StartsWith("Key|") || src.length <= 0.001f)
                continue;
            if (AnimationUtility.GetCurveBindings(src).Length == 0)
                continue;
            var shortName = CleanClipName(src.name);
            var folder = shortName.StartsWith("fp_") ? fp : tp;
            var dest = $"{folder}/{shortName}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(dest) != null)
                AssetDatabase.DeleteAsset(dest);
            var copy = Object.Instantiate(src);
            copy.name = shortName;
            if (shortName.Contains("idle") || shortName.Contains("throwcharge"))
            {
                var settings = AnimationUtility.GetAnimationClipSettings(copy);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(copy, settings);
            }
            AssetDatabase.CreateAsset(copy, dest);
            count++;
        }
        Debug.Log($"[CS2 Import] extracted {count} clips from {fbx}");
    }

    [MenuItem("Tools/CS2/Verify AWP Grenades")]
    public static void VerifyAwpGrenades()
    {
        var errors = new List<string>();
        var weapons = new[]
        {
            "Assets/Resources/CS2/Weapons/awp/awp.fbx",
            "Assets/Resources/CS2/Weapons/smokegrenade/smokegrenade.fbx",
            "Assets/Resources/CS2/Weapons/flashbang/flashbang.fbx",
            "Assets/Resources/CS2/Weapons/hegrenade/hegrenade.fbx",
        };
        foreach (var weapon in weapons)
            CheckModel(weapon, errors);
        CheckClip(weapons[0], "Assets/Resources/CS2/Weapons/awp/shoot.anim", errors, "weapon");
        CheckClip(weapons[0], "Assets/Resources/CS2/Weapons/awp/reload.anim", errors, "weapon");
        CheckClip(weapons[1], "Assets/Resources/CS2/Weapons/smokegrenade/thrown.anim", errors, "weapon");
        CheckClip(weapons[2], "Assets/Resources/CS2/Weapons/flashbang/thrown.anim", errors, "weapon");
        CheckClip(weapons[3], "Assets/Resources/CS2/Weapons/hegrenade/thrown.anim", errors, "weapon");
        CheckAgent("Assets/Resources/CS2/Agents/ctm_fbi_meter/ctm_fbi.fbx", "Assets/Resources/CS2/Agents/ctm_fbi_meter", errors);
        CheckAgent(Phoenix, PhoenixRoot, errors);
        if (errors.Count == 0)
            Debug.Log("[CS2 Import] AWP_GRENADE_OK");
        else
            Debug.LogError("[CS2 Import] AWP_GRENADE_FAIL\n" + string.Join("\n", errors));
    }

    static void CheckAgent(string model, string root, List<string> errors)
    {
        CheckClip(model, root + "/TPAnims/tp_throw_overhand_grenade.anim", errors, "wpn");
        CheckClip(model, root + "/FPAnims/fp_pullpin_flashbang.anim", errors, "wpn");
        CheckClip(model, root + "/FPAnims/fp_throw_overhand_hegrenade.anim", errors, "wpn");
        CheckClip(model, root + "/FPAnims/fp_draw_smoke.anim", errors, "wpn");
        CheckClip(model, root + "/TPAnims/tp_draw_awp.anim", errors, "wpn");
    }

    [MenuItem("Tools/CS2/Finish Imported Content")]
    public static void Finish()
    {
        ExtractWeaponClips(P250, "Assets/Resources/CS2/Weapons/p250");
        ExtractWeaponClips(M4, "Assets/Resources/CS2/Weapons/m4a1_silencer");
        EnableLocomotionLoop();
        AssignPhoenixMaterials();
        AssetDatabase.SaveAssets();
        Debug.Log("[CS2 Import] finish done");
    }

    static void EnableLocomotionLoop()
    {
        var folders = new[] { PhoenixRoot + "/TPAnims", PhoenixRoot + "/FPAnims" };
        var count = 0;
        foreach (var folder in folders)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || !ShouldLoop(clip.name))
                    continue;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.loopTime)
                    continue;
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                count++;
            }
        }
        Debug.Log($"[CS2 Import] looped {count} locomotion clips");
    }

    static bool ShouldLoop(string name)
    {
        if (name.Contains("_walk_") || name.Contains("_run_") || name.Contains("_inair_")
            || name.Contains("breathing") || name.Contains("_loop"))
            return true;
        if (name.StartsWith("tp_idle") || name.StartsWith("fp_idle"))
            return true;
        return name.Contains("_crouch_")
            && !name.Contains("jump")
            && !name.Contains("turn")
            && !name.Contains("planted")
            && !name.Contains("draw")
            && !name.Contains("reload")
            && !name.Contains("shoot");
    }

    static void ExtractWeaponClips(string fbx, string folder)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview") && c.length > 0.001f);
        foreach (var src in clips)
        {
            var shortName = CleanClipName(src.name);
            var dest = $"{folder}/{shortName}.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(dest) != null)
                AssetDatabase.DeleteAsset(dest);
            var copy = Object.Instantiate(src);
            copy.name = shortName;
            AssetDatabase.CreateAsset(copy, dest);
        }
    }

    static void AssignPhoenixMaterials()
    {
        var texRoot = PhoenixRoot + "/Textures";
        var matRoot = PhoenixRoot + "/Materials";
        Directory.CreateDirectory(matRoot);
        var body = MakeMat(matRoot + "/tm_phoenix_body.mat",
            texRoot + "/tm_phoenix_v2_body_variante_color_psd_a036cb51.png",
            texRoot + "/tm_phoenix_body_normal_psd_bdeb78fa.png",
            texRoot + "/tm_phoenix_v2_body_ao_psd_3f117e67_orm_1554291442.png");
        var legs = MakeMat(matRoot + "/tm_phoenix_legs.mat",
            texRoot + "/tm_phoenix_v2_legs_variantb_color_psd_74802a99.png",
            texRoot + "/tm_phoenix_legs_normal_psd_881fe0ed.png",
            texRoot + "/tm_phoenix_legs_ao_psd_d67a962e_orm_2458679251.png");
        var head = MakeMat(matRoot + "/tm_phoenix_head.mat",
            texRoot + "/tm_phoenix_v2_balaclava_varianta_color_psd_3019cf96.png",
            texRoot + "/tm_phoenix_v2_balaclava_normal_psd_d991e591.png",
            texRoot + "/tm_phoenix_v2_balaclava_cloth_psd_4adffbf0_orm_258026592.png");
        var arms = MakeMat(matRoot + "/tm_phoenix_arms.mat",
            texRoot + "/bare_arm_133_color_psd_5b76e0cf.png",
            texRoot + "/bare_arm_normal_tga_c9fefc2d.png",
            null);
        var gloves = MakeMat(matRoot + "/tm_phoenix_gloves.mat",
            texRoot + "/glove_fullfinger_color_psd_935f70c2.png",
            texRoot + "/glove_fullfinger_normal_tga_c013e034.png",
            texRoot + "/glove_fullfinger_ao_psd_37541d21_orm_1139827744.png");

        var importer = (ModelImporter)AssetImporter.GetAtPath(Phoenix);
        Remap(importer, "tm_phoenix_v2_body_variante", body);
        Remap(importer, "tm_phoenix_v2_legs_variantb", legs);
        Remap(importer, "tm_phoenix_v2_balaclava_varianta", head);
        Remap(importer, "bare_arm_133", arms);
        Remap(importer, "glove_fullfinger", gloves);
        importer.SaveAndReimport();
    }

    static void Remap(ModelImporter importer, string sourceName, Material material)
    {
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), sourceName), material);
        Debug.Log("[CS2 Import] remap material " + sourceName);
    }

    static Material MakeMat(string path, string albedo, string normal, string orm)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);
        var mat = new Material(Shader.Find("Standard"));
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(albedo));
        var normalTex = string.IsNullOrEmpty(normal) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
        mat.SetTexture("_BumpMap", normalTex);
        if (normalTex != null)
            mat.EnableKeyword("_NORMALMAP");
        var ormTex = string.IsNullOrEmpty(orm) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(orm);
        if (ormTex != null)
        {
            mat.SetTexture("_MetallicGlossMap", ormTex);
            mat.SetFloat("_Metallic", 1f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static string CleanClipName(string name)
    {
        name = name.Replace('\\', '/');
        var pipe = name.LastIndexOf('|');
        if (pipe >= 0 && pipe < name.Length - 1)
            name = name.Substring(pipe + 1);
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }

    static void Collect(Transform t, string parent, List<string> paths, bool isRoot)
    {
        var path = isRoot ? "" : (string.IsNullOrEmpty(parent) ? t.name : parent + "/" + t.name);
        if (!isRoot)
            paths.Add(path);
        var next = isRoot ? "" : path;
        for (var i = 0; i < t.childCount; i++)
            Collect(t.GetChild(i), next, paths, false);
    }

    static bool IsDown(string path)
    {
        return path.Contains("/pelvis") && !path.Contains("/spine_0") && !path.Contains("/wpn");
    }

    static bool IsArm(string path)
    {
        return path.Contains("/spine_0") || path.Contains("/wpnPivot") || path.EndsWith("/wpn");
    }

    static void WriteMask(string path, List<string> paths, System.Func<string, bool> active)
    {
        var mask = new AvatarMask();
        mask.transformCount = paths.Count;
        for (var i = 0; i < paths.Count; i++)
        {
            mask.SetTransformPath(i, paths[i]);
            mask.SetTransformActive(i, active(paths[i]));
        }
        if (AssetDatabase.LoadAssetAtPath<AvatarMask>(path) != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mask, path);
    }
}
