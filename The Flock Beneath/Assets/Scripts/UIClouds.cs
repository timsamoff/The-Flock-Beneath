using UnityEngine;

public class UIClouds : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float speedVariation = 0.5f;

    [Header("Screen Settings")]
    [SerializeField] private float buffer = 2f;
    [SerializeField] private float verticalRange = 2f;

    private float actualMoveSpeed;
    private float screenRightEdge;
    private float screenLeftEdge;

    void Start()
    {
        actualMoveSpeed = moveSpeed + Random.Range(-speedVariation, speedVariation);

        Camera cam = Camera.main;
        if (cam.orthographic)
        {
            float camHalfWidth = cam.orthographicSize * cam.aspect;
            screenRightEdge = camHalfWidth + buffer;
            screenLeftEdge = -camHalfWidth - buffer;
        }
        else
        {
            float zDistance = Mathf.Abs(cam.transform.position.z);
            Vector3 topRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, zDistance));
            screenRightEdge = topRight.x + buffer;
            screenLeftEdge = -topRight.x - buffer;
        }
    }

    void Update()
    {
        // Move to the right
        transform.Translate(Vector3.right * actualMoveSpeed * Time.deltaTime);

        // Wrap to the left if past right edge
        if (transform.position.x > screenRightEdge)
        {
            WrapToLeft();
        }
    }

    void WrapToLeft()
    {
        Vector3 pos = transform.position;
        pos.x = screenLeftEdge;

        pos.y += Random.Range(-verticalRange, verticalRange);

        transform.position = pos;
    }
}