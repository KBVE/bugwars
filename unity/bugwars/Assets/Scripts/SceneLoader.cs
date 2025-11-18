using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple script to automatically load the next scene after a delay.
/// Used in Credits scene to warmup shaders then load the main game scene.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Header("Scene Loading")]
    [Tooltip("Name of the scene to load")]
    public string sceneToLoad = "SampleScene";

    [Tooltip("Delay before loading the next scene (in seconds)")]
    public float loadDelay = 0.5f;

    [Tooltip("If true, will automatically load the scene on Start")]
    public bool autoLoad = true;

    private void Start()
    {
        if (autoLoad)
        {
            Debug.Log($"[SceneLoader] Will load '{sceneToLoad}' in {loadDelay} seconds");
            Invoke(nameof(LoadScene), loadDelay);
        }
    }

    /// <summary>
    /// Load the specified scene
    /// </summary>
    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.Log($"[SceneLoader] Loading scene: {sceneToLoad}");
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("[SceneLoader] No scene name specified!");
        }
    }

    /// <summary>
    /// Load a specific scene by name
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        sceneToLoad = sceneName;
        LoadScene();
    }

    /// <summary>
    /// Load the next scene in the build settings
    /// </summary>
    public void LoadNextScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        int nextSceneIndex = (currentSceneIndex + 1) % SceneManager.sceneCountInBuildSettings;
        Debug.Log($"[SceneLoader] Loading next scene (index {nextSceneIndex})");
        SceneManager.LoadScene(nextSceneIndex);
    }
}
