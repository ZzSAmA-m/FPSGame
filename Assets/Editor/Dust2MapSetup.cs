using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Dust2MapSetup
{
    const string VisualPath = "Assets/CS2/Maps/de_dust2/de_dust2.gltf";
    const string PhysicsPath = "Assets/CS2/Maps/de_dust2/world_physics_physics.gltf";
    const string ScenePath = "Assets/Scenes/Dust2.unity";

    [MenuItem("FPSGame/Setup Dust2 Map Now")]
    public static void Run()
    {
        Debug.Log("DUST2_SETUP_START");
        try
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
            if (visual == null)
            {
                Debug.LogError("DUST2_SETUP_FAIL missing " + VisualPath);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(visual, scene);
            root.name = "de_dust2";

            int hidden = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("blocklight") || n.Contains("toolsblock") || n.Contains("toolstrigger")
                    || n.Contains("postprocessing") || n.Contains("nav_attribute") || n.Contains("tools_cs_place")
                    || n.Contains("mesh_overlay") || n.Contains("skybox"))
                {
                    t.gameObject.SetActive(false);
                    hidden++;
                }
            }

            int colliders = 0;
            var physPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhysicsPath);
            if (physPrefab != null)
            {
                var phys = (GameObject)PrefabUtility.InstantiatePrefab(physPrefab, scene);
                phys.name = "de_dust2_physics";
                foreach (var r in phys.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;
                foreach (var mf in phys.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var n = mf.gameObject.name.ToLowerInvariant();
                    if (n.Contains("sky") || n.Contains("grenadeclip")) continue;
                    var mc = mf.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    colliders++;
                }
            }

            var pbr = Shader.Find("Shader Graphs/glTF-pbrMetallicRoughness");
            if (pbr != null)
            {
                var seen = new System.Collections.Generic.HashSet<Material>();
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || !seen.Add(m)) continue;
                        if (m.shader != null && m.shader.name == "glTF/PbrMetallicRoughness")
                            m.shader = pbr;
                    }
                }
            }

            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                cam.farClipPlane = 600f;
                cam.nearClipPlane = 0.1f;
                cam.fieldOfView = 70f;
                cam.transform.position = new Vector3(18f, 28f, -95f);
                cam.transform.rotation = Quaternion.Euler(18f, -20f, 0f);
            }

            var light = Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.95f, 0.85f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.4f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.18f);
            RenderSettings.fog = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var sb = new StringBuilder();
            sb.Append("DUST2_SETUP_DONE scene=").Append(ScenePath);
            sb.Append(" hidden=").Append(hidden);
            sb.Append(" colliders=").Append(colliders);
            sb.Append(" renderers=").Append(renderers.Length);
            Debug.Log(sb.ToString());
        }
        catch (System.Exception e)
        {
            Debug.LogError("DUST2_SETUP_FAIL " + e);
        }
    }
}
