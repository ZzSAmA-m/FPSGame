using UnityEditor;
using UnityEngine;

namespace MStudio
{
    [InitializeOnLoad]
    public class StyleHierarchy
    {
        static string[] dataArray;
        static string path;
        static ColorPalette colorPalette;

        static StyleHierarchy()
        {
            dataArray = AssetDatabase.FindAssets("t:ColorPalette");

            if (dataArray.Length >= 1)
            {
                path = AssetDatabase.GUIDToAssetPath(dataArray[0]);

                colorPalette =
                    AssetDatabase.LoadAssetAtPath<ColorPalette>(path);

                EditorApplication.hierarchyWindowItemByEntityIdOnGUI
                    += OnHierarchyWindow;
            }
        }

        private static void OnHierarchyWindow(
            EntityId entityId,
            Rect selectionRect)
        {
            if (dataArray.Length == 0)
                return;

            UnityEngine.Object instance =
                EditorUtility.EntityIdToObject(entityId);

            if (instance != null)
            {
                for (int i = 0; i < colorPalette.colorDesigns.Count; i++)
                {
                    var design = colorPalette.colorDesigns[i];

                    if (instance.name.StartsWith(design.keyChar))
                    {
                        string newName =
                            instance.name.Substring(
                                design.keyChar.Length);

                        EditorGUI.DrawRect(
                            selectionRect,
                            design.backgroundColor);

                        GUIStyle newStyle = new GUIStyle
                        {
                            alignment = design.textAlignment,
                            fontStyle = design.fontStyle,

                            normal = new GUIStyleState
                            {
                                textColor = design.textColor
                            }
                        };

                        EditorGUI.LabelField(
                            selectionRect,
                            newName.ToUpper(),
                            newStyle);
                    }
                }
            }
        }
    }
}