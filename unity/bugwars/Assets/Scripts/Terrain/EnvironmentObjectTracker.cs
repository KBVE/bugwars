using UnityEngine;

namespace BugWars.Terrain
{
    /// <summary>
    /// Tracks environment object lifecycle and notifies spawn data when destroyed
    /// Handles both client-side procedural objects (Phase 2) and server-authoritative objects (Phase 3)
    ///
    /// Phase 2 (Client-side): Marks as permanently harvested on destroy
    /// Phase 3 (Server-managed): Only clears activeInstance, allows server-controlled respawning
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
            if (_spawnData == null)
                return;

            // Clear active instance reference
            _spawnData.activeInstance = null;

            // Phase 2 (Client-side): Mark as permanently harvested
            // Phase 3 (Server-managed): Don't mark as harvested - let server control respawning
            if (!_spawnData.isServerManaged)
            {
                _spawnData.isHarvested = true;
            }
        }

        /// <summary>
        /// Get the server object ID if this is a server-managed object
        /// </summary>
        public string GetServerObjectId()
        {
            return _spawnData?.serverObjectId;
        }

        /// <summary>
        /// Check if this is a server-managed object
        /// </summary>
        public bool IsServerManaged()
        {
            return _spawnData?.isServerManaged ?? false;
        }
    }
}
