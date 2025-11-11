using UnityEngine;

namespace BugWars.Entity.NPC
{
    /// <summary>
    /// NPC entity class - represents non-player characters
    /// </summary>
    public class NPC : Entity
    {
        [Header("NPC Properties")]
        [SerializeField] private NPCType npcType;
        [SerializeField] private float detectionRange = 10f;
        [SerializeField] private float patrolSpeed = 2f;

        private Transform target;
        private NPCState currentState = NPCState.Idle;

        protected override void Awake()
        {
            base.Awake();
            entityName = "NPC";
        }

        private void Update()
        {
            UpdateBehavior();
        }

        private void UpdateBehavior()
        {
            switch (currentState)
            {
                case NPCState.Idle:
                    IdleBehavior();
                    break;
                case NPCState.Patrol:
                    PatrolBehavior();
                    break;
                case NPCState.Chase:
                    ChaseBehavior();
                    break;
                case NPCState.Attack:
                    AttackBehavior();
                    break;
            }
        }

        private void IdleBehavior()
        {
            // Idle behavior implementation
        }

        private void PatrolBehavior()
        {
            // Patrol behavior implementation
        }

        private void ChaseBehavior()
        {
            // Chase behavior implementation
        }

        private void AttackBehavior()
        {
            // Attack behavior implementation
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            // NPC-specific death behavior
            Debug.Log($"{entityName} has been defeated!");
            // TODO: Drop loot, play death animation, etc.
        }

        public void SetState(NPCState newState)
        {
            currentState = newState;
        }

        public NPCState GetState() => currentState;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }

    public enum NPCType
    {
        Friendly,
        Neutral,
        Hostile
    }

    public enum NPCState
    {
        Idle,
        Patrol,
        Chase,
        Attack
    }
}
