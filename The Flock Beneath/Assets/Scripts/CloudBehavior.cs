using UnityEngine;
using System.Collections.Generic;

public class CloudBehavior : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float speedVariation = 0.5f;
    
    [Header("Sheep Interaction")]
    [SerializeField] private float disengagementChancePerCloud = 0.02f; // 2% chance
    [SerializeField] private float checkInterval = 0.1f;
    
    [Header("Cleanup")]
    [SerializeField] private float despawnBuffer = 2f; // Despawn buffer
    
    private float actualMoveSpeed;
    private float screenRightEdge;
    private List<SheepBehavior> coveredSheep = new List<SheepBehavior>();
    private CloudManager cloudManager;
    private float nextDisengagementCheck;
    
    void Start()
    {
        actualMoveSpeed = moveSpeed + Random.Range(-speedVariation, speedVariation);
        
        // Get screen bounds
        Camera cam = Camera.main;
        if (cam.orthographic)
        {
            screenRightEdge = cam.orthographicSize * cam.aspect + despawnBuffer;
        }
        else
        {
            float zDistance = Mathf.Abs(cam.transform.position.z);
            Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, zDistance));
            screenRightEdge = topRight.x + despawnBuffer;
        }
        
        cloudManager = FindFirstObjectByType<CloudManager>();
        
        nextDisengagementCheck = Time.time + checkInterval;
        
        Debug.Log($"Cloud spawned with speed: {actualMoveSpeed:F2}");
    }
    
    void Update()
    {
        // Move clouds right
        transform.Translate(Vector3.right * actualMoveSpeed * Time.deltaTime);
        
        // Check for disengagement periodically
        if (Time.time >= nextDisengagementCheck)
        {
            ProcessSheepDisengagement();
            nextDisengagementCheck = Time.time + checkInterval;
        }
        
        // Despawn when off screen
        if (transform.position.x > screenRightEdge)
        {
            DespawnCloud();
        }
    }
    
    void OnTriggerEnter2D(Collider2D other)
    {
        SheepBehavior sheep = other.GetComponent<SheepBehavior>();
        if (sheep != null && !coveredSheep.Contains(sheep))
        {
            coveredSheep.Add(sheep);
            sheep.AddCloudCoverage(this);
            Debug.Log($"Sheep entered cloud coverage. Total covered sheep: {coveredSheep.Count}");
        }
    }
    
    void OnTriggerExit2D(Collider2D other)
    {
        SheepBehavior sheep = other.GetComponent<SheepBehavior>();
        if (sheep != null && coveredSheep.Contains(sheep))
        {
            coveredSheep.Remove(sheep);
            sheep.RemoveCloudCoverage(this);
            Debug.Log($"Sheep left cloud coverage. Total covered sheep: {coveredSheep.Count}");
        }
    }
    
    void ProcessSheepDisengagement()
    {
        // Potential disengagement when covering sheep
        for (int i = coveredSheep.Count - 1; i >= 0; i--)
        {
            SheepBehavior sheep = coveredSheep[i];
            
            // Check if sheep still exists
            if (sheep == null || sheep.gameObject == null)
            {
                coveredSheep.RemoveAt(i);
                continue;
            }
            
            // Only affect sheep that are currently following the shepherd
            if (sheep.IsFollowingShepherd())
            {
                // Calculate disengagement chance based on number of clouds covering this sheep
                int totalCloudsOnSheep = sheep.GetCloudCoverageCount();
                float totalDisengagementChance = disengagementChancePerCloud * totalCloudsOnSheep;
                
                // Roll for disengagement
                if (Random.Range(0f, 1f) < totalDisengagementChance)
                {
                    sheep.DisengageFromShepherd();
                    Debug.Log($"Cloud caused sheep to disengage! (Chance: {totalDisengagementChance:P2}, Clouds: {totalCloudsOnSheep})");
                }
            }
        }
    }
    
    void DespawnCloud()
    {
        // Clean up any remaining sheep references
        for (int i = coveredSheep.Count - 1; i >= 0; i--)
        {
            if (coveredSheep[i] != null)
            {
                coveredSheep[i].RemoveCloudCoverage(this);
            }
        }
        coveredSheep.Clear();
        
        // Notify cloud manager
        if (cloudManager != null)
        {
            cloudManager.OnCloudDespawned(this);
        }
        
        Debug.Log("Cloud despawned");
        Destroy(gameObject);
    }
    
    public int GetCoveredSheepCount()
    {
        return coveredSheep.Count;
    }
    
    public float GetMoveSpeed()
    {
        return actualMoveSpeed;
    }
    
    void OnDestroy()
    {
        // Cleanup
        for (int i = 0; i < coveredSheep.Count; i++)
        {
            if (coveredSheep[i] != null)
            {
                coveredSheep[i].RemoveCloudCoverage(this);
            }
        }
    }
}