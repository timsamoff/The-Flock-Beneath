using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class GrassSpawner : MonoBehaviour
{
    [Header("Grass Prefabs")]
    [SerializeField] private GameObject[] prefabs;
    
    [Header("Spawn Settings")]
    [SerializeField] private Vector2Int spawnCountRange = new Vector2Int(15, 50);
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool guaranteeAllPrefabs = true; // Ensures all prefabs are used at least once
    
    [Header("Screen Boundaries")]
    [SerializeField] private float screenMarginPercent = 0.1f; // 10% margin from screen edges
    
    [Header("Scale Settings")]
    [SerializeField] private bool randomScale = false;
    [SerializeField] private Vector2 scaleRange = new Vector2(0.5f, 1.5f);
    [SerializeField] private Transform parentTransform;
    
    private Camera targetCamera;
    
    void Start()
    {
        // Initialize random seed
        Random.InitState(System.DateTime.Now.Millisecond + GetInstanceID());
        
        targetCamera = Camera.main;
        if (targetCamera == null)
        {
            targetCamera = FindFirstObjectByType<Camera>();
        }
        
        if (targetCamera == null)
        {
            Debug.LogError("No camera found! Please ensure there's a camera in the scene.");
            return;
        }
        
        if (spawnOnStart)
        {
            SpawnPrefabs();
        }
    }
    
    [ContextMenu("Spawn Prefabs")]
    public void SpawnPrefabs()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogWarning("No prefabs assigned to spawn!");
            return;
        }
        
        if (targetCamera == null)
        {
            Debug.LogError("No camera available for spawning!");
            return;
        }
        
        // Get random number of objects to spawn within specified range
        int numberOfObjectsToSpawn = Random.Range(spawnCountRange.x, spawnCountRange.y + 1);
        
        // Create list of prefabs to spawn
        List<GameObject> prefabsToSpawn = new List<GameObject>();
        
        if (guaranteeAllPrefabs && prefabs.Length <= numberOfObjectsToSpawn)
        {
            // Add each prefab at least once
            prefabsToSpawn.AddRange(prefabs);
            
            // Fill remaining slots with random prefabs
            int remainingSlots = numberOfObjectsToSpawn - prefabs.Length;
            for (int i = 0; i < remainingSlots; i++)
            {
                prefabsToSpawn.Add(prefabs[Random.Range(0, prefabs.Length)]);
            }
            
            // Shuffle list to randomize spawn order
            for (int i = 0; i < prefabsToSpawn.Count; i++)
            {
                GameObject temp = prefabsToSpawn[i];
                int randomIndex = Random.Range(i, prefabsToSpawn.Count);
                prefabsToSpawn[i] = prefabsToSpawn[randomIndex];
                prefabsToSpawn[randomIndex] = temp;
            }
        }
        else
        {
            for (int i = 0; i < numberOfObjectsToSpawn; i++)
            {
                prefabsToSpawn.Add(prefabs[Random.Range(0, prefabs.Length)]);
            }
        }
        
        // Spawn all prefabs
        foreach (GameObject prefab in prefabsToSpawn)
        {
            SpawnSpecificPrefab(prefab);
        }
    }
    
    private void SpawnSpecificPrefab(GameObject prefabToSpawn)
    {
        Vector2 randomScreenPoint = GetRandomScreenPoint();
        Vector3 worldPosition = GetWorldPositionFromScreen(randomScreenPoint);
        
        // Spawn prefab
        GameObject spawnedObject = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity);
        
        // Set parent (if specified)
        if (parentTransform != null)
        {
            spawnedObject.transform.SetParent(parentTransform);
        }
        
        // Apply random scale (if enabled)
        if (randomScale)
        {
            float randomScaleFactor = Random.Range(scaleRange.x, scaleRange.y);
            spawnedObject.transform.localScale = Vector3.one * randomScaleFactor;
        }
    }
    
    private void SpawnRandomPrefab()
    {
        // Choose random prefab
        GameObject prefabToSpawn = prefabs[Random.Range(0, prefabs.Length)];
        SpawnSpecificPrefab(prefabToSpawn);
    }
    
    private Vector2 GetRandomScreenPoint()
    {
        float margin = screenMarginPercent;
        
        float randomX = Random.Range(Screen.width * margin, Screen.width * (1f - margin));
        float randomY = Random.Range(Screen.height * margin, Screen.height * (1f - margin));
        
        return new Vector2(randomX, randomY);
    }
    
    private Vector3 GetWorldPositionFromScreen(Vector2 screenPoint)
    {
        Vector3 screenPointWithZ = new Vector3(screenPoint.x, screenPoint.y, targetCamera.nearClipPlane);
        return targetCamera.ScreenToWorldPoint(screenPointWithZ);
    }
    
    [ContextMenu("Clear Spawned Objects")]
    public void ClearSpawnedObjects()
    {
        if (parentTransform != null)
        {
            // Clear children of parent transform
            for (int i = parentTransform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(parentTransform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(parentTransform.GetChild(i).gameObject);
                }
            }
        }
        else
        {
            Debug.LogWarning("No parent transform assigned. Cannot clear spawned objects automatically.");
        }
    }
    
    public GameObject SpawnSinglePrefab()
    {
        if (prefabs == null || prefabs.Length == 0) return null;
        
        GameObject prefabToSpawn = prefabs[Random.Range(0, prefabs.Length)];
        Vector2 randomScreenPoint = GetRandomScreenPoint();
        Vector3 worldPosition = GetWorldPositionFromScreen(randomScreenPoint);
        
        GameObject spawnedObject = Instantiate(prefabToSpawn, worldPosition, Quaternion.identity);
        
        if (parentTransform != null)
        {
            spawnedObject.transform.SetParent(parentTransform);
        }
        
        if (randomScale)
        {
            float randomScaleFactor = Random.Range(scaleRange.x, scaleRange.y);
            spawnedObject.transform.localScale = Vector3.one * randomScaleFactor;
        }
        
        return spawnedObject;
    }
}