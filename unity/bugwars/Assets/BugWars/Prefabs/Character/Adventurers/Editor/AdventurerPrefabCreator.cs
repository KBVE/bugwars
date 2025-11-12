using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BugWars.Characters.Editor
{
    /// <summary>
    /// Editor utility to create and sync character prefabs from FBX models
    /// </summary>
    public class AdventurerPrefabCreator : EditorWindow
    {
        private const string FBX_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/fbx/";
        private const string PREFAB_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/";
        private const string MATERIAL_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/Materials/";
        private const string RIG_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/Rig_Medium/";
        private const string ANIMATOR_PATH = "Assets/BugWars/Prefabs/Character/Adventurers/Animators/";

        private static readonly string[] CHARACTER_NAMES = new string[]
        {
            "Knight",
            "Barbarian",
            "Mage",
            "Rogue",
            "Rogue_Hooded"
        };

        #region Menu Items

        /// <summary>
        /// All-in-one sync function: Creates, syncs, and verifies all adventurer prefabs
        /// </summary>
        [MenuItem("KBVE/Characters/Sync Adventurers")]
        public static void SyncAdventurers()
        {
            Debug.Log("=== KBVE Adventurer Sync - All-In-One ===");
            Debug.Log("Creating, syncing, and verifying all adventurer characters...\n");

            StringBuilder fullReport = new StringBuilder();
            fullReport.AppendLine("╔════════════════════════════════════════════════════╗");
            fullReport.AppendLine("║     KBVE ADVENTURER SYNC - COMPLETE REPORT        ║");
            fullReport.AppendLine("╚════════════════════════════════════════════════════╝\n");

            // PHASE 1: Load Rigs
            fullReport.AppendLine("PHASE 1: Loading Animation Rigs");
            fullReport.AppendLine("--------------------------------");

            // Load all rig files
            GameObject generalRig = LoadRig("Rig_Medium_General.fbx");
            GameObject movementBasicRig = LoadRig("Rig_Medium_MovementBasic.fbx");
            GameObject movementAdvancedRig = LoadRig("Rig_Medium_MovementAdvanced.fbx");
            GameObject combatMeleeRig = LoadRig("Rig_Medium_CombatMelee.fbx");
            GameObject combatRangedRig = LoadRig("Rig_Medium_CombatRanged.fbx");
            GameObject simulationRig = LoadRig("Rig_Medium_Simulation.fbx");
            GameObject specialRig = LoadRig("Rig_Medium_Special.fbx");

            // Verify critical rigs loaded
            if (generalRig == null || movementBasicRig == null)
            {
                fullReport.AppendLine("✗ FATAL: Failed to load critical rig files! Cannot proceed.");
                Debug.LogError(fullReport.ToString());
                return;
            }

            fullReport.AppendLine($"✓ Loaded: {generalRig.name}");
            fullReport.AppendLine($"✓ Loaded: {movementBasicRig.name}");
            if (movementAdvancedRig != null) fullReport.AppendLine($"✓ Loaded: {movementAdvancedRig.name}");
            if (combatMeleeRig != null) fullReport.AppendLine($"✓ Loaded: {combatMeleeRig.name}");
            if (combatRangedRig != null) fullReport.AppendLine($"✓ Loaded: {combatRangedRig.name}");
            if (simulationRig != null) fullReport.AppendLine($"✓ Loaded: {simulationRig.name}");
            if (specialRig != null) fullReport.AppendLine($"✓ Loaded: {specialRig.name}");
            fullReport.AppendLine();

            // PHASE 2: Create & Sync
            fullReport.AppendLine("PHASE 2: Creating & Syncing Prefabs");
            fullReport.AppendLine("------------------------------------");

            int successCount = 0;
            int errorCount = 0;

            foreach (string characterName in CHARACTER_NAMES)
            {
                bool success = SyncCharacterPrefab(
                    characterName,
                    generalRig,
                    movementBasicRig,
                    movementAdvancedRig,
                    combatMeleeRig,
                    combatRangedRig,
                    simulationRig,
                    specialRig,
                    fullReport);
                if (success)
                    successCount++;
                else
                    errorCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            fullReport.AppendLine($"\nSync Results: {successCount} succeeded, {errorCount} failed\n");

            // PHASE 3: Verification
            fullReport.AppendLine("PHASE 3: Verification Check");
            fullReport.AppendLine("---------------------------");

            bool allValid = true;
            foreach (string characterName in CHARACTER_NAMES)
            {
                bool isValid = VerifyCharacterPrefab(characterName, fullReport);
                if (!isValid)
                    allValid = false;
            }

            // PHASE 4: Apply Pixel Shader
            fullReport.AppendLine("\nPHASE 4: Applying Pixel Art Shader");
            fullReport.AppendLine("-----------------------------------");

            string shaderPath = "Assets/BugWars/Prefabs/Character/Adventurers/Shaders/PixelArtCharacter.shader";
            Shader pixelShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

            int shaderAppliedCount = 0;
            if (pixelShader != null)
            {
                fullReport.AppendLine($"✓ Loaded shader: {pixelShader.name}");

                foreach (string characterName in CHARACTER_NAMES)
                {
                    string materialName = characterName.Replace("_Hooded", "") + "_Material.mat";
                    string materialFullPath = MATERIAL_PATH + materialName;
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFullPath);

                    if (material != null)
                    {
                        // Only update if not already using the pixel shader
                        if (material.shader != pixelShader)
                        {
                            Texture mainTex = material.mainTexture;
                            Color color = material.HasProperty("_Color") ? material.color : Color.white;

                            material.shader = pixelShader;
                            material.mainTexture = mainTex;
                            if (material.HasProperty("_Color"))
                                material.SetColor("_Color", color);

                            // Set default pixel art parameters (more aggressive for noticeable effect)
                            if (material.HasProperty("_PixelSize"))
                                material.SetFloat("_PixelSize", 0.05f);  // Increased from 0.02
                            if (material.HasProperty("_TexturePixelation"))
                                material.SetFloat("_TexturePixelation", 16f);  // Increased from 8
                            if (material.HasProperty("_OutlineWidth"))
                                material.SetFloat("_OutlineWidth", 0.015f);  // Slightly thicker
                            if (material.HasProperty("_OutlineColor"))
                                material.SetColor("_OutlineColor", Color.black);
                            if (material.HasProperty("_VertexQuantization"))
                                material.SetFloat("_VertexQuantization", 0.7f);  // Increased from 0.5
                            if (material.HasProperty("_QuantizationSize"))
                                material.SetFloat("_QuantizationSize", 0.15f);  // Increased from 0.1
                            if (material.HasProperty("_AmbientStrength"))
                                material.SetFloat("_AmbientStrength", 0.3f);
                            if (material.HasProperty("_DiffuseStrength"))
                                material.SetFloat("_DiffuseStrength", 0.7f);

                            EditorUtility.SetDirty(material);
                            fullReport.AppendLine($"✓ Applied pixel shader to: {materialName}");
                            shaderAppliedCount++;
                        }
                        else
                        {
                            fullReport.AppendLine($"• {materialName} already using pixel shader");
                        }
                    }
                }

                AssetDatabase.SaveAssets();
                fullReport.AppendLine($"\nPixel Shader Applied: {shaderAppliedCount} material(s) updated");
            }
            else
            {
                fullReport.AppendLine($"⚠ Pixel shader not found at: {shaderPath}");
                fullReport.AppendLine("  Materials will use default shader. Run 'Apply Pixel Shader to Materials' manually.");
            }

            // Final Summary
            fullReport.AppendLine("\n╔════════════════════════════════════════════════════╗");
            if (allValid && errorCount == 0)
            {
                fullReport.AppendLine("║  ✓ SUCCESS - All adventurers ready to use!        ║");
            }
            else if (errorCount > 0)
            {
                fullReport.AppendLine("║  ✗ ERRORS - Some characters failed to sync        ║");
            }
            else
            {
                fullReport.AppendLine("║  ⚠ WARNINGS - Characters synced but have issues   ║");
            }
            fullReport.AppendLine("╚════════════════════════════════════════════════════╝");

            Debug.Log(fullReport.ToString());

            // Show summary dialog in editor
            if (allValid && errorCount == 0)
            {
                string shaderInfo = pixelShader != null
                    ? $"\n\nPixel Art Shader: Applied to {shaderAppliedCount} material(s)"
                    : "";

                EditorUtility.DisplayDialog(
                    "Adventurer Sync Complete",
                    $"Successfully synced all {successCount} adventurer characters!\n\n" +
                    "All rigs, materials, and components are configured." + shaderInfo + "\n\n" +
                    "Characters are ready to use in your scenes.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Adventurer Sync Complete with Issues",
                    $"Synced: {successCount}\nFailed: {errorCount}\n\n" +
                    "Check the Console for detailed report.",
                    "OK");
            }
        }

        [MenuItem("KBVE/Characters/Setup Test Character")]
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
            Debug.Log("✓ Test Knight character created in scene at (0, 0, 0)!");
        }

        #endregion

        #region Core Functions

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

            // Create the prefab
            string prefabPath = PREFAB_PATH + characterName + "_Prefab.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

            // Clean up the instance
            DestroyImmediate(instance);

            Debug.Log($"Created prefab: {prefabPath}");
        }

        private static bool SyncCharacterPrefab(
            string characterName,
            GameObject generalRig,
            GameObject movementBasicRig,
            GameObject movementAdvancedRig,
            GameObject combatMeleeRig,
            GameObject combatRangedRig,
            GameObject simulationRig,
            GameObject specialRig,
            StringBuilder report)
        {
            string prefabPath = PREFAB_PATH + characterName + "_Prefab.prefab";
            bool prefabExisted = File.Exists(prefabPath);

            // Only create if prefab doesn't exist (preserves GUIDs)
            if (!prefabExisted)
            {
                report.AppendLine($"  {characterName}: Creating new prefab...");
                CreateCharacterPrefab(characterName);

                // Reload the prefab
                AssetDatabase.Refresh();
                report.AppendLine($"  {characterName}: Prefab created");
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.AppendLine($"✗ {characterName}: Failed to load prefab");
                return false;
            }

            // Load prefab contents
            string prefabRoot = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefab);
            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);

            if (instance == null)
            {
                report.AppendLine($"✗ {characterName}: Failed to load prefab contents");
                return false;
            }

            try
            {
                // Get or add AdventurerCharacter component
                AdventurerCharacter character = instance.GetComponent<AdventurerCharacter>();
                if (character == null)
                {
                    character = instance.AddComponent<AdventurerCharacter>();
                    report.AppendLine($"  + Added AdventurerCharacter component");
                }

                // Sync material
                string materialName = characterName.Replace("_Hooded", "") + "_Material.mat";
                string materialFullPath = MATERIAL_PATH + materialName;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFullPath);

                if (material != null)
                {
                    character.SetMaterial(material);
                }

                // Get or add Animator component
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                    report.AppendLine($"  + Added Animator component to {characterName}");
                }

                // Get or create Animator Controller
                RuntimeAnimatorController animatorController = GetOrCreateAnimatorController(characterName.Replace("_Hooded", ""));
                if (animatorController != null)
                {
                    animator.runtimeAnimatorController = animatorController;
                    if (!prefabExisted)
                    {
                        report.AppendLine($"  + Created Animator Controller for {characterName}");
                    }
                }

                // Set Avatar from the FBX model
                string fbxPath = FBX_PATH + characterName + ".fbx";
                GameObject fbxModel = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbxModel != null)
                {
                    Animator fbxAnimator = fbxModel.GetComponentInChildren<Animator>();
                    if (fbxAnimator != null && fbxAnimator.avatar != null)
                    {
                        animator.avatar = fbxAnimator.avatar;
                        report.AppendLine($"  + Assigned Avatar from FBX model");
                    }
                    else
                    {
                        report.AppendLine($"  ⚠ No Avatar found in FBX - may need to configure FBX import settings");
                    }
                }

                // Use SerializedObject to set the rig references (handles private/protected fields)
                SerializedObject serializedCharacter = new SerializedObject(character);

                // Set character class
                SerializedProperty classProperty = serializedCharacter.FindProperty("characterClass");
                if (classProperty != null)
                {
                    classProperty.stringValue = characterName.Replace("_Hooded", "");
                }

                // Set animator reference
                SerializedProperty animatorProperty = serializedCharacter.FindProperty("animator");
                if (animatorProperty != null && animator != null)
                {
                    animatorProperty.objectReferenceValue = animator;
                }

                // Set all rig references
                SerializedProperty generalRigProperty = serializedCharacter.FindProperty("generalRigReference");
                if (generalRigProperty != null)
                {
                    generalRigProperty.objectReferenceValue = generalRig;
                }

                SerializedProperty movementBasicRigProperty = serializedCharacter.FindProperty("movementBasicRigReference");
                if (movementBasicRigProperty != null)
                {
                    movementBasicRigProperty.objectReferenceValue = movementBasicRig;
                }

                SerializedProperty movementAdvancedRigProperty = serializedCharacter.FindProperty("movementAdvancedRigReference");
                if (movementAdvancedRigProperty != null && movementAdvancedRig != null)
                {
                    movementAdvancedRigProperty.objectReferenceValue = movementAdvancedRig;
                }

                SerializedProperty combatMeleeRigProperty = serializedCharacter.FindProperty("combatMeleeRigReference");
                if (combatMeleeRigProperty != null && combatMeleeRig != null)
                {
                    combatMeleeRigProperty.objectReferenceValue = combatMeleeRig;
                }

                SerializedProperty combatRangedRigProperty = serializedCharacter.FindProperty("combatRangedRigReference");
                if (combatRangedRigProperty != null && combatRangedRig != null)
                {
                    combatRangedRigProperty.objectReferenceValue = combatRangedRig;
                }

                SerializedProperty simulationRigProperty = serializedCharacter.FindProperty("simulationRigReference");
                if (simulationRigProperty != null && simulationRig != null)
                {
                    simulationRigProperty.objectReferenceValue = simulationRig;
                }

                SerializedProperty specialRigProperty = serializedCharacter.FindProperty("specialRigReference");
                if (specialRigProperty != null && specialRig != null)
                {
                    specialRigProperty.objectReferenceValue = specialRig;
                }

                // Set mesh renderer reference
                SkinnedMeshRenderer meshRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                SerializedProperty meshRendererProperty = serializedCharacter.FindProperty("meshRenderer");
                if (meshRendererProperty != null && meshRenderer != null)
                {
                    meshRendererProperty.objectReferenceValue = meshRenderer;
                }

                // Apply changes
                serializedCharacter.ApplyModifiedProperties();

                // Save the modified prefab
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);

                report.AppendLine($"✓ {characterName}: Synced successfully");
                return true;
            }
            finally
            {
                // Always unload the prefab contents
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static bool VerifyCharacterPrefab(string characterName, StringBuilder report)
        {
            string prefabPath = PREFAB_PATH + characterName + "_Prefab.prefab";

            report.AppendLine($"\n{characterName}:");

            // Check prefab exists
            if (!File.Exists(prefabPath))
            {
                report.AppendLine($"  ✗ Prefab file not found");
                return false;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.AppendLine($"  ✗ Failed to load prefab");
                return false;
            }

            // Load prefab contents for inspection
            GameObject instance = PrefabUtility.LoadPrefabContents(prefabPath);
            if (instance == null)
            {
                report.AppendLine($"  ✗ Failed to load prefab contents");
                return false;
            }

            bool isValid = true;

            try
            {
                // Check AdventurerCharacter component
                AdventurerCharacter character = instance.GetComponent<AdventurerCharacter>();
                if (character == null)
                {
                    report.AppendLine($"  ✗ Missing AdventurerCharacter component");
                    isValid = false;
                }
                else
                {
                    report.AppendLine($"  ✓ AdventurerCharacter component present");

                    // Verify using SerializedObject
                    SerializedObject so = new SerializedObject(character);

                    // Check character material
                    SerializedProperty matProp = so.FindProperty("characterMaterial");
                    if (matProp == null || matProp.objectReferenceValue == null)
                    {
                        report.AppendLine($"  ⚠ Material not assigned");
                    }
                    else
                    {
                        report.AppendLine($"  ✓ Material: {matProp.objectReferenceValue.name}");
                    }

                    // Check animator
                    SerializedProperty animProp = so.FindProperty("animator");
                    if (animProp == null || animProp.objectReferenceValue == null)
                    {
                        report.AppendLine($"  ⚠ Animator not assigned");
                    }
                    else
                    {
                        Animator anim = animProp.objectReferenceValue as Animator;
                        if (anim != null)
                        {
                            if (anim.runtimeAnimatorController != null)
                            {
                                report.AppendLine($"  ✓ Animator with controller: {anim.runtimeAnimatorController.name}");
                            }
                            else
                            {
                                report.AppendLine($"  ✓ Animator present (⚠ no controller)");
                            }

                            // Check Avatar
                            if (anim.avatar != null)
                            {
                                report.AppendLine($"  ✓ Avatar: {anim.avatar.name}");
                            }
                            else
                            {
                                report.AppendLine($"  ⚠ Avatar not assigned");
                            }
                        }
                    }

                    // Check all rig references
                    SerializedProperty generalRigProp = so.FindProperty("generalRigReference");
                    if (generalRigProp == null || generalRigProp.objectReferenceValue == null)
                    {
                        report.AppendLine($"  ✗ General rig not assigned");
                        isValid = false;
                    }
                    else
                    {
                        report.AppendLine($"  ✓ General rig: {generalRigProp.objectReferenceValue.name}");
                    }

                    SerializedProperty movementBasicRigProp = so.FindProperty("movementBasicRigReference");
                    if (movementBasicRigProp == null || movementBasicRigProp.objectReferenceValue == null)
                    {
                        report.AppendLine($"  ✗ Movement Basic rig not assigned");
                        isValid = false;
                    }
                    else
                    {
                        report.AppendLine($"  ✓ Movement Basic rig: {movementBasicRigProp.objectReferenceValue.name}");
                    }

                    // Check optional rigs
                    SerializedProperty movementAdvancedRigProp = so.FindProperty("movementAdvancedRigReference");
                    if (movementAdvancedRigProp != null && movementAdvancedRigProp.objectReferenceValue != null)
                    {
                        report.AppendLine($"  ✓ Movement Advanced rig: {movementAdvancedRigProp.objectReferenceValue.name}");
                    }

                    SerializedProperty combatMeleeRigProp = so.FindProperty("combatMeleeRigReference");
                    if (combatMeleeRigProp != null && combatMeleeRigProp.objectReferenceValue != null)
                    {
                        report.AppendLine($"  ✓ Combat Melee rig: {combatMeleeRigProp.objectReferenceValue.name}");
                    }

                    SerializedProperty combatRangedRigProp = so.FindProperty("combatRangedRigReference");
                    if (combatRangedRigProp != null && combatRangedRigProp.objectReferenceValue != null)
                    {
                        report.AppendLine($"  ✓ Combat Ranged rig: {combatRangedRigProp.objectReferenceValue.name}");
                    }

                    SerializedProperty simulationRigProp = so.FindProperty("simulationRigReference");
                    if (simulationRigProp != null && simulationRigProp.objectReferenceValue != null)
                    {
                        report.AppendLine($"  ✓ Simulation rig: {simulationRigProp.objectReferenceValue.name}");
                    }

                    SerializedProperty specialRigProp = so.FindProperty("specialRigReference");
                    if (specialRigProp != null && specialRigProp.objectReferenceValue != null)
                    {
                        report.AppendLine($"  ✓ Special rig: {specialRigProp.objectReferenceValue.name}");
                    }

                    // Check mesh renderer
                    SerializedProperty meshProp = so.FindProperty("meshRenderer");
                    if (meshProp == null || meshProp.objectReferenceValue == null)
                    {
                        report.AppendLine($"  ⚠ Mesh renderer not assigned");
                    }
                    else
                    {
                        report.AppendLine($"  ✓ Mesh renderer present");
                    }
                }

                // Check for Animator component
                Animator animator = instance.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    report.AppendLine($"  ⚠ No Animator component found in hierarchy");
                }

                // Check for mesh renderer
                SkinnedMeshRenderer meshRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (meshRenderer == null)
                {
                    report.AppendLine($"  ⚠ No SkinnedMeshRenderer found in hierarchy");
                }

                return isValid;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static GameObject LoadRig(string rigFileName)
        {
            string rigPath = RIG_PATH + rigFileName;
            GameObject rig = AssetDatabase.LoadAssetAtPath<GameObject>(rigPath);

            if (rig == null)
            {
                Debug.LogError($"Failed to load rig: {rigPath}");
            }

            return rig;
        }

        private static RuntimeAnimatorController GetOrCreateAnimatorController(string characterName)
        {
            // Ensure Animators folder exists
            if (!AssetDatabase.IsValidFolder(ANIMATOR_PATH))
            {
                string parentPath = "Assets/BugWars/Prefabs/Character/Adventurers";
                AssetDatabase.CreateFolder(parentPath, "Animators");
            }

            string controllerPath = ANIMATOR_PATH + characterName + "_Controller.controller";
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);

            if (controller == null)
            {
                // Create new Animator Controller
                var animatorController = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

                // Add a default idle state
                var rootStateMachine = animatorController.layers[0].stateMachine;
                var idleState = rootStateMachine.AddState("Idle");
                rootStateMachine.defaultState = idleState;

                // Add common parameters for movement
                animatorController.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
                animatorController.AddParameter("Speed", AnimatorControllerParameterType.Float);

                AssetDatabase.SaveAssets();
                controller = animatorController;
            }

            return controller;
        }

        /// <summary>
        /// Configure FBX import settings to generate humanoid Avatar
        /// </summary>
        [MenuItem("KBVE/Characters/Configure FBX Import Settings")]
        public static void ConfigureFBXImportSettings()
        {
            Debug.Log("=== Configuring FBX Import Settings ===");

            int configuredCount = 0;

            foreach (string characterName in CHARACTER_NAMES)
            {
                string fbxPath = FBX_PATH + characterName + ".fbx";
                ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;

                if (importer != null)
                {
                    bool changed = false;

                    // Configure Avatar generation
                    if (importer.animationType != ModelImporterAnimationType.Human)
                    {
                        importer.animationType = ModelImporterAnimationType.Human;
                        changed = true;
                    }

                    // Generate Avatar
                    if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                        changed = true;
                    }

                    if (changed)
                    {
                        importer.SaveAndReimport();
                        configuredCount++;
                        Debug.Log($"✓ Configured FBX import for: {characterName}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== FBX Configuration Complete: {configuredCount} files updated ===");

            if (configuredCount > 0)
            {
                EditorUtility.DisplayDialog(
                    "FBX Configuration Complete",
                    $"Configured {configuredCount} FBX file(s) for humanoid Avatar generation.\n\n" +
                    "Run 'Sync Adventurers' again to assign the Avatars.",
                    "OK");
            }
        }

        /// <summary>
        /// Apply the Pixel Art shader to all character materials
        /// </summary>
        [MenuItem("KBVE/Characters/Apply Pixel Shader to Materials")]
        public static void ApplyPixelShaderToMaterials()
        {
            Debug.Log("=== Applying Pixel Shader to Character Materials ===");

            // Load the pixel shader
            string shaderPath = "Assets/BugWars/Prefabs/Character/Adventurers/Shaders/PixelArtCharacter.shader";
            Shader pixelShader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);

            if (pixelShader == null)
            {
                Debug.LogError($"Pixel shader not found at: {shaderPath}");
                EditorUtility.DisplayDialog(
                    "Shader Not Found",
                    $"Could not find PixelArtCharacter shader at:\n{shaderPath}\n\nMake sure the shader file exists.",
                    "OK");
                return;
            }

            Debug.Log($"✓ Loaded shader: {pixelShader.name}");

            int updatedCount = 0;

            // Update each character material
            foreach (string characterName in CHARACTER_NAMES)
            {
                string materialName = characterName.Replace("_Hooded", "") + "_Material.mat";
                string materialFullPath = MATERIAL_PATH + materialName;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialFullPath);

                if (material != null)
                {
                    // Store the main texture before changing shader
                    Texture mainTex = material.mainTexture;
                    Color color = material.HasProperty("_Color") ? material.color : Color.white;

                    // Change shader
                    material.shader = pixelShader;

                    // Restore and set properties
                    material.mainTexture = mainTex;
                    if (material.HasProperty("_Color"))
                    {
                        material.SetColor("_Color", color);
                    }

                    // Set default pixel art parameters (more aggressive for noticeable effect)
                    if (material.HasProperty("_PixelSize"))
                        material.SetFloat("_PixelSize", 0.05f);  // Increased from 0.02
                    if (material.HasProperty("_TexturePixelation"))
                        material.SetFloat("_TexturePixelation", 16f);  // Increased from 8
                    if (material.HasProperty("_OutlineWidth"))
                        material.SetFloat("_OutlineWidth", 0.015f);  // Slightly thicker
                    if (material.HasProperty("_OutlineColor"))
                        material.SetColor("_OutlineColor", Color.black);
                    if (material.HasProperty("_VertexQuantization"))
                        material.SetFloat("_VertexQuantization", 0.7f);  // Increased from 0.5
                    if (material.HasProperty("_QuantizationSize"))
                        material.SetFloat("_QuantizationSize", 0.15f);  // Increased from 0.1
                    if (material.HasProperty("_AmbientStrength"))
                        material.SetFloat("_AmbientStrength", 0.3f);
                    if (material.HasProperty("_DiffuseStrength"))
                        material.SetFloat("_DiffuseStrength", 0.7f);

                    EditorUtility.SetDirty(material);
                    updatedCount++;
                    Debug.Log($"✓ Updated material: {materialName}");
                }
                else
                {
                    Debug.LogWarning($"Material not found: {materialFullPath}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"=== Pixel Shader Applied: {updatedCount} materials updated ===");

            EditorUtility.DisplayDialog(
                "Pixel Shader Applied",
                $"Successfully applied PixelArtCharacter shader to {updatedCount} material(s)!\n\n" +
                "Default settings:\n" +
                "• Pixel Size: 0.02\n" +
                "• Texture Pixelation: 8x\n" +
                "• Outline Width: 0.01\n" +
                "• Vertex Quantization: 0.5\n\n" +
                "You can adjust these in the material inspector.",
                "OK");
        }

        #endregion
    }
}