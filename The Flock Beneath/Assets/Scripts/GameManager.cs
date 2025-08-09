using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text uiText;

    [Header("Prefabs")]
    [SerializeField] private GameObject sheepPrefab;
    [SerializeField] private GameObject shepherdPrefab;
    [SerializeField] private GameObject corralPrefab;

    [Header("Game Areas")]
    [SerializeField] private float screenEdgeBuffer = 0.2f;
    [SerializeField] private float corralScreenPadding = 2f;
    [SerializeField] private float corralAvoidanceRadius = 1.0f;

    [Header("Level Settings")]
    [SerializeField] private bool testMode = false;
    [SerializeField] private int testLevelAmount = 10;
    [SerializeField] private int currentLevel = 1;

    [Header("Spawning")]
    [SerializeField] private int maxSpawnAttempts = 200;
    [SerializeField] private float minSpawnDistance = 1.0f;

    private int startingSheep;
    private int totalSheep;
    private int corralledSheep = 0;
    private int lostSheep = 0;

    private List<GameObject> spawnedSheep = new List<GameObject>();
    private GameObject spawnedShepherd;
    private GameObject spawnedCorral;
    private List<Vector3> occupiedPositions = new List<Vector3>();

    private float screenLeft, screenRight, screenTop, screenBottom;
    private Bounds corralAvoidanceBounds;

    void Start()
    {
        Cursor.visible = false;
        CalculateScreenBounds();
        StartLevel();
    }

    void CalculateScreenBounds()
    {
        Camera cam = Camera.main;

        if (cam.orthographic)
        {
            float orthoSize = cam.orthographicSize;
            float aspect = cam.aspect;

            screenLeft = -orthoSize * aspect + screenEdgeBuffer;
            screenRight = orthoSize * aspect - screenEdgeBuffer;
            screenBottom = -orthoSize + screenEdgeBuffer;
            screenTop = orthoSize - screenEdgeBuffer;
        }
        else
        {
            float zDistance = Mathf.Abs(cam.transform.position.z);

            Vector3 bottomLeft = cam.ScreenToWorldPoint(new Vector3(0, 0, zDistance));
            Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, zDistance));

            screenLeft = bottomLeft.x + screenEdgeBuffer;
            screenRight = topRight.x - screenEdgeBuffer;
            screenBottom = bottomLeft.y + screenEdgeBuffer;
            screenTop = topRight.y - screenEdgeBuffer;
        }

        Debug.Log($"Camera Type: {(cam.orthographic ? "Orthographic" : "Perspective")}");
        Debug.Log($"Screen bounds: Left={screenLeft:F2}, Right={screenRight:F2}, Bottom={screenBottom:F2}, Top={screenTop:F2}");
        Debug.Log($"Screen area: {(screenRight - screenLeft) * (screenTop - screenBottom):F2} square units");
    }

    public void StartLevel()
    {
        ClearPreviousSpawns();

        int sheepCount = testMode ? testLevelAmount : GetFibonacciSheepCount(currentLevel);
        Debug.Log($"Starting Level {currentLevel} - Test Mode: {testMode}, Sheep Count: {sheepCount}");
        SetTotalSheep(sheepCount);

        SpawnCorral();
        CalculateCorralAvoidanceArea();
        SpawnShepherd();
        SpawnSheep(sheepCount);

        Debug.Log($"Level {currentLevel} started with {sheepCount} sheep. Actually spawned: {spawnedSheep.Count}");
    }

    int GetFibonacciSheepCount(int level)
    {
        if (level <= 1) return 2;
        int a = 1, b = 1;
        for (int i = 2; i <= level; i++)
        {
            int temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    void ClearPreviousSpawns()
    {
        foreach (GameObject sheep in spawnedSheep)
            if (sheep != null) Destroy(sheep);

        if (spawnedShepherd != null)
            Destroy(spawnedShepherd);

        if (spawnedCorral != null)
            Destroy(spawnedCorral);

        spawnedSheep.Clear();
        occupiedPositions.Clear();
        corralledSheep = 0;
        lostSheep = 0;
    }

    void SpawnCorral()
    {
        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;
        Vector3 center = new Vector3((screenLeft + screenRight) / 2f, (screenBottom + screenTop) / 2f, 0f);

        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0: // Left
                position = new Vector3(screenLeft - corralScreenPadding, center.y, 0);
                rotation = Quaternion.Euler(0, 0, 0);
                break;
            case 1: // Right
                position = new Vector3(screenRight + corralScreenPadding, center.y, 0);
                rotation = Quaternion.Euler(0, 0, 180);
                break;
            case 2: // Top
                position = new Vector3(center.x, screenTop + corralScreenPadding, 0);
                rotation = Quaternion.Euler(0, 0, -90);
                break;
            case 3: // Bottom
                position = new Vector3(center.x, screenBottom - corralScreenPadding, 0);
                rotation = Quaternion.Euler(0, 0, 90);
                break;
        }

        spawnedCorral = Instantiate(corralPrefab, position, rotation);
    }

    void CalculateCorralAvoidanceArea()
    {
        if (spawnedCorral == null) return;

        Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
        if (corralCollider != null)
        {
            Bounds corralBounds = corralCollider.bounds;
            corralBounds.Expand(corralAvoidanceRadius * 2f); // expand in all directions
            corralAvoidanceBounds = corralBounds;

            Debug.Log($"Corral pos={spawnedCorral.transform.position}, corralBoundsCenter={corralAvoidanceBounds.center}, size={corralAvoidanceBounds.size}");

        }
        else
        {
            corralAvoidanceBounds = new Bounds(spawnedCorral.transform.position,
                Vector3.one * (corralAvoidanceRadius * 2f));
        }
    }

    void SpawnShepherd()
    {
        Vector3 spawnPos = GetValidSpawnPosition();
        if (spawnPos == Vector3.zero)
        {
            Debug.LogWarning("Could not find spawn position for shepherd");
            return;
        }

        spawnedShepherd = Instantiate(shepherdPrefab, spawnPos, Quaternion.identity);
        occupiedPositions.Add(spawnPos);
    }

    void SpawnSheep(int count)
    {
        int successfulSpawns = 0;
        int attempts = 0;
        int maxTotalAttempts = maxSpawnAttempts * count;

        float currentMinDistance = minSpawnDistance;
        float currentCorralAvoidance = corralAvoidanceRadius;

        Debug.Log($"Attempting to spawn {count} sheep. Screen area available: {(screenRight - screenLeft) * (screenTop - screenBottom):F2}");

        for (int i = 0; i < count && attempts < maxTotalAttempts; i++)
        {
            Vector3 spawnPos = GetValidSpawnPosition(currentMinDistance, currentCorralAvoidance);
            attempts++;

            if (spawnPos == Vector3.zero && attempts % (maxSpawnAttempts / 2) == 0)
            {
                currentMinDistance = Mathf.Max(0.5f, currentMinDistance * 0.8f);
                currentCorralAvoidance = Mathf.Max(1f, currentCorralAvoidance * 0.8f);
                Debug.Log($"Relaxing constraints: minDistance={currentMinDistance:F2}, corralAvoidance={currentCorralAvoidance:F2}");
            }

            if (spawnPos == Vector3.zero)
            {
                Debug.LogWarning($"Attempt {attempts}: Could not find valid spawn position for sheep {i + 1}");
                i--; // Retry this sheep
                continue;
            }

            Debug.Log($"Spawning sheep {i + 1} at position {spawnPos}");

            float randomZ = Random.Range(0f, 360f);
            GameObject sheep = Instantiate(sheepPrefab, spawnPos, Quaternion.Euler(0f, 0f, randomZ));
            spawnedSheep.Add(sheep);
            occupiedPositions.Add(spawnPos);

            SheepBehavior sb = sheep.GetComponent<SheepBehavior>();
            if (sb != null)
            {
                sb.SetGameManager(this);

                if (spawnedShepherd != null)
                {
                    typeof(SheepBehavior).GetField("shepherd", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(sb, spawnedShepherd.transform);
                }

                if (spawnedCorral != null)
                {
                    Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
                    if (corralCollider != null)
                    {
                        typeof(SheepBehavior).GetField("corralZone", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.SetValue(sb, corralCollider);
                    }
                }
            }

            if (spawnPos.x < screenLeft || spawnPos.x > screenRight ||
                spawnPos.y < screenBottom || spawnPos.y > screenTop)
            {
                Debug.LogError($"ERROR: Sheep spawned outside screen bounds! Position: {spawnPos}, Bounds: ({screenLeft}, {screenBottom}) to ({screenRight}, {screenTop})");
            }

            successfulSpawns++;
        }

        Debug.Log($"Successfully spawned {successfulSpawns}/{count} sheep after {attempts} attempts");
        Debug.Log($"Final constraints used: minDistance={currentMinDistance:F2}, corralAvoidance={currentCorralAvoidance:F2}");

        if (successfulSpawns < count)
        {
            Debug.LogError($"Could only spawn {successfulSpawns} out of {count} requested sheep. " +
                          "Consider DECREASING screenEdgeBuffer, DECREASING minSpawnDistance, or INCREASING maxSpawnAttempts.");
        }

        if (successfulSpawns != count)
        {
            Debug.Log($"Updating sheep count from {count} to {successfulSpawns}");
            SetTotalSheep(successfulSpawns);
        }
    }

    bool IsSheepVisible(GameObject sheep)
    {
        if (sheep == null) return false;

        Camera cam = Camera.main;
        Vector3 viewportPoint = cam.WorldToViewportPoint(sheep.transform.position);

        return viewportPoint.x >= 0.05f && viewportPoint.x <= 0.95f &&
               viewportPoint.y >= 0.05f && viewportPoint.y <= 0.95f &&
               viewportPoint.z > 0; // In front of camera
    }

    Vector3 GetValidSpawnPosition(float currentMinDistance = -1f, float currentCorralAvoidance = -1f)
    {
        if (currentMinDistance < 0) currentMinDistance = minSpawnDistance;
        if (currentCorralAvoidance < 0) currentCorralAvoidance = corralAvoidanceRadius;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(screenLeft, screenRight),
                Random.Range(screenBottom, screenTop),
                0f
            );

            if (IsValidSpawnPosition(pos, currentMinDistance, currentCorralAvoidance))
            {
                Debug.Log($"Found valid spawn position: {pos} (attempt {i + 1})");
                return pos;
            }
        }

        Debug.LogWarning($"Failed to find valid spawn position after {maxSpawnAttempts} attempts");
        return Vector3.zero;
    }

    bool IsValidSpawnPosition(Vector3 pos, float currentMinDistance = -1f, float currentCorralAvoidance = -1f)
    {
        if (currentMinDistance < 0) currentMinDistance = minSpawnDistance;

        if (pos.x < screenLeft || pos.x > screenRight || pos.y < screenBottom || pos.y > screenTop)
            return false;

        foreach (Vector3 other in occupiedPositions)
        {
            if (Vector3.Distance(pos, other) < currentMinDistance)
                return false;
        }

        if (spawnedCorral != null)
        {
            if (corralAvoidanceBounds.Contains(pos))
                return false;
        }

        return true;
    }

    public void SetTotalSheep(int count)
    {
        startingSheep = totalSheep = count;
        UpdateUI();
    }

    public void CorralSheep(SheepBehavior sheep)
    {
        corralledSheep++;
        UpdateUI();
        CheckLevelComplete();
    }

    public void SheepLost(SheepBehavior sheep)
    {
        lostSheep++;
        totalSheep--;

        if (spawnedSheep.Contains(sheep.gameObject))
            spawnedSheep.Remove(sheep.gameObject);

        UpdateUI();
        CheckLevelComplete();
    }

    void CheckLevelComplete()
    {
        if (corralledSheep == startingSheep)
        {
            Debug.Log($"Level {currentLevel} complete!");
            currentLevel++;
            Invoke(nameof(StartLevel), 2f);
        }
        else if (totalSheep == 0 && corralledSheep < startingSheep)
        {
            Debug.Log("Game Over: Some sheep were lost.");
        }
    }

    void UpdateUI()
    {
        if (uiText != null)
            uiText.text = $"Level: {currentLevel}\nCorralled: {corralledSheep}/{startingSheep}\nLost: {lostSheep}";
    }

    public int GetCorralledSheepCount() => corralledSheep;
    public int GetLostSheepCount() => lostSheep;
    public int GetTotalSheepCount() => totalSheep;
    public int GetCurrentLevel() => currentLevel;

    [ContextMenu("Complete Level")]
    public void CompleteLevel() => StartLevel();

    [ContextMenu("Restart Level")]
    public void RestartLevel() => StartLevel();

    public void SpawnClouds()
    {
        // Placeholder for cloud spawning
        Debug.Log("Cloud spawning - placeholder");
    }

    public void UpdateCloudPositions()
    {
        // Placeholder for cloud movement
        Debug.Log("Cloud update - placeholder");
    }
    
    void OnDrawGizmos()
{
    if (Application.isPlaying && spawnedCorral != null)
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(corralAvoidanceBounds.center, corralAvoidanceBounds.size);
    }
}

}