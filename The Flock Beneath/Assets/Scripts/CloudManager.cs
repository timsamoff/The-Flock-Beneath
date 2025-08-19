using UnityEngine;
using System.Collections.Generic;

public class CloudManager : MonoBehaviour
{
    [Header("Cloud Prefabs")]
    [SerializeField] private GameObject[] cloudPrefabs;
    
    [Header("Spawning Settings")]
    [SerializeField] private float spawnInterval = 8f; // Time between cloud spawns
    [SerializeField] private float spawnIntervalVariation = 3f; // Random timing
    [SerializeField] private float spawnBuffer = 2f; // Spawn cloud buffer
    
    [Header("Cloud Settings")]
    [SerializeField] private int maxCloudsOnScreen = 5;
    [SerializeField] private bool enableCloudSpawning = true;
    
    [Header("Level Scaling")]
    [SerializeField] private float difficultyScaling = 0.1f; // Spawn rate increase per new level
    [SerializeField] private int minLevel = 3; // Don't spawn clouds until this level
    
    private float nextSpawnTime;
    private float screenLeftEdge;
    private float screenTop, screenBottom;
    private List<CloudBehavior> activeClouds = new List<CloudBehavior>();
    private GameManager gameManager;
    
    void Start()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        
        CalculateScreenBounds();
        
        // Set initial spawn time
        ScheduleNextSpawn();
        
        Debug.Log($"CloudManager initialized. Spawn area: Left={screenLeftEdge:F2}, Top={screenTop:F2}, Bottom={screenBottom:F2}");
    }
    
    void Update()
    {
        if (!enableCloudSpawning) return;
        
        if (Time.time >= nextSpawnTime && ShouldSpawnCloud())
        {
            SpawnCloud();
            ScheduleNextSpawn();
        }
    }
    
    void CalculateScreenBounds()
    {
        Camera cam = Camera.main;
        
        if (cam.orthographic)
        {
            float orthoSize = cam.orthographicSize;
            float aspect = cam.aspect;
            
            screenLeftEdge = -orthoSize * aspect - spawnBuffer;
            screenTop = orthoSize;
            screenBottom = -orthoSize;
        }
        else
        {
            float zDistance = Mathf.Abs(cam.transform.position.z);
            Vector3 bottomLeft = cam.ScreenToWorldPoint(new Vector3(0, 0, zDistance));
            Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, zDistance));
            
            screenLeftEdge = bottomLeft.x - spawnBuffer;
            screenTop = topRight.y;
            screenBottom = bottomLeft.y;
        }
    }
    
    bool ShouldSpawnCloud()
    {
        // Don't spawn if too many clouds
        if (activeClouds.Count >= maxCloudsOnScreen) return false;
        
        // Don't spawn clouds on early levels
        if (gameManager != null && gameManager.GetCurrentLevel() < minLevel) return false;
        
        return true;
    }
    
    void SpawnCloud()
    {
        if (cloudPrefabs == null || cloudPrefabs.Length == 0)
        {
            Debug.LogWarning("No cloud prefabs assigned to CloudManager!");
            return;
        }
        
        // Choose random cloud prefab
        GameObject cloudPrefab = cloudPrefabs[Random.Range(0, cloudPrefabs.Length)];
        
        // Random Y position within screen bounds
        float yPosition = Random.Range(screenBottom, screenTop);
        Vector3 spawnPosition = new Vector3(screenLeftEdge, yPosition, 0f);
        
        // Spawn cloud
        GameObject cloudObject = Instantiate(cloudPrefab, spawnPosition, Quaternion.identity);
        CloudBehavior cloudBehavior = cloudObject.GetComponent<CloudBehavior>();
        
        if (cloudBehavior != null)
        {
            activeClouds.Add(cloudBehavior);
            Debug.Log($"Cloud spawned at {spawnPosition}. Active clouds: {activeClouds.Count}");
        }
        else
        {
            Debug.LogWarning("Spawned cloud prefab doesn't have CloudBehavior component!");
            Destroy(cloudObject);
        }
    }
    
    void ScheduleNextSpawn()
    {
        float baseInterval = spawnInterval;
        
        // Increase spawn rate based on level difficulty
        if (gameManager != null)
        {
            int currentLevel = gameManager.GetCurrentLevel();
            float levelModifier = Mathf.Max(0.3f, 1f - (currentLevel - minLevel) * difficultyScaling);
            baseInterval *= levelModifier;
        }
        
        // Add random variation
        float variation = Random.Range(-spawnIntervalVariation, spawnIntervalVariation);
        float actualInterval = Mathf.Max(1f, baseInterval + variation); // Minimum 1 second
        
        nextSpawnTime = Time.time + actualInterval;
        
        Debug.Log($"Next cloud spawn in {actualInterval:F1} seconds");
    }
    
    public void OnCloudDespawned(CloudBehavior cloud)
    {
        if (activeClouds.Contains(cloud))
        {
            activeClouds.Remove(cloud);
            Debug.Log($"Cloud removed from active list. Active clouds: {activeClouds.Count}");
        }
    }
    
    public void ClearAllClouds()
    {
        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            if (activeClouds[i] != null)
            {
                Destroy(activeClouds[i].gameObject);
            }
        }
        activeClouds.Clear();
        Debug.Log("All clouds cleared");
    }
    
    public void SetCloudSpawning(bool enabled)
    {
        enableCloudSpawning = enabled;
        Debug.Log($"Cloud spawning {(enabled ? "enabled" : "disabled")}");
    }
    
    public int GetActiveCloudCount()
    {
        return activeClouds.Count;
    }
    
    [ContextMenu("Spawn Cloud Now")]
    public void SpawnCloudNow()
    {
        if (ShouldSpawnCloud())
        {
            SpawnCloud();
        }
        else
        {
            Debug.Log("Cannot spawn cloud - conditions not met");
        }
    }
    
    [ContextMenu("Clear All Clouds")]
    public void ClearAllCloudsMenuItem()
    {
        ClearAllClouds();
    }
}