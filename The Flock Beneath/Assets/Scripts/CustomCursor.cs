using UnityEngine;
using UnityEngine.SceneManagement;

public class GlobalCursor : MonoBehaviour
{
    [SerializeField] private Texture2D cursorTexture;
    [SerializeField] private Vector2 hotspot = Vector2.zero;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Cursor.SetCursor(cursorTexture, hotspot, CursorMode.Auto);
    }
}