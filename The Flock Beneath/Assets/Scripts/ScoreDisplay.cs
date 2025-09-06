using UnityEngine;
using TMPro;

public class ScoreDisplay : MonoBehaviour
{
    [Header("Star Prefabs")]
    [SerializeField] private GameObject wholeStarPrefab;
    [SerializeField] private GameObject halfStarPrefab;
    
    [Header("Star Outline Positions")]
    [SerializeField] private Transform[] starOutlinePositions;
    
    [Header("Level Display")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI lostSheepText;
    
    private GameObject[] instantiatedStars = new GameObject[3];

    void Start()
    {
        Cursor.visible = true;
        
        // Starts at 1 for first time
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        
        float stars = GetLevelScore(currentLevel);
        
        Debug.Log($"ScoreDisplay: Current level = {currentLevel}, Stars = {stars}");
        
        Debug.Log($"Whole Star Prefab: {(wholeStarPrefab != null ? wholeStarPrefab.name : "NULL")}");
        Debug.Log($"Half Star Prefab: {(halfStarPrefab != null ? halfStarPrefab.name : "NULL")}");
        Debug.Log($"Star Outline Positions Count: {(starOutlinePositions != null ? starOutlinePositions.Length : 0)}");
        
        DisplayStars(stars);
        DisplayLevelText(currentLevel);
        DisplayLostSheepText(currentLevel);
        
        // Increment for next time
        IncrementCurrentLevel();
    }

    void DisplayLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Day {level}";
            Debug.Log($"Level text updated to: Day {level}");
        }
        else
        {
            Debug.LogWarning("Level text component is not assigned!");
        }
    }
    
    void DisplayLostSheepText(int level)
    {
        if (lostSheepText != null)
        {
            int lostSheepCount = GetLevelLostSheep(level);
            lostSheepText.text = $"{lostSheepCount} lost sheep.";
            Debug.Log($"Lost sheep text updated to: {lostSheepCount} lost sheep.");
        }
        else
        {
            Debug.LogWarning("Lost sheep text component is not assigned!");
        }
    }
    
    void IncrementCurrentLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        PlayerPrefs.SetInt("CurrentLevel", currentLevel + 1);
        PlayerPrefs.Save();
        Debug.Log($"Current level incremented to: {currentLevel + 1}");
    }

    void DisplayStars(float starScore)
    {
        Debug.Log($"DisplayStars called with score: {starScore}");
        
        // Clear existing stars
        ClearStars();
        
        // Clamp star score between 0 and 3
        starScore = Mathf.Clamp(starScore, 0f, 3f);
        
        int wholeStars = Mathf.FloorToInt(starScore);
        bool hasHalfStar = (starScore - wholeStars) >= 0.5f;
        
        Debug.Log($"Whole stars to display: {wholeStars}, Has half star: {hasHalfStar}");
        
        // Display whole stars
        for (int i = 0; i < wholeStars && i < 3; i++)
        {
            if (starOutlinePositions[i] != null && wholeStarPrefab != null)
            {
                Debug.Log($"Creating whole star {i} at position {starOutlinePositions[i].name}");
                instantiatedStars[i] = Instantiate(wholeStarPrefab, starOutlinePositions[i]);
                MatchUIImageTransform(instantiatedStars[i], starOutlinePositions[i]);
                Debug.Log($"Whole star {i} created successfully: {instantiatedStars[i].name}");
            }
            else
            {
                Debug.LogError($"Missing reference for star {i}: outline={starOutlinePositions[i]}, prefab={wholeStarPrefab}");
            }
        }
        
        // Display half stars
        if (hasHalfStar && wholeStars < 3)
        {
            if (starOutlinePositions[wholeStars] != null && halfStarPrefab != null)
            {
                Debug.Log($"Creating half star at position {wholeStars}");
                instantiatedStars[wholeStars] = Instantiate(halfStarPrefab, starOutlinePositions[wholeStars]);
                MatchUIImageTransform(instantiatedStars[wholeStars], starOutlinePositions[wholeStars]);
                Debug.Log($"Half star created successfully: {instantiatedStars[wholeStars].name}");
            }
            else
            {
                Debug.LogError($"Missing reference for half star: outline={starOutlinePositions[wholeStars]}, prefab={halfStarPrefab}");
            }
        }
    }
    
    void MatchUIImageTransform(GameObject starOverlay, Transform outlineTransform)
    {
        Debug.Log($"Matching transform for {starOverlay.name} to {outlineTransform.name}");
        
        // Get the RectTransform components
        RectTransform overlayRect = starOverlay.GetComponent<RectTransform>();
        RectTransform outlineRect = outlineTransform.GetComponent<RectTransform>();
        
        if (overlayRect != null && outlineRect != null)
        {
            // Match anchors and pivot
            overlayRect.anchorMin = outlineRect.anchorMin;
            overlayRect.anchorMax = outlineRect.anchorMax;
            overlayRect.pivot = outlineRect.pivot;
            
            // Match size
            overlayRect.sizeDelta = outlineRect.sizeDelta;
            
            // Match position
            overlayRect.anchoredPosition = Vector2.zero;
            
            // Match rotation and scale
            overlayRect.localRotation = Quaternion.identity;
            overlayRect.localScale = Vector3.one;
            
            Debug.Log($"Transform matched - Size: {overlayRect.sizeDelta}, Position: {overlayRect.anchoredPosition}");
            
            // Ensure star overlay is active and visible
            starOverlay.SetActive(true);
            
            UnityEngine.UI.Image overlayImage = starOverlay.GetComponent<UnityEngine.UI.Image>();
            if (overlayImage != null)
            {
                overlayImage.enabled = true;
                Debug.Log($"Image component enabled on {starOverlay.name}");
            }
            else
            {
                Debug.LogWarning($"No Image component found on {starOverlay.name}");
            }
        }
        else
        {
            Debug.LogError($"RectTransform missing - Overlay: {overlayRect}, Outline: {outlineRect}");
        }
    }
    
    void ClearStars()
    {
        for (int i = 0; i < instantiatedStars.Length; i++)
        {
            if (instantiatedStars[i] != null)
            {
                DestroyImmediate(instantiatedStars[i]);
                instantiatedStars[i] = null;
            }
        }
    }

    float GetLevelScore(int level)
    {
        return PlayerPrefs.GetFloat($"Level{level}Stars", 0f);
    }
    
    int GetLevelLostSheep(int level)
    {
        return PlayerPrefs.GetInt($"Level{level}Lost", 0);
    }
    
    void OnDestroy()
    {
        ClearStars();
    }
}