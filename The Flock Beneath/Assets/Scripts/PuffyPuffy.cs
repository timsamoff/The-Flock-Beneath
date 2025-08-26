using UnityEngine;
using System.Collections;

public class PuffyPuffy : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float corralTimer = 5f;
    
    [Header("Transformation Settings")]
    [SerializeField] private float targetScale = 2.0f;
    [SerializeField] private float transformationDuration = 1.5f;
        
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    private bool isInCorral = false;
    private bool isTransforming = false;
    private float currentTimer = 0f;
    private Coroutine timerCoroutine;
    private Coroutine transformationCoroutine;
    
    private SheepBehavior sheepBehavior;
    private SpriteRenderer spriteRenderer;
    private Vector3 originalScale;
    private Color originalColor;
    
    private CanvasGroup canvasGroup;

    private SpriteRenderer[] childSprites;

    public System.Action<PuffyPuffy> OnTransformationStarted;
    public System.Action<PuffyPuffy> OnTransformationCompleted;

    void Start()
    {
        sheepBehavior = GetComponent<SheepBehavior>();
        
        childSprites = GetComponentsInChildren<SpriteRenderer>();

        foreach (var sr in childSprites)
        {
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }

        originalScale = transform.localScale;

        if (showDebugLogs)
        {
            Debug.Log($"PuffyPuffy initialized on {gameObject.name}. Child sprites: {childSprites.Length}");
        }
    }
    
    void Update()
    {
        // Check corral status
        if (sheepBehavior != null)
        {
            bool newCorralStatus = sheepBehavior.isCorralled;

            if (newCorralStatus != isInCorral)
            {
                if (showDebugLogs)
                {
                    Debug.Log($"Sheep {gameObject.name} corral status changed: {isInCorral} -> {newCorralStatus}");
                }

                isInCorral = newCorralStatus;

                if (newCorralStatus)
                {
                    StartCorralTimer();
                }
                else
                {
                    StopCorralTimer();
                }
            }
        }
    }
    
    private void StartCorralTimer()
    {
        if (isTransforming) return;
        
        if (showDebugLogs)
        {
            Debug.Log($"Sheep {gameObject.name} entered corral. Starting {corralTimer}s timer.");
        }
        
        // Stop any existing timer
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        
        // Start new timer
        timerCoroutine = StartCoroutine(CorralTimerCoroutine());
    }
    
    private void StopCorralTimer()
    {
        if (showDebugLogs && currentTimer > 0f)
        {
            Debug.Log($"Sheep {gameObject.name} left corral. Stopping and resetting timer. (Was at {currentTimer:F1}s/{corralTimer}s)");
        }
        
        // Stop timer coroutine
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
        
        // Reset timer
        currentTimer = 0f;
    }
    
    private IEnumerator CorralTimerCoroutine()
    {
        currentTimer = 0f;
        
        while (currentTimer < corralTimer && isInCorral && !isTransforming)
        {
            currentTimer += Time.deltaTime;

            if (showDebugLogs)
                Debug.Log($"[PuffyPuffy] {gameObject.name} timer = {currentTimer:F2}/{corralTimer}");

            yield return null;
        }
        
        if (currentTimer >= corralTimer && isInCorral && !isTransforming)
        {
            StartTransformation();
        }
        
        timerCoroutine = null;
    }
    
    private void StartTransformation()
    {
        if (isTransforming) return;
        
        isTransforming = true;
        
        if (showDebugLogs)
        {
            Debug.Log($"Sheep {gameObject.name} starting PUFF transformation!");
        }
        
        OnTransformationStarted?.Invoke(this);
        
        transformationCoroutine = StartCoroutine(PuffTransformationCoroutine());
    }
    
    private IEnumerator PuffTransformationCoroutine()
    {
        float elapsedTime = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = originalScale * targetScale;

        float startRotation = transform.eulerAngles.z;
        float endRotation = startRotation + 180f;

        // Store original colors for all sprites
        Color[] startColors = new Color[childSprites.Length];
        Color[] endColors = new Color[childSprites.Length];
        for (int i = 0; i < childSprites.Length; i++)
        {
            Color c = childSprites[i].color;
            startColors[i] = c;
            endColors[i] = new Color(c.r, c.g, c.b, 0f); // fade to invisible
        }

        while (elapsedTime < transformationDuration)
        {
            float t = elapsedTime / transformationDuration; // linear progress

            // Scale
            transform.localScale = Vector3.Lerp(startScale, endScale, t);

            // Rotation
            float z = Mathf.Lerp(startRotation, endRotation, t);
            transform.rotation = Quaternion.Euler(0f, 0f, z);

            // Fade all sprites
            for (int i = 0; i < childSprites.Length; i++)
            {
                childSprites[i].color = Color.Lerp(startColors[i], endColors[i], t);
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Snap to final values
        transform.localScale = endScale;
        transform.rotation = Quaternion.Euler(0f, 0f, endRotation);

        for (int i = 0; i < childSprites.Length; i++)
            childSprites[i].color = endColors[i];

        if (showDebugLogs)
            Debug.Log($"PUFF transformation completed for {gameObject.name}");

        OnTransformationCompleted?.Invoke(this);

        // if (sheepBehavior != null)
        // {
        //     GameManager gameManager = sheepBehavior.GetGameManager();
        //     if (gameManager != null)
        //     {
        //         // Pass the sheep behavior reference, not just increment count
        //         gameManager.SheepPuffed(sheepBehavior);
        //     }
        // }

        yield return new WaitForSeconds(0.2f);
        Destroy(gameObject);
    }
    
    [ContextMenu("Test Fade")]
    public void TestFade()
    {
        if (spriteRenderer != null)
        {
            Color testColor = spriteRenderer.color;
            testColor.a = 0.5f;
            spriteRenderer.color = testColor;
            Debug.Log($"Test fade applied. Alpha should be 0.5. Current alpha: {spriteRenderer.color.a}");
        }
    }
    
    [ContextMenu("Reset Fade")]
    public void TestResetFade()
    {
        if (spriteRenderer != null)
        {
            Color testColor = originalColor;
            testColor.a = 1f;
            spriteRenderer.color = testColor;
            Debug.Log($"Fade reset. Alpha should be 1.0. Current alpha: {spriteRenderer.color.a}");
        }
    }
    
    public void SetCorralTimer(float newTimer)
    {
        corralTimer = newTimer;
        if (showDebugLogs)
        {
            Debug.Log($"Corral timer set to {corralTimer}s for sheep {gameObject.name}");
        }
    }
    
    public void SetTargetScale(float newScale)
    {
        targetScale = newScale;
    }
    
    public void SetTransformationDuration(float newDuration)
    {
        transformationDuration = newDuration;
    }
    
    public float GetRemainingTime()
    {
        if (!isInCorral || isTransforming) return 0f;
        return Mathf.Max(0f, corralTimer - currentTimer);
    }
    
    public float GetTimerProgress()
    {
        if (corralTimer <= 0f) return 0f;
        return Mathf.Clamp01(currentTimer / corralTimer);
    }
    
    public bool IsTransforming()
    {
        return isTransforming;
    }
    
    public void ResetSheep()
    {
        StopCorralTimer();
        
        if (transformationCoroutine != null)
        {
            StopCoroutine(transformationCoroutine);
            transformationCoroutine = null;
        }
        
        isTransforming = false;
        isInCorral = false;
        currentTimer = 0f;
        
        // Reset visual properties
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }
        
        if (showDebugLogs)
        {
            Debug.Log($"Sheep {gameObject.name} has been reset.");
        }
    }
    
    void OnDestroy()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }
        
        if (transformationCoroutine != null)
        {
            StopCoroutine(transformationCoroutine);
        }
    }
}