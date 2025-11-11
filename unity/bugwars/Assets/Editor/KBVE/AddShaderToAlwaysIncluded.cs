using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Linq;

namespace BugWars.Editor
{
    /// <summary>
    /// Editor utility to add the Samurai shader to "Always Included Shaders" list
    /// This ensures the shader is included in builds
    /// </summary>
    public class AddShaderToAlwaysIncluded : EditorWindow
    {
        [MenuItem("KBVE/Add Samurai Shader to Always Included")]
        public static void AddSamuraiShader()
        {
            // Find the shader
            Shader samuraiShader = Shader.Find("BugWars/SamuraiAnimatedSprite_Unity6");

            if (samuraiShader == null)
            {
                Debug.LogError("[AddShaderToAlwaysIncluded] Could not find shader 'BugWars/SamuraiAnimatedSprite_Unity6'");
                return;
            }

            // Get current graphics settings
            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            var serializedObject = new SerializedObject(graphicsSettings);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            // Check if shader is already in the list
            bool alreadyIncluded = false;
            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var shader = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader == samuraiShader)
                {
                    alreadyIncluded = true;
                    break;
                }
            }

            if (alreadyIncluded)
            {
                Debug.Log("[AddShaderToAlwaysIncluded] Shader is already in 'Always Included Shaders' list");
                return;
            }

            // Add shader to the list
            arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
            var newElement = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
            newElement.objectReferenceValue = samuraiShader;

            serializedObject.ApplyModifiedProperties();

            Debug.Log("[AddShaderToAlwaysIncluded] Successfully added 'BugWars/SamuraiAnimatedSprite_Unity6' to Always Included Shaders");
            Debug.Log($"[AddShaderToAlwaysIncluded] Total shaders in list: {arrayProp.arraySize}");
        }

        [MenuItem("KBVE/List Always Included Shaders")]
        public static void ListAlwaysIncludedShaders()
        {
            var graphicsSettings = GraphicsSettings.GetGraphicsSettings();
            var serializedObject = new SerializedObject(graphicsSettings);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            Debug.Log("=== ALWAYS INCLUDED SHADERS ===");
            Debug.Log($"Total: {arrayProp.arraySize}");

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var shader = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null)
                {
                    Debug.Log($"  [{i}] {shader.name}");
                }
                else
                {
                    Debug.Log($"  [{i}] NULL");
                }
            }

            Debug.Log("=== END LIST ===");
        }
    }
}
