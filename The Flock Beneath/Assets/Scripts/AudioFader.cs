using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioFader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float fadeInTime = 2.0f;
    [SerializeField] private float fadeOutTime = 0.5f;
    [SerializeField] private float targetVolume = 1.0f;
    [SerializeField] private bool fadeInOnStart = true;
    
    [Header("Persistence Settings")]
    [SerializeField] private bool persistBetweenScenes = true;
    
    private AudioSource backgroundMusic;
    private static AudioFader instance;
    
    void Awake()
    {
        // Singleton
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
        
        backgroundMusic = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (backgroundMusic == null)
        {
            Debug.LogError("No AudioSource component found on AudioFader!");
            return;
        }

        if (fadeInOnStart)
        {
            backgroundMusic.volume = 0f;
            StartCoroutine(FadeInMusic());
        }
        
        if (persistBetweenScenes)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }
    
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"Scene {scene.name} loaded. Music continuing...");
    }

    IEnumerator FadeInMusic()
    {
        if (backgroundMusic == null) yield break;
        
        float currentTime = 0f;
        float startVolume = backgroundMusic.volume;

        while (currentTime < fadeInTime)
        {
            currentTime += Time.deltaTime;
            backgroundMusic.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / fadeInTime);
            yield return null;
        }

        backgroundMusic.volume = targetVolume;
    }
    
    IEnumerator FadeOutMusic()
    {
        if (backgroundMusic == null) yield break;
        
        float currentTime = 0f;
        float startVolume = backgroundMusic.volume;

        while (currentTime < fadeOutTime)
        {
            currentTime += Time.deltaTime;
            backgroundMusic.volume = Mathf.Lerp(startVolume, 0f, currentTime / fadeInTime);
            yield return null;
        }

        backgroundMusic.volume = 0f;
    }
    
    public void FadeIn()
    {
        StopAllCoroutines();
        StartCoroutine(FadeInMusic());
    }
    
    public void FadeOut()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOutMusic());
    }
    
    public void StopMusicImmediately()
    {
        StopAllCoroutines();
        if (backgroundMusic != null)
        {
            backgroundMusic.Stop();
            backgroundMusic.volume = 0f;
        }
    }
    
    public void RestartAndFadeIn()
    {
        StopAllCoroutines();
        if (backgroundMusic != null)
        {
            backgroundMusic.Stop();
            backgroundMusic.volume = 0f;
            backgroundMusic.Play();
            StartCoroutine(FadeInMusic());
        }
    }
    
    public void SetVolume(float volume)
    {
        if (backgroundMusic != null)
        {
            targetVolume = Mathf.Clamp01(volume);
            backgroundMusic.volume = targetVolume;
        }
    }
    
    public void FadeToVolume(float volume)
    {
        targetVolume = Mathf.Clamp01(volume);
        StopAllCoroutines();
        StartCoroutine(FadeToTargetVolume());
    }
    
    IEnumerator FadeToTargetVolume()
    {
        if (backgroundMusic == null) yield break;
        
        float currentTime = 0f;
        float startVolume = backgroundMusic.volume;

        while (currentTime < fadeInTime)
        {
            currentTime += Time.deltaTime;
            backgroundMusic.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / fadeInTime);
            yield return null;
        }

        backgroundMusic.volume = targetVolume;
    }
    
    // Stop music and destroy
    public void StopAndDestroy()
    {
        StartCoroutine(FadeOutAndDestroy());
    }
    
    IEnumerator FadeOutAndDestroy()
    {
        yield return StartCoroutine(FadeOutMusic());
        Destroy(gameObject);
    }
}