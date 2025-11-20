using UnityEngine;

namespace BugWars.Terrain
{
    /// <summary>
    /// Tracks environment object lifecycle and notifies spawn data when destroyed
    /// Prevents harvested objects from respawning
    /// </summary>
    public class EnvironmentObjectTracker : MonoBehaviour
    {
        private EnvironmentSpawnData _spawnData;

        public void Initialize(EnvironmentSpawnData spawnData)
        {
            _spawnData = spawnData;
        }

        private void OnDestroy()
        {
            // Mark spawn data as harvested so it never respawns
            if (_spawnData != null)
            {
                _spawnData.isHarvested = true;
                _spawnData.activeInstance = null;
                Debug.Log($"[EnvironmentObjectTracker] Object destroyed/harvested at {transform.position} - marked as harvested, will not respawn");
            }
        }
    }
}
