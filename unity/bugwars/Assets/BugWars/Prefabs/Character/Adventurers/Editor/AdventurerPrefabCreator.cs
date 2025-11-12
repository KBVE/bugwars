using UnityEngine;
using UnityEditor;
using System.IO;

namespace BugWars.Characters.Editor
{
    /// <summary>
    /// Editor utility to create character prefabs from FBX models
    /// </summary>
    public class AdventurerPrefabCreator : EditorWindow
    {
        private const string FBX_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/fbx/";
        private const string PREFAB_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/";
        private const string MATERIAL_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/Materials/";
        
        [MenuItem("BugWars/Create Adventurer Prefabs")]
        public static void CreateAllAdventurerPrefabs()
        {
            CreateCharacterPrefab("Knight");
            CreateCharacterPrefab("Barbarian");
            CreateCharacterPrefab("Mage");
            CreateCharacterPrefab("Rogue");
            CreateCharacterPrefab("Rogue_Hooded");
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log("All adventurer prefabs created successfully!");
        }
        
        private static void CreateCharacterPrefab(string characterName)
        {
            // Load the FBX model
            string fbxPath = FBX_PATH + characterName + ".fbx";
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            
            if (fbxModel == null)
            {
                Debug.LogError($"Could not find FBX model at: {fbxPath}");
                return;
            }
            
            // Instantiate the model
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
            instance.name = characterName + "_Character";
            
            // Add the AdventurerCharacter component
            AdventurerCharacter character = instance.AddComponent<AdventurerCharacter>();
            
            // Load and assign the material
            string materialName = characterName.Replace("_Hooded", "") + "_Material.mat";
            string materialFullPath = MATERIAL_PATH + materialName;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFullPath);
            
            if (material != null)
            {
                character.SetMaterial(material);
            }
            else
            {
                Debug.LogWarning($"Material not found at: {materialFullPath}");
            }
            
            // Load rig references
            GameObject generalRig = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BugWars/Prefabs/Character/Adventurers/Rig_Medium/Rig_Medium_General.fbx");
            GameObject movementRig = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BugWars/Prefabs/Character/Adventurers/Rig_Medium/Rig_Medium_MovementBasic.fbx");
            
            // Create the prefab
            string prefabPath = PREFAB_PATH + characterName + "_Prefab.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            
            // Clean up the instance
            DestroyImmediate(instance);
            
            Debug.Log($"Created prefab: {prefabPath}");
        }
        
        [MenuItem("BugWars/Setup Single Character (Test)")]
        public static void SetupTestCharacter()
        {
            // Create a Knight in the scene for testing
            string fbxPath = FBX_PATH + "Knight.fbx";
            GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            
            if (fbxModel == null)
            {
                Debug.LogError($"Could not find Knight FBX model");
                return;
            }
            
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxModel);
            instance.name = "Knight_Test_Character";
            instance.transform.position = new Vector3(0, 0, 0);
            
            // Add the AdventurerCharacter component
            AdventurerCharacter character = instance.AddComponent<AdventurerCharacter>();
            
            // Load and assign the material
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH + "Knight_Material.mat");
            if (material != null)
            {
                character.SetMaterial(material);
            }
            
            Selection.activeGameObject = instance;
            Debug.Log("Test Knight character created in scene!");
        }
    }
}