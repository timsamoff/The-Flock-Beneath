using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class ShepherdController : MonoBehaviour
{
    private Camera cam;
    private Rigidbody2D rb;
    private Collider2D col;

    [SerializeField] private float maxMoveSpeed = 5f;
    [SerializeField] private float stopDistance = 0.5f;
    [SerializeField] private float slowdownRange = 3f;

    private Vector2 previousMouseWorldPos;

    private void Awake()
    {
        cam = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        previousMouseWorldPos = rb.position;
    }

    private void FixedUpdate()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Vector2 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

        if (col.OverlapPoint(mouseWorldPos))
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 toMouse = mouseWorldPos - rb.position;
        float distance = toMouse.magnitude;

        if (distance < stopDistance)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = toMouse.normalized;

        float t = Mathf.Clamp01(distance / slowdownRange);
        float moveSpeed = Mathf.SmoothStep(0f, maxMoveSpeed, t);

        rb.MovePosition(rb.position + direction * moveSpeed * Time.fixedDeltaTime);
        rb.linearVelocity = Vector2.zero;

        if ((mouseWorldPos - previousMouseWorldPos).sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            rb.rotation = angle;
        }

        previousMouseWorldPos = mouseWorldPos;

        Debug.Log($"Distance: {distance:F3}, MoveSpeed: {moveSpeed:F3}, Rotation: {rb.rotation:F1}");
    }
}