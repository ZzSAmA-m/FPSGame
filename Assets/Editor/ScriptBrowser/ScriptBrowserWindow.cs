#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Window / 脚本浏览器。一次画出 Assets/Scripts 下全部文件夹、脚本和 public 成员，
/// 用方框和连线做成从上到下的树状图。
/// </summary>
public class ScriptBrowserWindow : EditorWindow
{
    const string RootFolder = "Assets/Scripts";
    const float HGap = 16f;
    const float VGap = 56f;
    const float Pad = 36f;

    enum NodeKind
    {
        Folder,
        File,
        Type,
        Field,
        Property,
        Event,
        Method
    }

    sealed class TreeNode
    {
        public NodeKind kind;
        public string title;
        public string subtitle;
        public string path;
        public Type type;
        public float width;
        public float height;
        public float subtreeWidth;
        public Rect rect;
        public readonly List<TreeNode> children = new();
    }

    Vector2 scroll;
    float zoom = 0.85f;
    bool panning;
    TreeNode root;
    TreeNode selected;
    Dictionary<string, List<Type>> typesByPath;
    GUIStyle titleStyle;
    GUIStyle subStyle;

    [MenuItem("Window/脚本浏览器", false, 2000)]
    public static void Open()
    {
        var window = GetWindow<ScriptBrowserWindow>();
        window.titleContent = new GUIContent("脚本浏览器", EditorGUIUtility.IconContent("cs Script Icon").image);
        window.minSize = new Vector2(720f, 420f);
        window.Show();
    }

    void OnEnable()
    {
        RebuildAll();
        EditorApplication.projectChanged += OnProjectChanged;
        AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
    }

    void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
        AssemblyReloadEvents.afterAssemblyReload -= OnAfterReload;
    }

    void OnProjectChanged()
    {
        RebuildAll();
        Repaint();
    }

    void OnAfterReload()
    {
        RebuildAll();
        Repaint();
    }

    void RebuildAll()
    {
        RebuildTypeCache();
        root = BuildFolderNode(RootFolder);
        selected = null;
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawToolbar();
        var body = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        HandleZoom(body);
        DrawGraph(body);
        DrawHoverTip();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                RebuildAll();
                scroll = Vector2.zero;
            }

            GUILayout.Space(8f);
            GUILayout.Label("缩放", EditorStyles.miniLabel, GUILayout.Width(28f));
            zoom = GUILayout.HorizontalSlider(zoom, 0.2f, 2.5f, GUILayout.Width(120f));
            GUILayout.Label($"{Mathf.RoundToInt(zoom * 100f)}%", EditorStyles.miniLabel, GUILayout.Width(36f));
            if (GUILayout.Button("适应", EditorStyles.toolbarButton, GUILayout.Width(40f)))
                zoom = 0.85f;

            GUILayout.FlexibleSpace();
            GUILayout.Label("滚轮缩放，中键拖动画布，双击脚本打开", EditorStyles.miniLabel);
        }
    }

    void HandleZoom(Rect body)
    {
        var ev = Event.current;
        if (!body.Contains(ev.mousePosition) && ev.type != EventType.MouseUp)
            return;

        if (ev.type == EventType.ScrollWheel)
        {
            var oldZoom = zoom;
            var factor = ev.delta.y > 0f ? 0.9f : 1.111111f;
            zoom = Mathf.Clamp(zoom * factor, 0.2f, 2.5f);
            var mouseInView = ev.mousePosition - body.position;
            var contentPos = (scroll + mouseInView) / Mathf.Max(0.0001f, oldZoom);
            scroll = contentPos * zoom - mouseInView;
            scroll.x = Mathf.Max(0f, scroll.x);
            scroll.y = Mathf.Max(0f, scroll.y);
            ev.Use();
            Repaint();
            return;
        }

        if (ev.button == 2)
        {
            if (ev.type == EventType.MouseDown)
            {
                panning = true;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && panning)
            {
                scroll -= ev.delta;
                scroll.x = Mathf.Max(0f, scroll.x);
                scroll.y = Mathf.Max(0f, scroll.y);
                ev.Use();
                Repaint();
            }
        }

        if (ev.type == EventType.MouseUp)
            panning = false;
    }

    void DrawHoverTip()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        var tip = GUI.tooltip;
        if (string.IsNullOrEmpty(tip))
            return;

        var content = new GUIContent(tip);
        var style = EditorStyles.helpBox;
        var width = Mathf.Min(420f, Mathf.Max(160f, style.CalcSize(content).x + 12f));
        var height = style.CalcHeight(content, width - 8f) + 8f;
        var mouse = Event.current.mousePosition;
        var r = new Rect(mouse.x + 18f, mouse.y + 18f, width, height);
        if (r.xMax > position.width - 6f)
            r.x = Mathf.Max(6f, mouse.x - r.width - 12f);
        if (r.yMax > position.height - 6f)
            r.y = Mathf.Max(6f, mouse.y - r.height - 12f);
        GUI.Box(r, content, style);
    }

    void DrawGraph(Rect body)
    {
        EditorGUI.DrawRect(body, new Color(0.97f, 0.97f, 0.97f));
        if (root == null)
            RebuildAll();
        if (root == null)
            return;

        EnsureStyles();
        Measure(root);
        Place(root, Pad, Pad);
        var content = new Rect(0f, 0f, (root.subtreeWidth + Pad * 2f) * zoom, (MaxBottom(root) + Pad) * zoom);
        scroll = GUI.BeginScrollView(body, scroll, content);
        if (Event.current.type == EventType.Repaint)
            DrawLinks(root);
        DrawNodeRecursive(root);
        GUI.EndScrollView();
    }

    Rect ZoomRect(Rect r)
    {
        return new Rect(r.x * zoom, r.y * zoom, r.width * zoom, r.height * zoom);
    }

    void EnsureStyles()
    {
        var titleSize = Mathf.Max(8, Mathf.RoundToInt(12f * zoom));
        var subSize = Mathf.Max(7, Mathf.RoundToInt(10f * zoom));
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            titleStyle.normal.textColor = Color.black;
            subStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            subStyle.normal.textColor = new Color(0.28f, 0.28f, 0.28f);
        }

        titleStyle.fontSize = titleSize;
        subStyle.fontSize = subSize;
    }

    float NodeWidth(TreeNode node)
    {
        var oldTitle = titleStyle.fontSize;
        var oldSub = subStyle.fontSize;
        titleStyle.fontSize = 12;
        subStyle.fontSize = 10;
        var pad = 18f;
        var tw = titleStyle.CalcSize(new GUIContent(node.title)).x;
        var sw = string.IsNullOrEmpty(node.subtitle) ? 0f : subStyle.CalcSize(new GUIContent(node.subtitle)).x;
        titleStyle.fontSize = oldTitle;
        subStyle.fontSize = oldSub;
        var textW = Mathf.Max(tw, sw) + pad;
        switch (node.kind)
        {
            case NodeKind.Folder: return Mathf.Clamp(textW, 120f, 240f);
            case NodeKind.File: return Mathf.Clamp(textW, 140f, 260f);
            case NodeKind.Type: return Mathf.Clamp(textW, 130f, 240f);
            default: return Mathf.Clamp(textW, 110f, 280f);
        }
    }

    static float NodeHeight(TreeNode node)
    {
        return node.kind == NodeKind.Field || node.kind == NodeKind.Property || node.kind == NodeKind.Event || node.kind == NodeKind.Method
            ? 40f
            : 46f;
    }

    float Measure(TreeNode node)
    {
        node.width = NodeWidth(node);
        node.height = NodeHeight(node);
        if (node.children.Count == 0)
        {
            node.subtreeWidth = node.width;
            return node.subtreeWidth;
        }

        var sum = 0f;
        for (var i = 0; i < node.children.Count; i++)
        {
            if (i > 0)
                sum += HGap;
            sum += Measure(node.children[i]);
        }

        node.subtreeWidth = Mathf.Max(node.width, sum);
        return node.subtreeWidth;
    }

    static void Place(TreeNode node, float left, float top)
    {
        node.rect = new Rect(left + (node.subtreeWidth - node.width) * 0.5f, top, node.width, node.height);
        if (node.children.Count == 0)
            return;

        var span = 0f;
        for (var i = 0; i < node.children.Count; i++)
        {
            if (i > 0)
                span += HGap;
            span += node.children[i].subtreeWidth;
        }

        var x = left + (node.subtreeWidth - span) * 0.5f;
        var childTop = top + node.height + VGap;
        foreach (var child in node.children)
        {
            Place(child, x, childTop);
            x += child.subtreeWidth + HGap;
        }
    }

    static float MaxBottom(TreeNode node)
    {
        var bottom = node.rect.yMax;
        foreach (var child in node.children)
            bottom = Mathf.Max(bottom, MaxBottom(child));
        return bottom;
    }

    void DrawLinks(TreeNode node)
    {
        Handles.BeginGUI();
        Handles.color = new Color(0.08f, 0.08f, 0.08f, 1f);
        DrawLinksInner(node);
        Handles.EndGUI();
        Handles.color = Color.white;
    }

    void DrawLinksInner(TreeNode node)
    {
        var parent = ZoomRect(node.rect);
        var origin = new Vector3(parent.center.x, parent.yMax, 0f);
        foreach (var child in node.children)
        {
            var destRect = ZoomRect(child.rect);
            var dest = new Vector3(destRect.center.x, destRect.yMin, 0f);
            Handles.DrawAAPolyLine(2f, origin, dest);
            DrawLinksInner(child);
        }
    }

    void DrawNodeRecursive(TreeNode node)
    {
        DrawBox(node);
        foreach (var child in node.children)
            DrawNodeRecursive(child);
    }

    void DrawBox(TreeNode node)
    {
        var r = ZoomRect(node.rect);
        var picked = selected == node;
        EditorGUI.DrawRect(r, Color.black);
        var inner = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
        EditorGUI.DrawRect(inner, picked ? new Color(0.78f, 0.90f, 1f) : FillOf(node.kind));

        var textW = inner.width - 8f;
        var shownTitle = Ellipsize(node.title, titleStyle, textW);
        var shownSub = Ellipsize(node.subtitle, subStyle, textW);
        var tip = string.IsNullOrEmpty(node.subtitle) ? node.title : node.title + "\n" + node.subtitle;

        GUI.BeginGroup(inner);
        var titleRect = new Rect(4f, 3f, inner.width - 8f, Mathf.Max(16f, inner.height * 0.48f));
        var subRect = new Rect(4f, titleRect.yMax - 1f, inner.width - 8f, inner.height - titleRect.yMax - 2f);
        GUI.Label(titleRect, new GUIContent(shownTitle, tip), titleStyle);
        if (!string.IsNullOrEmpty(shownSub))
            GUI.Label(subRect, new GUIContent(shownSub, tip), subStyle);
        GUI.EndGroup();

        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
        var ev = Event.current;
        if (ev.type != EventType.MouseDown || ev.button != 0 || !r.Contains(ev.mousePosition))
            return;

        selected = node;
        if (!string.IsNullOrEmpty(node.path))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(node.path);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
                if (ev.clickCount >= 2)
                    AssetDatabase.OpenAsset(asset);
            }
        }

        ev.Use();
        Repaint();
    }

    static string Ellipsize(string text, GUIStyle style, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 8f)
            return text ?? "";
        if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
            return text;

        const string ellipsis = "…";
        var lo = 0;
        var hi = text.Length;
        var result = ellipsis;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var candidate = text.Substring(0, mid) + ellipsis;
            if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
            {
                result = candidate;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    static Color FillOf(NodeKind kind)
    {
        switch (kind)
        {
            case NodeKind.Folder: return Color.white;
            case NodeKind.File: return new Color(0.90f, 0.95f, 1f);
            case NodeKind.Type: return new Color(0.90f, 1f, 0.92f);
            case NodeKind.Field:
            case NodeKind.Property: return new Color(0.93f, 0.97f, 1f);
            case NodeKind.Event: return new Color(1f, 0.94f, 0.88f);
            case NodeKind.Method: return new Color(0.93f, 0.92f, 1f);
            default: return Color.white;
        }
    }

    TreeNode BuildFolderNode(string folder)
    {
        var node = new TreeNode
        {
            kind = NodeKind.Folder,
            title = string.Equals(folder, RootFolder, StringComparison.OrdinalIgnoreCase) ? "Scripts" : Path.GetFileName(folder),
            subtitle = "文件夹",
            path = folder
        };

        if (!AssetDatabase.IsValidFolder(folder))
            return node;

        var subs = AssetDatabase.GetSubFolders(folder);
        Array.Sort(subs, StringComparer.OrdinalIgnoreCase);
        foreach (var sub in subs)
            node.children.Add(BuildFolderNode(sub.Replace('\\', '/')));

        foreach (var path in DirectScripts(folder))
            node.children.Add(BuildFileNode(path));

        return node;
    }

    TreeNode BuildFileNode(string path)
    {
        var node = new TreeNode
        {
            kind = NodeKind.File,
            title = Path.GetFileName(path),
            subtitle = ".cs",
            path = path
        };

        var types = TypesForPath(path);
        foreach (var type in types)
        {
            if (type.IsNested)
                continue;
            node.children.Add(BuildTypeNode(type));
        }

        return node;
    }

    TreeNode BuildTypeNode(Type type)
    {
        var node = new TreeNode
        {
            kind = NodeKind.Type,
            title = PrettyType(type),
            subtitle = TypeKindName(type),
            type = type
        };

        try
        {
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
                node.children.Add(BuildTypeNode(nested));
        }
        catch
        {
        }

        AddMembers(node, type);
        return node;
    }

    static void AddMembers(TreeNode parent, Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var field in FilterMembers(type.GetFields(flags), f => !f.IsSpecialName))
        {
            parent.children.Add(new TreeNode
            {
                kind = NodeKind.Field,
                title = field.Name,
                subtitle = (field.IsStatic ? "static " : "") + PrettyType(field.FieldType)
            });
        }

        foreach (var prop in FilterMembers(type.GetProperties(flags), p => p.GetIndexParameters().Length == 0))
        {
            parent.children.Add(new TreeNode
            {
                kind = NodeKind.Property,
                title = prop.Name,
                subtitle = (IsStatic(prop) ? "static " : "") + PrettyType(prop.PropertyType)
            });
        }

        foreach (var ev in FilterMembers(type.GetEvents(flags), _ => true))
        {
            parent.children.Add(new TreeNode
            {
                kind = NodeKind.Event,
                title = ev.Name,
                subtitle = "event"
            });
        }

        foreach (var method in FilterMembers(type.GetMethods(flags), m => !m.IsSpecialName && !m.IsConstructor))
        {
            parent.children.Add(new TreeNode
            {
                kind = NodeKind.Method,
                title = method.Name,
                subtitle = (method.IsStatic ? "static " : "") + PrettyType(method.ReturnType) + MethodArgs(method)
            });
        }
    }

    static List<string> DirectScripts(string folder)
    {
        var files = new List<string>();
        var folderNormalized = folder.Replace('\\', '/');
        foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;
            var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            if (!string.Equals(dir, folderNormalized, StringComparison.OrdinalIgnoreCase))
                continue;
            files.Add(path);
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        return files;
    }

    static string TypeKindName(Type type)
    {
        if (type.IsInterface) return "接口";
        if (type.IsEnum) return "枚举";
        if (type.IsValueType) return "结构";
        if (type.IsAbstract && type.IsSealed) return "静态类";
        if (type.IsAbstract) return "抽象类";
        return "类";
    }

    static List<T> FilterMembers<T>(T[] members, Func<T, bool> keep) where T : MemberInfo
    {
        var list = new List<T>(members.Length);
        foreach (var member in members)
        {
            if (!keep(member))
                continue;
            if (member.GetCustomAttribute<ObsoleteAttribute>() != null)
                continue;
            if (member.Name.StartsWith("<", StringComparison.Ordinal))
                continue;
            list.Add(member);
        }

        list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return list;
    }

    static bool IsStatic(PropertyInfo prop)
    {
        var m = prop.GetGetMethod(false) ?? prop.GetSetMethod(false);
        return m != null && m.IsStatic;
    }

    static string MethodArgs(MethodInfo method)
    {
        var ps = method.GetParameters();
        if (ps.Length == 0)
            return "()";
        var sb = new StringBuilder("(");
        for (var i = 0; i < ps.Length; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(ps[i].Name);
        }

        sb.Append(')');
        return sb.ToString();
    }

    static string PrettyType(Type type)
    {
        if (type == null) return "?";
        if (type == typeof(void)) return "void";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(char)) return "char";
        if (type == typeof(short)) return "short";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(int)) return "int";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(long)) return "long";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type.IsByRef) return PrettyType(type.GetElementType());
        if (type.IsArray) return PrettyType(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Nullable<>))
                return PrettyType(type.GetGenericArguments()[0]) + "?";
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0)
                name = name.Substring(0, tick);
            var args = type.GetGenericArguments();
            var sb = new StringBuilder(name);
            sb.Append('<');
            for (var i = 0; i < args.Length; i++)
            {
                if (i > 0)
                    sb.Append(", ");
                sb.Append(PrettyType(args[i]));
            }

            sb.Append('>');
            return sb.ToString();
        }

        return type.Name;
    }

    List<Type> TypesForPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath) || typesByPath == null)
            return new List<Type>();
        if (typesByPath.TryGetValue(assetPath.Replace('\\', '/'), out var list))
            return list;
        return new List<Type>();
    }

    void RebuildTypeCache()
    {
        typesByPath = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { RootFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script == null)
                continue;
            var list = new List<Type>();
            CollectTypesFromScript(script, list);
            list.Sort((a, b) =>
            {
                var nested = a.IsNested.CompareTo(b.IsNested);
                return nested != 0 ? nested : string.CompareOrdinal(a.FullName, b.FullName);
            });
            typesByPath[path] = list;
        }
    }

    static void CollectTypesFromScript(MonoScript script, List<Type> list)
    {
        var main = script.GetClass();
        if (main != null)
            AddTypeTree(main, list);

        var ns = ParseNamespace(script.text);
        var names = ParsePublicTypeNames(script.text);
        var assemblies = new List<Assembly>();
        if (main != null)
            assemblies.Add(main.Assembly);
        else
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                if (assemblyName == "Assembly-CSharp" || assemblyName == "Assembly-CSharp-firstpass")
                    assemblies.Add(assembly);
            }
        }

        foreach (var assembly in assemblies)
        {
            foreach (var typeName in names)
            {
                var type = ResolveType(assembly, ns, typeName);
                if (type != null)
                    AddTypeTree(type, list);
            }
        }
    }

    static Type ResolveType(Assembly assembly, string ns, string typeName)
    {
        var full = string.IsNullOrEmpty(ns) ? typeName : ns + "." + typeName;
        try
        {
            var direct = assembly.GetType(full, false, false);
            if (direct != null)
                return direct;
        }
        catch
        {
        }

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }
        catch
        {
            return null;
        }

        if (types == null)
            return null;

        foreach (var type in types)
        {
            if (type == null || type.IsNested)
                continue;
            if ((type.Namespace ?? "") != (ns ?? ""))
                continue;
            if (type.Name == typeName || type.Name.StartsWith(typeName + "`", StringComparison.Ordinal))
                return type;
        }

        return null;
    }

    static void AddTypeTree(Type type, List<Type> list)
    {
        if (type == null || list.Contains(type))
            return;
        if (!type.IsPublic && !type.IsNestedPublic)
            return;
        list.Add(type);
        Type[] nested;
        try
        {
            nested = type.GetNestedTypes(BindingFlags.Public);
        }
        catch
        {
            return;
        }

        foreach (var child in nested)
            AddTypeTree(child, list);
    }

    static string ParseNamespace(string source)
    {
        if (string.IsNullOrEmpty(source))
            return "";
        var match = Regex.Match(source, @"namespace\s+([\w.]+)");
        return match.Success ? match.Groups[1].Value : "";
    }

    static List<string> ParsePublicTypeNames(string source)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(source))
            return names;
        foreach (Match match in Regex.Matches(
                     source,
                     @"\bpublic\s+(?:(?:static|abstract|sealed|partial|readonly)\s+)*(?:class|struct|interface|enum|record)\s+(\w+)"))
        {
            var name = match.Groups[1].Value;
            if (!names.Contains(name))
                names.Add(name);
        }

        return names;
    }
}
#endif
