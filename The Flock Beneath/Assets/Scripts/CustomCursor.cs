using UnityEngine;
using UnityEngine.SceneManagement;

public class CustomCursor : MonoBehaviour
{
    [SerializeField] private Texture2D cursorTexture;
    
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetCustomCursor();
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetCustomCursor();
    }
    
    private void SetCustomCursor()
    {
        if (cursorTexture != null)
        {
            Cursor.SetCursor(cursorTexture, Vector2.zero, CursorMode.Auto);
        }
    }
    
    private void Start()
    {
        SetCustomCursor();
    }
}