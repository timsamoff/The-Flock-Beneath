using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System.Reflection;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text uiText;

    [Header("Prefabs")]
    [SerializeField] private GameObject sheepPrefab;
    [SerializeField] private GameObject shepherdPrefab;
    [SerializeField] private GameObject corralPrefab;

    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int level1StartingSheep = 2;
    [SerializeField] private float levelTransitionDelay = 2f;

    [Header("Corral Spawning")]
    [SerializeField] private float screenEdgeBuffer = 0.2f;
    [SerializeField] private float corralScreenPadding = 2f;

    [Header("Sheep Spawning")]
    [SerializeField] private int maxSpawnAttempts = 200;
    [SerializeField] private float minSpawnDistance = 1.0f;
    [SerializeField] private int maxTeleportAttempts = 50;
    [SerializeField] private float corralAvoidanceRadius = 1.0f;

    [Header("Debug Settings")]
    [SerializeField] private bool testMode = false;
    [SerializeField] private int testLevelAmount = 10;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showCorralBounds = true;
    [SerializeField] private bool showScreenBounds = true;
    [SerializeField] private bool showAvoidanceBounds = true;

    private int startingSheep;
    private int totalSheep;
    private int corralledSheep = 0;
    private int lostSheep = 0;

    private List<GameObject> spawnedSheep = new List<GameObject>();
    private List<GameObject> debugLines = new List<GameObject>();
    private GameObject spawnedShepherd;
    private GameObject spawnedCorral;
    private List<Vector3> occupiedPositions = new List<Vector3>();

    private float screenLeft, screenRight, screenTop, screenBottom;
    private Bounds corralAvoidanceBounds;

    // Previous values for detecting inspector changes
    private bool prevShowDebugGizmos;
    private bool prevShowCorralBounds;
    private bool prevShowScreenBounds;
    private bool prevShowAvoidanceBounds;

    private bool isLevelTransitioning = false;

    void Start()
    {
        Cursor.visible = false;
        CalculateScreenBounds();

        // Initialize previous values
        prevShowDebugGizmos = showDebugGizmos;
        prevShowCorralBounds = showCorralBounds;
        prevShowScreenBounds = showScreenBounds;
        prevShowAvoidanceBounds = showAvoidanceBounds;

        StartLevel();
    }

    void Update()
    {
        // Update gizmos
        if (Application.isPlaying && HasDebugSettingsChanged())
        {
            RefreshDebugVisuals();
        }

        // Check for level completion every frame
        CheckLevelComplete();
    }

    bool HasDebugSettingsChanged()
    {
        return showDebugGizmos != prevShowDebugGizmos ||
               showCorralBounds != prevShowCorralBounds ||
               showScreenBounds != prevShowScreenBounds ||
               showAvoidanceBounds != prevShowAvoidanceBounds;
    }

    void RefreshDebugVisuals()
    {
        // Clear existing debug visuals
        foreach (GameObject debugLine in debugLines)
            if (debugLine != null) DestroyImmediate(debugLine);
        debugLines.Clear();

        // Create new ones if enabled
        if (showDebugGizmos)
        {
            CreateRuntimeDebugVisuals();
        }

        // Update previous values
        prevShowDebugGizmos = showDebugGizmos;
        prevShowCorralBounds = showCorralBounds;
        prevShowScreenBounds = showScreenBounds;
        prevShowAvoidanceBounds = showAvoidanceBounds;

        Debug.Log("Debug visuals refreshed");
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
        Debug.Log($"Screen bounds for spawning: Left={screenLeft:F2}, Right={screenRight:F2}, Bottom={screenBottom:F2}, Top={screenTop:F2}");
        Debug.Log($"Screen area: {(screenRight - screenLeft) * (screenTop - screenBottom):F2} square units");
    }

    public void StartLevel()
    {
        isLevelTransitioning = false;
        corralledSheep = 0;
        lostSheep = 0;

        ClearPreviousSpawns();

        int sheepCount = testMode ? testLevelAmount : GetFibonacciSheepCount(currentLevel);
        SetTotalSheep(sheepCount);

        SpawnCorral();
        CalculateCorralAvoidanceArea();
        SpawnShepherd();
        SpawnSheep(sheepCount);

        // Create runtime debug visuals
        if (showDebugGizmos && Application.isPlaying)
        {
            CreateRuntimeDebugVisuals();
        }

        Debug.Log($"Level {currentLevel} started with {sheepCount} sheep. Actually spawned: {spawnedSheep.Count}");

        // UI update after level start
        UpdateUI();
    }

    int GetFibonacciSheepCount(int level)
    {
        if (level <= 1)
        {
            return level1StartingSheep;
        }

        int a = 1;
        int b = level1StartingSheep;

        for (int i = 2; i < level; i++)
        {
            int temp = a + b;
            a = b;
            b = temp;
        }

        return a + b;
    }

    void ClearPreviousSpawns()
    {
        foreach (GameObject sheep in spawnedSheep)
            if (sheep != null) Destroy(sheep);

        if (spawnedShepherd != null)
            Destroy(spawnedShepherd);

        if (spawnedCorral != null)
            Destroy(spawnedCorral);

        // Clear debug visuals
        foreach (GameObject debugLine in debugLines)
            if (debugLine != null) Destroy(debugLine);

        spawnedSheep.Clear();
        debugLines.Clear();
        occupiedPositions.Clear();
        corralledSheep = 0;
        lostSheep = 0;
    }

    void SpawnCorral()
    {
        // Calculate corral placement bounds
        Camera cam = Camera.main;
        float corralLeft, corralRight, corralTop, corralBottom, centerX, centerY;

        if (cam.orthographic)
        {
            float orthoSize = cam.orthographicSize;
            float aspect = cam.aspect;

            corralLeft = -orthoSize * aspect;
            corralRight = orthoSize * aspect;
            corralBottom = -orthoSize;
            corralTop = orthoSize;
            centerX = 0f;
            centerY = 0f;
        }
        else
        {
            float zDistance = Mathf.Abs(cam.transform.position.z);
            Vector3 bottomLeft = cam.ScreenToWorldPoint(new Vector3(0, 0, zDistance));
            Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, zDistance));

            corralLeft = bottomLeft.x;
            corralRight = topRight.x;
            corralBottom = bottomLeft.y;
            corralTop = topRight.y;
            centerX = (corralLeft + corralRight) / 2f;
            centerY = (corralBottom + corralTop) / 2f;
        }

        Vector3 position = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0: // Left
                position = new Vector3(corralLeft - corralScreenPadding, centerY, 0);
                rotation = Quaternion.Euler(0, 0, 0);
                break;
            case 1: // Right
                position = new Vector3(corralRight + corralScreenPadding, centerY, 0);
                rotation = Quaternion.Euler(0, 0, 180);
                break;
            case 2: // Top
                position = new Vector3(centerX, corralTop + corralScreenPadding, 0);
                rotation = Quaternion.Euler(0, 0, -90);
                break;
            case 3: // Bottom
                position = new Vector3(centerX, corralBottom - corralScreenPadding, 0);
                rotation = Quaternion.Euler(0, 0, 90);
                break;
        }

        spawnedCorral = Instantiate(corralPrefab, position, rotation);
        Debug.Log($"Corral spawned at edge {edge} at position: {position}");
    }

    void CalculateCorralAvoidanceArea()
    {
        if (spawnedCorral == null)
        {
            Debug.LogWarning("CalculateCorralAvoidanceArea: spawnedCorral is null!");
            return;
        }

        Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
        if (corralCollider != null)
        {
            Bounds corralBounds = corralCollider.bounds;
            corralBounds.Expand(corralAvoidanceRadius * 2f); // expand in all directions
            corralAvoidanceBounds = corralBounds;

            Debug.Log($"Corral collider found! Type: {corralCollider.GetType().Name}");
            Debug.Log($"Corral pos={spawnedCorral.transform.position}, corralBoundsCenter={corralAvoidanceBounds.center}, size={corralAvoidanceBounds.size}");
            Debug.Log($"Original collider bounds: center={corralCollider.bounds.center}, size={corralCollider.bounds.size}");
        }
        else
        {
            Debug.LogWarning("CalculateCorralAvoidanceArea: No Collider2D found on corral!");
            corralAvoidanceBounds = new Bounds(spawnedCorral.transform.position,
                Vector3.one * (corralAvoidanceRadius * 2f));
        }
    }

    void SpawnShepherd()
    {
        // Check if the corral has been spawned
        if (spawnedCorral == null)
        {
            Debug.LogError("Corral not spawned! Cannot spawn shepherd.");
            return;
        }

        // Spawn shepherd at center of corral
        Vector3 spawnPos = spawnedCorral.transform.position;

        // Center of screen
        Vector3 targetPos = Vector3.zero;

        // Shepherd vs screen center
        Vector3 direction = targetPos - spawnPos;

        // Angle relative to the positive x-axis
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // Face center
        Quaternion spawnRotation = Quaternion.Euler(0, 0, angle - 90f);

        // Spawn shepherd
        spawnedShepherd = Instantiate(shepherdPrefab, spawnPos, spawnRotation);
        occupiedPositions.Add(spawnPos);
    }

    Vector3 GetCorralInteriorPosition()
    {
        if (spawnedCorral == null) return Vector3.zero;

        Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
        if (corralCollider == null) return spawnedCorral.transform.position;

        Bounds corralBounds = corralCollider.bounds;

        // Find position inside corral bounds
        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(corralBounds.min.x, corralBounds.max.x),
                Random.Range(corralBounds.min.y, corralBounds.max.y),
                0f
            );

            // Check if position is inside corral
            if (corralCollider.bounds.Contains(pos))
            {
                return pos;
            }
        }

        return corralBounds.center;
    }

    void SpawnSheep(int count)
    {
        int successfulSpawns = 0;
        int attempts = 0;
        int maxTotalAttempts = maxSpawnAttempts * count;

        float currentMinDistance = minSpawnDistance;

        // Calculate playable screen bounds
        float playableLeft = screenLeft;
        float playableRight = screenRight;
        float playableBottom = screenBottom;
        float playableTop = screenTop;

        Debug.Log($"Attempting to spawn {count} sheep anywhere on screen.");
        Debug.Log($"Playable area: Left={playableLeft:F2}, Right={playableRight:F2}, Bottom={playableBottom:F2}, Top={playableTop:F2}");
        Debug.Log($"Playable area size: {(playableRight - playableLeft) * (playableTop - playableBottom):F2} square units");

        for (int i = 0; i < count && attempts < maxTotalAttempts; i++)
        {
            // Random position within playable screen bounds
            Vector3 spawnPos = new Vector3(
                Random.Range(playableLeft, playableRight),
                Random.Range(playableBottom, playableTop),
                0f
            );

            // Only check min distance from other entities
            bool validDistance = true;
            foreach (Vector3 other in occupiedPositions)
            {
                if (Vector3.Distance(spawnPos, other) < currentMinDistance)
                {
                    validDistance = false;
                    break;
                }
            }

            attempts++;

            if (!validDistance)
            {
                if (attempts % (maxSpawnAttempts / 2) == 0)
                {
                    currentMinDistance = Mathf.Max(0.5f, currentMinDistance * 0.8f);
                    Debug.Log($"Relaxing distance constraint: minDistance={currentMinDistance:F2}");
                }
                i--; // Retry sheep
                continue;
            }

            Debug.Log($"Spawning sheep {i + 1} at position {spawnPos}");

            float randomZ = Random.Range(0f, 360f);
            GameObject sheep = Instantiate(sheepPrefab, spawnPos, Quaternion.Euler(0f, 0f, randomZ));
            spawnedSheep.Add(sheep);
            occupiedPositions.Add(spawnPos);

            // Check if sheep spawned on top of corral and teleport
            bool sheepInCorral = IsPositionInCorral(spawnPos);
            Debug.Log($"Sheep {i + 1} spawn check: position={spawnPos}, inCorral={sheepInCorral}");

            if (sheepInCorral)
            {
                Debug.Log($"DETECTED: Sheep {i + 1} spawned in corral, calling teleport...");
                TeleportSheepAwayFromCorral(sheep);
            }

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

            successfulSpawns++;
        }

        Debug.Log($"Successfully spawned {successfulSpawns}/{count} sheep after {attempts} attempts");
        Debug.Log($"Final constraints used: minDistance={currentMinDistance:F2}");
    }

    bool IsPositionInCorral(Vector3 position)
    {
        if (spawnedCorral == null)
        {
            Debug.Log("IsPositionInCorral: spawnedCorral is null");
            return false;
        }

        Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
        if (corralCollider == null)
        {
            Debug.Log("IsPositionInCorral: No Collider2D found on corral");
            return false;
        }

        // Multiple collision detection methods
        bool overlapPoint = corralCollider.OverlapPoint(position);
        bool boundsContains = corralCollider.bounds.Contains(position);

        Debug.Log($"Position check for {position}:");
        Debug.Log($"  Corral bounds: {corralCollider.bounds}");
        Debug.Log($"  OverlapPoint result: {overlapPoint}");
        Debug.Log($"  Bounds.Contains result: {boundsContains}");

        return overlapPoint || boundsContains;
    }

    void TeleportSheepAwayFromCorral(GameObject sheep)
    {
        Vector3 originalPos = sheep.transform.position;
        Debug.Log($"TELEPORTING: Sheep spawned in corral at {originalPos}, attempting to teleport to safe position...");

        // Try with normal distance constraints
        for (int i = 0; i < maxTeleportAttempts / 2; i++)
        {
            Vector3 newPos = new Vector3(
                Random.Range(screenLeft, screenRight),
                Random.Range(screenBottom, screenTop),
                0f
            );

            bool inCorral = IsPositionInCorral(newPos);
            bool validDistance = IsValidDistanceFromOthers(newPos, minSpawnDistance);

            if (!inCorral && validDistance)
            {
                sheep.transform.position = newPos;
                UpdateOccupiedPositions(originalPos, newPos);
                Debug.Log($"SUCCESS: Sheep teleported from {originalPos} to {newPos} (attempt {i + 1})");
                return;
            }
        }

        Debug.Log("Trying with relaxed distance constraints...");

        float relaxedDistance = minSpawnDistance * 0.5f;

        for (int i = 0; i < maxTeleportAttempts / 2; i++)
        {
            Vector3 newPos = new Vector3(
                Random.Range(screenLeft, screenRight),
                Random.Range(screenBottom, screenTop),
                0f
            );

            bool inCorral = IsPositionInCorral(newPos);
            bool validDistance = IsValidDistanceFromOthers(newPos, relaxedDistance);

            Debug.Log($"Relaxed teleport attempt {i + 1}: pos={newPos}, inCorral={inCorral}, validDistance={validDistance}");

            if (!inCorral && validDistance)
            {
                sheep.transform.position = newPos;
                UpdateOccupiedPositions(originalPos, newPos);
                Debug.Log($"SUCCESS: Sheep teleported with relaxed constraints from {originalPos} to {newPos} (attempt {i + 1})");
                return;
            }
        }

        Debug.LogWarning($"FAILED: Could not find safe teleport position for sheep after {maxTeleportAttempts} attempts. Sheep remains at {originalPos}");
    }

    void CreateRuntimeDebugVisuals()
    {
        // Screen bounds outline
        if (showScreenBounds)
        {
            CreateDebugBounds("ScreenBounds",
                new Vector3((screenLeft + screenRight) / 2f, (screenBottom + screenTop) / 2f, 0f),
                new Vector3(screenRight - screenLeft, screenTop - screenBottom, 0f),
                Color.green);
        }

        // Corral bounds outline
        if (showCorralBounds && spawnedCorral != null)
        {
            Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
            if (corralCollider != null)
            {
                CreateDebugBounds("CorralBounds",
                    corralCollider.bounds.center,
                    corralCollider.bounds.size,
                    Color.yellow);
            }
        }

        // Avoidance bounds outline
        if (showAvoidanceBounds && spawnedCorral != null)
        {
            CreateDebugBounds("AvoidanceBounds",
                corralAvoidanceBounds.center,
                corralAvoidanceBounds.size,
                Color.red);
        }
    }

    void CreateDebugBounds(string name, Vector3 center, Vector3 size, Color color)
    {
        GameObject boundsObject = new GameObject($"Debug_{name}");
        LineRenderer lr = boundsObject.AddComponent<LineRenderer>();

        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 5;
        lr.useWorldSpace = true;
        lr.sortingOrder = 100; // Render on top

        // Calculate corner points of rectangle
        float halfWidth = size.x / 2f;
        float halfHeight = size.y / 2f;

        Vector3[] points = new Vector3[5];
        points[0] = center + new Vector3(-halfWidth, -halfHeight, 0); // Bottom left
        points[1] = center + new Vector3(halfWidth, -halfHeight, 0);  // Bottom right
        points[2] = center + new Vector3(halfWidth, halfHeight, 0);    // Top right
        points[3] = center + new Vector3(-halfWidth, halfHeight, 0);  // Top left
        points[4] = points[0]; // Close rectangle

        lr.SetPositions(points);

        debugLines.Add(boundsObject);
        Debug.Log($"Created runtime debug visual: {name} at {center} with size {size}");
    }

    [ContextMenu("Toggle Debug Visuals")]
    public void ToggleDebugVisuals()
    {
        showDebugGizmos = !showDebugGizmos;
        RefreshDebugVisuals();
        Debug.Log($"Debug visuals {(showDebugGizmos ? "enabled" : "disabled")}");
    }

    void UpdateOccupiedPositions(Vector3 oldPos, Vector3 newPos)
    {
        for (int j = 0; j < occupiedPositions.Count; j++)
        {
            if (Vector3.Distance(occupiedPositions[j], oldPos) < 0.1f)
            {
                occupiedPositions[j] = newPos;
                Debug.Log($"Updated occupied positions list: old pos {oldPos} -> new pos {newPos}");
                return;
            }
        }
        Debug.LogWarning($"Could not find old position {oldPos} in occupied positions list to update");
    }

    bool IsValidDistanceFromOthers(Vector3 pos, float minDistance)
    {
        foreach (Vector3 other in occupiedPositions)
        {
            if (Vector3.Distance(pos, other) < minDistance)
                return false;
        }
        return true;
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

    Vector3 GetValidSpawnPositionAnywhere(float currentMinDistance = -1f)
    {
        if (currentMinDistance < 0) currentMinDistance = minSpawnDistance;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(screenLeft, screenRight),
                Random.Range(screenBottom, screenTop),
                0f
            );

            if (IsValidSpawnPositionAnywhere(pos, currentMinDistance))
            {
                Debug.Log($"Found valid spawn position: {pos} (attempt {i + 1})");
                return pos;
            }
        }

        Debug.LogWarning($"Failed to find valid spawn position after {maxSpawnAttempts} attempts");
        return Vector3.zero;
    }

    bool IsValidSpawnPositionAnywhere(Vector3 pos, float currentMinDistance = -1f)
    {
        if (currentMinDistance < 0) currentMinDistance = minSpawnDistance;

        // Check screen bounds
        if (pos.x < screenLeft || pos.x > screenRight || pos.y < screenBottom || pos.y > screenTop)
            return false;

        // Check min distance from other entities (but allow corral overlap at start)
        foreach (Vector3 other in occupiedPositions)
        {
            if (Vector3.Distance(pos, other) < currentMinDistance)
                return false;
        }

        return true;
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
        startingSheep = count;
        totalSheep = count;
        Debug.Log($"SetTotalSheep called: startingSheep={startingSheep}, totalSheep={totalSheep}");
        UpdateUI();
    }

    public void SheepLeftCorral(SheepBehavior sheep)
    {
        corralledSheep--;
        Debug.Log($"SheepLeftCorral called: corralledSheep is now {corralledSheep}");
        UpdateUI();
    }

    public void SheepEnteredCorral(SheepBehavior sheep)
    {
        corralledSheep++;
        Debug.Log("Sheep entered corral. Current count: " + corralledSheep);
        UpdateUI();
        CheckLevelComplete();
    }

    public void SheepLost(SheepBehavior sheep)
    {
        lostSheep++;
        totalSheep--; // Decrease total count
        
        if (sheep.isCorralled)
        {
            corralledSheep--;
        }
        
        Debug.Log($"SheepLost called: lostSheep={lostSheep}, totalSheep={totalSheep}, corralledSheep={corralledSheep}");

        if (spawnedSheep.Contains(sheep.gameObject))
        {
            spawnedSheep.Remove(sheep.gameObject);
        }
        
        Destroy(sheep.gameObject);

        UpdateUI();
        CheckLevelComplete();
    }

    void CheckLevelComplete()
    {
        if (isLevelTransitioning) return;

        // Count sheep inside the corral
        int sheepActuallyInCorral = 0;
        int aliveSheep = 0;
        
        foreach (var sheep in spawnedSheep)
        {
            if (sheep == null) continue; // Skip lost sheep
            
            aliveSheep++;
            
            if (IsPositionInCorral(sheep.transform.position))
            {
                sheepActuallyInCorral++;
            }
        }
        
        if (aliveSheep > 0 && sheepActuallyInCorral >= aliveSheep)
        {
            Debug.Log($"Level {currentLevel} complete! All {aliveSheep} sheep are physically inside the corral.");
            Debug.Log($"Stats - Alive: {aliveSheep}, In Corral: {sheepActuallyInCorral}, Lost: {lostSheep}");
            
            isLevelTransitioning = true;
            currentLevel++;
            Invoke(nameof(StartLevel), levelTransitionDelay);
        }
    }

    void UpdateUI()
    {
        if (uiText != null)
        {
            string newText = $"Level: {currentLevel}\nCorralled: {corralledSheep}/{startingSheep}\nLost: {lostSheep}";
            uiText.text = newText;
            Debug.Log($"UI Updated: {newText.Replace("\n", " | ")}");
        }
        else
        {
            Debug.LogWarning("UpdateUI called but uiText is null! Please assign the UI Text component in the inspector.");
        }
    }

    public int GetCorralledSheepCount() => corralledSheep;
    public int GetLostSheepCount() => lostSheep;
    public int GetTotalSheepCount() => totalSheep;
    public int GetCurrentLevel() => currentLevel;

    [ContextMenu("Complete Level")]
    public void CompleteLevel() => StartLevel();

    [ContextMenu("Restart Level")]
    public void RestartLevel() => StartLevel();

    [ContextMenu("Test Corral Detection")]
    public void TestCorralDetection()
    {
        if (spawnedCorral == null)
        {
            Debug.Log("No corral spawned to test!");
            return;
        }

        Vector3 corralPos = spawnedCorral.transform.position;
        Debug.Log($"Testing corral detection at corral position: {corralPos}");
        bool result = IsPositionInCorral(corralPos);
        Debug.Log($"Result: {result}");

        // Test a few positions around the corral
        Vector3[] testPositions = {
            corralPos + Vector3.up * 0.5f,
            corralPos + Vector3.down * 0.5f,
            corralPos + Vector3.left * 0.5f,
            corralPos + Vector3.right * 0.5f
        };

        for (int i = 0; i < testPositions.Length; i++)
        {
            bool testResult = IsPositionInCorral(testPositions[i]);
            Debug.Log($"Test position {i}: {testPositions[i]} -> {testResult}");
        }
    }

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
        if (!Application.isPlaying)
        {
            // Gizmos in edit mode
        }

        // Draw gizmos at runtime
        if (Application.isPlaying && showDebugGizmos)
        {
            // Screen bounds = green
            if (showScreenBounds)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(
                    new Vector3((screenLeft + screenRight) / 2f, (screenBottom + screenTop) / 2f, 0f),
                    new Vector3(screenRight - screenLeft, screenTop - screenBottom, 0f));
            }

            // Corral = yellow
            if (showCorralBounds && spawnedCorral != null)
            {
                Collider2D corralCollider = spawnedCorral.GetComponent<Collider2D>();
                if (corralCollider != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(corralCollider.bounds.center, corralCollider.bounds.size);
                }
            }

            // Corral padding = red
            if (showAvoidanceBounds && spawnedCorral != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(corralAvoidanceBounds.center, corralAvoidanceBounds.size);
            }
        }
    }
}