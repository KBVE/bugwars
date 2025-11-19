using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace BugWars.Editor
{
    /// <summary>
    /// Helper to ensure environment prefab materials are included in WebGL builds
    /// Adds sample objects to Credits scene to prevent shader stripping
    /// </summary>
    public static class EnvironmentShaderInclusionHelper
    {
        [MenuItem("KBVE/Tools/Add Environment Prefabs to Credits Scene (Fix WebGL Invisibility)")]
        public static void AddEnvironmentPrefabsToCreditsScene()
        {
            // Load Credits scene
            Scene creditsScene = EditorSceneManager.OpenScene("Assets/Scenes/Credits.unity", OpenSceneMode.Single);

            // Create a container for environment shader samples
            GameObject container = GameObject.Find("_EnvironmentShaderSamples");
            if (container == null)
            {
                container = new GameObject("_EnvironmentShaderSamples");
                container.transform.position = new Vector3(10000, 10000, 10000); // Far away from camera
            }

            int added = 0;
            float spacing = 10f; // Spacing between objects
            int objectIndex = 0;

            // Add ALL tree prefabs (to ensure all unique materials are included)
            string[] treePaths = System.IO.Directory.GetFiles(
                "Assets/Resources/Prefabs/Forest/Trees",
                "*.prefab",
                System.IO.SearchOption.TopDirectoryOnly);

            foreach (string treePath in treePaths)
            {
                GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePath);
                if (treePrefab != null)
                {
                    GameObject treeInstance = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, container.transform);
                    treeInstance.transform.localPosition = new Vector3(objectIndex * spacing, 0, 0);
                    treeInstance.SetActive(true); // ENABLED for safer shader inclusion

                    // Remove interaction components to make these pure shader references
                    var interactable = treeInstance.GetComponent<BugWars.Interaction.InteractableObject>();
                    if (interactable != null)
                        UnityEngine.Object.DestroyImmediate(interactable);

                    // Remove colliders - we only need the renderer for shader inclusion
                    var colliders = treeInstance.GetComponents<Collider>();
                    foreach (var col in colliders)
                        UnityEngine.Object.DestroyImmediate(col);

                    added++;
                    objectIndex++;
                    Debug.Log($"Added tree sample: {treePrefab.name}");
                }
            }

            // Add ALL bush prefabs
            string[] bushPaths = System.IO.Directory.GetFiles(
                "Assets/Resources/Prefabs/Forest/Bushes",
                "*.prefab",
                System.IO.SearchOption.TopDirectoryOnly);

            foreach (string bushPath in bushPaths)
            {
                GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bushPath);
                if (bushPrefab != null)
                {
                    GameObject bushInstance = (GameObject)PrefabUtility.InstantiatePrefab(bushPrefab, container.transform);
                    bushInstance.transform.localPosition = new Vector3(objectIndex * spacing, 0, 0);
                    bushInstance.SetActive(true); // ENABLED for safer shader inclusion

                    // Remove interaction components to make these pure shader references
                    var interactable = bushInstance.GetComponent<BugWars.Interaction.InteractableObject>();
                    if (interactable != null)
                        UnityEngine.Object.DestroyImmediate(interactable);

                    // Remove colliders - we only need the renderer for shader inclusion
                    var colliders = bushInstance.GetComponents<Collider>();
                    foreach (var col in colliders)
                        UnityEngine.Object.DestroyImmediate(col);

                    added++;
                    objectIndex++;
                    Debug.Log($"Added bush sample: {bushPrefab.name}");
                }
            }

            // Add ALL rock prefabs
            string[] rockPaths = System.IO.Directory.GetFiles(
                "Assets/Resources/Prefabs/Forest/Rocks",
                "*.prefab",
                System.IO.SearchOption.TopDirectoryOnly);

            foreach (string rockPath in rockPaths)
            {
                GameObject rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);
                if (rockPrefab != null)
                {
                    GameObject rockInstance = (GameObject)PrefabUtility.InstantiatePrefab(rockPrefab, container.transform);
                    rockInstance.transform.localPosition = new Vector3(objectIndex * spacing, 0, 0);
                    rockInstance.SetActive(true); // ENABLED for safer shader inclusion

                    // Remove interaction components to make these pure shader references
                    var interactable = rockInstance.GetComponent<BugWars.Interaction.InteractableObject>();
                    if (interactable != null)
                        UnityEngine.Object.DestroyImmediate(interactable);

                    // Remove colliders - we only need the renderer for shader inclusion
                    var colliders = rockInstance.GetComponents<Collider>();
                    foreach (var col in colliders)
                        UnityEngine.Object.DestroyImmediate(col);

                    added++;
                    objectIndex++;
                    Debug.Log($"Added rock sample: {rockPrefab.name}");
                }
            }

            // Save scene
            EditorSceneManager.MarkSceneDirty(creditsScene);
            EditorSceneManager.SaveScene(creditsScene);

            EditorUtility.DisplayDialog(
                "Environment Shaders Added",
                $"Added {added} sample environment objects to Credits scene.\n\n" +
                "These disabled objects ensure environment materials/shaders are included in WebGL builds.\n\n" +
                "Rebuild your WebGL build to see environment objects.",
                "OK");

            Debug.Log($"[EnvironmentShaderInclusion] Added {added} sample objects to Credits scene to prevent shader stripping");
        }
    }
}
