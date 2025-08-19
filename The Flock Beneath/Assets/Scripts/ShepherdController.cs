using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public class ShepherdController : MonoBehaviour
{
    private Camera cam;
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer spriteRenderer;

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 5f;
    [SerializeField] private float accelerationTime = 0.2f;
    [SerializeField] private float stopThreshold = 0.05f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 720f;

    private Vector2 velocityRef;
    private Vector2 targetPosition;
    private bool isMouseOver = false;

    private Vector2 spriteHalfSize;

    private void Awake()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        rb.freezeRotation = true;

        spriteHalfSize = spriteRenderer.bounds.extents;
        targetPosition = rb.position;

        // Move mouse to the shepherd's position
        Vector2 shepherdScreenPos = cam.WorldToScreenPoint(transform.position);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;

        Mouse.current.WarpCursorPosition(shepherdScreenPos);
    }


    private void FixedUpdate()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        mouseScreenPos.x = Mathf.Clamp(mouseScreenPos.x, 0, Screen.width);
        mouseScreenPos.y = Mathf.Clamp(mouseScreenPos.y, 0, Screen.height);

        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        isMouseOver = col.OverlapPoint(mouseWorldPos);

        if (isMouseOver)
        {
            rb.linearVelocity = Vector2.zero;
            velocityRef = Vector2.zero;

            rb.MovePosition(rb.position);
            return;
        }

        targetPosition = mouseWorldPos;

        Vector2 currentPosition = rb.position;
        Vector2 direction = targetPosition - currentPosition;
        float distance = direction.magnitude;

        if (distance > stopThreshold)
        {
            Vector2 newPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref velocityRef, accelerationTime, maxSpeed, Time.fixedDeltaTime);

            Vector2 screenBottomLeft = cam.ScreenToWorldPoint(Vector3.zero);
            Vector2 screenTopRight = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, 0));

            float minX = screenBottomLeft.x + spriteHalfSize.x / 2f;
            float maxX = screenTopRight.x - spriteHalfSize.x / 2f;
            float minY = screenBottomLeft.y + spriteHalfSize.y / 2f;
            float maxY = screenTopRight.y - spriteHalfSize.y / 2f;

            newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
            newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);

            rb.MovePosition(newPosition);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            velocityRef = Vector2.zero;
        }

        if (direction.sqrMagnitude > stopThreshold * stopThreshold)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float smoothedAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotationSpeed * Time.fixedDeltaTime / 360f);
            rb.rotation = smoothedAngle;
        }
    }

}