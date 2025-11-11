using UnityEngine;

namespace BugWars.Entity.NPC
{
    /// <summary>
    /// NPC entity class - represents non-player characters
    /// Now supports billboard 2D sprites in 3D environment
    /// </summary>
    public class NPC : Entity
    {
        [Header("NPC Properties")]
        [SerializeField] private NPCType npcType;
        [SerializeField] private float detectionRange = 10f;

        private Transform target;
        private NPCState currentState = NPCState.Idle;

        protected override void Awake()
        {
            base.Awake();
            entityName = "NPC";
        }

        protected override void Start()
        {
            base.Start();
            // Register with EntityManager
            EntityManager.Instance.RegisterEntity(this);
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
            // TODO: Implement patrol points and movement
        }

        private void ChaseBehavior()
        {
            // Chase behavior implementation
            if (target != null)
            {
                Vector3 direction = (target.position - transform.position).normalized;
                Move(direction);
            }
        }

        private void AttackBehavior()
        {
            // Attack behavior implementation
        }

        protected override void OnDeath()
        {
            base.OnDeath();
            // NPC-specific death behavior
            Debug.Log($"[NPC] {entityName} has died!");
            // TODO: Drop loot, play death animation, etc.
        }

        private void OnDestroy()
        {
            // Unregister from EntityManager when destroyed
            if (EntityManager.Instance != null)
            {
                EntityManager.Instance.UnregisterEntity(this);
            }
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
