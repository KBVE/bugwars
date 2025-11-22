using UnityEngine;
using VContainer;

namespace BugWars.Auth
{
    /// <summary>
    /// Manages Supabase configuration including the anonymous key.
    /// This component should be attached to the GameLifetimeScope as a serialized field
    /// and will be injected anywhere it's needed via VContainer.
    ///
    /// Usage:
    /// 1. Add this component to GameLifetimeScope in the Inspector
    /// 2. Set the Supabase Anon Key in the Inspector (or it will use the default fallback)
    /// 3. Inject this manager wherever you need the anon key:
    ///    [Inject] private SupabaseManager _supabaseManager;
    /// 4. Access the key: _supabaseManager.AnonKey
    /// </summary>
    public class SupabaseManager : MonoBehaviour
    {
        [Header("Supabase Configuration")]
        [SerializeField]
        [Tooltip("Supabase Anonymous Key - This is safe to expose client-side")]
        private string supabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYW5vbiIsImlzcyI6InN1cGFiYXNlIiwiaWF0IjoxNzU1NDAzMjAwLCJleHAiOjE5MTMxNjk2MDB9.oietJI22ZytbghFywvdYMSJp7rcsBdBYbcciJxeGWrg";
        // Anonymous key is safe to expose on the client-side
        [SerializeField]
        [Tooltip("Supabase Project URL (e.g., https://your-project.supabase.co)")]
        private string supabaseUrl = "https://supabase.kbve.com";

        /// <summary>
        /// Get the Supabase anonymous key.
        /// This is the public anon key that's safe to expose on the client-side.
        /// </summary>
        public string AnonKey
        {
            get
            {
                if (string.IsNullOrEmpty(supabaseAnonKey))
                {
                    Debug.LogWarning("[SupabaseManager] Supabase anon key is not set! Please set it in the Inspector.");
                    return "";
                }
                return supabaseAnonKey;
            }
        }

        /// <summary>
        /// Get the Supabase project URL.
        /// </summary>
        public string ProjectUrl
        {
            get
            {
                if (string.IsNullOrEmpty(supabaseUrl))
                {
                    Debug.LogWarning("[SupabaseManager] Supabase URL is not set! Please set it in the Inspector.");
                    return "";
                }
                return supabaseUrl;
            }
        }

        /// <summary>
        /// Check if the Supabase configuration is valid.
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(supabaseAnonKey) && !string.IsNullOrEmpty(supabaseUrl);

        private void Awake()
        {
            if (string.IsNullOrEmpty(supabaseAnonKey))
            {
                Debug.LogWarning("[SupabaseManager] Supabase anon key is not configured. " +
                    "Please set it in the GameLifetimeScope Inspector.");
            }

            if (string.IsNullOrEmpty(supabaseUrl))
            {
                Debug.LogWarning("[SupabaseManager] Supabase URL is not configured. " +
                    "Please set it in the GameLifetimeScope Inspector.");
            }

            if (IsConfigured)
            {
                Debug.Log($"[SupabaseManager] Configured with URL: {supabaseUrl}");
                Debug.Log($"[SupabaseManager] Anon key present: {!string.IsNullOrEmpty(supabaseAnonKey)}");
            }
        }

        /// <summary>
        /// Get the Authorization header value for Supabase API requests.
        /// Format: "Bearer {anonKey}"
        /// </summary>
        public string GetAuthorizationHeader()
        {
            return $"Bearer {AnonKey}";
        }

        /// <summary>
        /// Get the apikey header value for Supabase API requests.
        /// Some Supabase endpoints require this header instead of Authorization.
        /// </summary>
        public string GetApiKeyHeader()
        {
            return AnonKey;
        }

        /// <summary>
        /// Validate that both the anon key and URL are configured.
        /// Logs errors if not configured.
        /// </summary>
        /// <returns>True if configured, false otherwise</returns>
        public bool ValidateConfiguration()
        {
            bool isValid = true;

            if (string.IsNullOrEmpty(supabaseAnonKey))
            {
                Debug.LogError("[SupabaseManager] Supabase anon key is required but not set!");
                isValid = false;
            }

            if (string.IsNullOrEmpty(supabaseUrl))
            {
                Debug.LogError("[SupabaseManager] Supabase URL is required but not set!");
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Set the Supabase configuration at runtime (optional).
        /// Useful for testing or dynamic configuration.
        /// </summary>
        /// <param name="anonKey">Supabase anonymous key</param>
        /// <param name="projectUrl">Supabase project URL</param>
        public void SetConfiguration(string anonKey, string projectUrl)
        {
            supabaseAnonKey = anonKey;
            supabaseUrl = projectUrl;
            Debug.Log($"[SupabaseManager] Configuration updated: {projectUrl}");
        }
    }
}
