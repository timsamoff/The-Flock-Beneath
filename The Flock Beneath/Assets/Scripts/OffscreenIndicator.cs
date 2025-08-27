using UnityEngine;

public class OffscreenIndicator : MonoBehaviour
{
    [Header("Arrow Settings")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private float edgeBuffer = 30f;
    [SerializeField] private float minScale = 0.01f;
    [SerializeField] private float maxScale = 0.5f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private float offscreenDelay = 1.0f;

    private Camera mainCam;
    private GameObject arrowInstance;
    private Collider2D sheepCollider;

    private float offscreenTimer = 0f;

    void Start()
    {
        mainCam = Camera.main;
        sheepCollider = GetComponent<Collider2D>();

        if (arrowPrefab != null)
        {
            arrowInstance = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
            arrowInstance.SetActive(false);
        }
        else
        {
            Debug.LogError("Arrow prefab not assigned on " + gameObject.name);
        }
    }

    void Update()
    {
        if (mainCam == null || arrowInstance == null) return;

        bool offscreen = IsColliderOffscreen();

        // Update timer
        if (offscreen)
        {
            offscreenTimer += Time.deltaTime;
        }
        else
        {
            offscreenTimer = 0f;
        }

        if (offscreenTimer >= offscreenDelay)
        {
            if (!arrowInstance.activeSelf)
                arrowInstance.SetActive(true);

            UpdateArrowPositionAndRotation();
        }
        else
        {
            if (arrowInstance.activeSelf)
                arrowInstance.SetActive(false);
        }
    }

    private void UpdateArrowPositionAndRotation()
    {
        Vector3 screenPos = mainCam.WorldToScreenPoint(transform.position);

        // Clamp to screen edges
        float clampedX = Mathf.Clamp(screenPos.x, edgeBuffer, Screen.width - edgeBuffer);
        float clampedY = Mathf.Clamp(screenPos.y, edgeBuffer, Screen.height - edgeBuffer);

        if (screenPos.y > Screen.height) clampedY = Screen.height - edgeBuffer;
        if (screenPos.y < 0) clampedY = edgeBuffer;
        if (screenPos.x > Screen.width) clampedX = Screen.width - edgeBuffer;
        if (screenPos.x < 0) clampedX = edgeBuffer;

        screenPos.z = Mathf.Abs(mainCam.transform.position.z);
        Vector3 arrowWorldPos = mainCam.ScreenToWorldPoint(new Vector3(clampedX, clampedY, screenPos.z));
        arrowInstance.transform.position = arrowWorldPos;

        // Rotate toward sheep
        Vector3 dir = (transform.position - arrowWorldPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrowInstance.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        float distFromEdgeX = Mathf.Min(Mathf.Abs(viewportPos.x), Mathf.Abs(viewportPos.x - 1));
        float distFromEdgeY = Mathf.Min(Mathf.Abs(viewportPos.y), Mathf.Abs(viewportPos.y - 1));
        float distFromEdge = Mathf.Min(distFromEdgeX, distFromEdgeY);

        float t = Mathf.Clamp01(distFromEdge * 2);
        float scale = Mathf.Lerp(minScale, maxScale, 1 - t);
        arrowInstance.transform.localScale = Vector3.one * scale;
    }

    private bool IsColliderOffscreen()
    {
        if (sheepCollider == null || mainCam == null) return true;

        Bounds bounds = sheepCollider.bounds;
        Vector3[] corners = new Vector3[4]
        {
            new Vector3(bounds.min.x, bounds.min.y),
            new Vector3(bounds.min.x, bounds.max.y),
            new Vector3(bounds.max.x, bounds.min.y),
            new Vector3(bounds.max.x, bounds.max.y)
        };

        foreach (var corner in corners)
        {
            Vector3 viewportPos = mainCam.WorldToViewportPoint(corner);
            if (viewportPos.x >= 0 && viewportPos.x <= 1 &&
                viewportPos.y >= 0 && viewportPos.y <= 1)
            {
                return false;
            }
        }

        return true;
    }

    void OnDestroy()
    {
        if (arrowInstance != null)
            Destroy(arrowInstance);
    }
}