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

    private float offscreenTimer = 0f;

    void Start()
    {
        mainCam = Camera.main;

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

        bool offscreen = IsPivotOffscreen();

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

        // Rotate arrow toward prefab
        Vector3 dir = (transform.position - arrowWorldPos).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        arrowInstance.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);

        // Scale arrow
        float distance = Vector3.Distance(transform.position, arrowWorldPos);
        float t = Mathf.Clamp01(distance / maxDistance);
        float scale = Mathf.Lerp(maxScale, minScale, t);
        arrowInstance.transform.localScale = Vector3.one * scale;
    }

    private bool IsPivotOffscreen()
    {
        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        return viewportPos.x < 0 || viewportPos.x > 1 ||
               viewportPos.y < 0 || viewportPos.y > 1;
    }

    void OnDestroy()
    {
        if (arrowInstance != null)
            Destroy(arrowInstance);
    }
}