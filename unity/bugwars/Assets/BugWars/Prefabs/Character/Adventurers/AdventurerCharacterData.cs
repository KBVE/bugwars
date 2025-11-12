using UnityEngine;

namespace BugWars.Characters
{
    /// <summary>
    /// ScriptableObject to store character configuration data
    /// This allows easy creation of character variants without code changes
    /// </summary>
    [CreateAssetMenu(fileName = "NewAdventurerData", menuName = "BugWars/Character/Adventurer Data")]
    public class AdventurerCharacterData : ScriptableObject
    {
        [Header("Character Identity")]
        [SerializeField] private string characterClass = "Knight";
        [SerializeField] private string displayName = "Knight";
        [TextArea(3, 5)]
        [SerializeField] private string description = "A heavily armored warrior";
        
        [Header("Visual Assets")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Material characterMaterial;
        [SerializeField] private Sprite characterIcon;
        
        [Header("Stats")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private int attackPower = 10;
        [SerializeField] private float attackRange = 1.5f;
        
        [Header("Animation")]
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private GameObject generalRig;
        [SerializeField] private GameObject movementRig;
        
        // Properties
        public string CharacterClass => characterClass;
        public string DisplayName => displayName;
        public string Description => description;
        public GameObject ModelPrefab => modelPrefab;
        public Material CharacterMaterial => characterMaterial;
        public Sprite CharacterIcon => characterIcon;
        public float MoveSpeed => moveSpeed;
        public float RotationSpeed => rotationSpeed;
        public int MaxHealth => maxHealth;
        public int AttackPower => attackPower;
        public float AttackRange => attackRange;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public GameObject GeneralRig => generalRig;
        public GameObject MovementRig => movementRig;
        
        /// <summary>
        /// Apply this character data to an AdventurerCharacter component
        /// </summary>
        public void ApplyToCharacter(AdventurerCharacter character)
        {
            if (character == null) return;
            
            if (characterMaterial != null)
            {
                character.SetMaterial(characterMaterial);
            }
            
            if (animatorController != null)
            {
                character.SetAnimator(animatorController);
            }
        }
        
        /// <summary>
        /// Create a character instance from this data
        /// </summary>
        public GameObject CreateInstance(Vector3 position, Quaternion rotation)
        {
            if (modelPrefab == null)
            {
                Debug.LogError($"Cannot create instance of {characterClass} - no model prefab assigned!");
                return null;
            }
            
            GameObject instance = Instantiate(modelPrefab, position, rotation);
            instance.name = $"{characterClass}_Instance";
            
            AdventurerCharacter character = instance.GetComponent<AdventurerCharacter>();
            if (character != null)
            {
                ApplyToCharacter(character);
            }
            else
            {
                character = instance.AddComponent<AdventurerCharacter>();
                ApplyToCharacter(character);
            }
            
            return instance;
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Validate the data in the editor
        /// </summary>
        private void OnValidate()
        {
            // Ensure positive values
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            rotationSpeed = Mathf.Max(0.1f, rotationSpeed);
            maxHealth = Mathf.Max(1, maxHealth);
            attackPower = Mathf.Max(0, attackPower);
            attackRange = Mathf.Max(0.1f, attackRange);
        }
#endif
    }
}