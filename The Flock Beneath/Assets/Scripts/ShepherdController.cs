using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

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
    [SerializeField] private float stopThreshold = 0.1f; // World units

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 720f;

    // Calculate pixel-perfect stop threshold
    private float pixelStopThreshold = 5f; // Stop when within 5 pixels of target

    private Vector2 velocityRef;
    private Vector2 targetPosition;
    private bool isMouseOver = false;
    private bool isTouching = false;
    private int currentTouchId = -1;
    
    // Mouse control
    private bool mouseInputEnabled = false;
    private Vector2 lastMousePosition;
    private float mouseMovementThreshold = 2f; // Pixels of movement required to enable mouse

    // Touch end handling
    private bool isStoppingFromTouch = false;
    private Vector2 touchEndPosition;

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

        // Enable enhanced touch support 
        EnhancedTouchSupport.Enable();
        
        // Initialize mouse position and disable mouse input
        lastMousePosition = Mouse.current.position.ReadValue();
        mouseInputEnabled = false;

        // Move mouse to shepherd's position
        Vector2 shepherdScreenPos = cam.WorldToScreenPoint(transform.position);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Confined;
        Mouse.current.WarpCursorPosition(shepherdScreenPos);
    }

    private void Update()
    {
        ProcessTouchInput();
        CheckMouseActivation();
    }

    private void CheckMouseActivation()
    {
        if (isTouching || isStoppingFromTouch)
        {
            // Disable mouse input during touch
            mouseInputEnabled = false;
            return;
        }

        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        float mouseMovement = Vector2.Distance(currentMousePos, lastMousePosition);

        // Enable mouse input only if it moves
        if (mouseMovement > mouseMovementThreshold)
        {
            mouseInputEnabled = true;
        }

        lastMousePosition = currentMousePos;
    }

    private void ProcessTouchInput()
    {
        // Reset touch state when no active touches
        if (Touch.activeTouches.Count == 0)
        {
            if (isTouching)
            {
                // Touch ended
                isStoppingFromTouch = true;
                touchEndPosition = targetPosition; // Keep last target position
            }
            isTouching = false;
            currentTouchId = -1;
            return;
        }

        if (isStoppingFromTouch && Touch.activeTouches.Count > 0)
        {
            isStoppingFromTouch = false;
        }

        // Process active touches
        foreach (var touch in Touch.activeTouches)
        {
            if (currentTouchId == -1 || touch.finger.index == currentTouchId)
            {
                Vector2 touchScreenPos = touch.screenPosition;
                Vector2 touchWorldPos = cam.ScreenToWorldPoint(touchScreenPos);
                
                bool touchOverPlayer = col.OverlapPoint(touchWorldPos);
                
                switch (touch.phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        // Disable mouse input when touch begins
                        mouseInputEnabled = false;
                        isStoppingFromTouch = false;
                        
                        if (touchOverPlayer)
                        {
                            isTouching = true;
                            currentTouchId = touch.finger.index;
                            targetPosition = rb.position; // Stop movement
                        }
                        else
                        {
                            isTouching = true;
                            currentTouchId = touch.finger.index;
                            targetPosition = touchWorldPos;
                        }
                        break;
                        
                    case UnityEngine.InputSystem.TouchPhase.Moved:
                        isStoppingFromTouch = false;
                        if (!touchOverPlayer)
                        {
                            // Update target position while dragging
                            targetPosition = touchWorldPos;
                        }
                        else
                        {
                            targetPosition = rb.position;
                        }
                        break;
                        
                    case UnityEngine.InputSystem.TouchPhase.Stationary:
                        isStoppingFromTouch = false;

                        break;
                        
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        if (touch.finger.index == currentTouchId)
                        {
                            isTouching = false;
                            currentTouchId = -1;
                            isStoppingFromTouch = true;
                            touchEndPosition = targetPosition; // Keep last target position
                            
                            // Don't immediately re-enable mouse
                            mouseInputEnabled = false;
                        }
                        break;
                }
            }
        }
    }

    private void FixedUpdate()
    {
        if (!isTouching && !isStoppingFromTouch && mouseInputEnabled)
        {
            ProcessMouseInput();
        }
        else if (!isTouching && !isStoppingFromTouch && !mouseInputEnabled)
        {
            rb.linearVelocity = Vector2.zero;
            velocityRef = Vector2.zero;
            rb.MovePosition(rb.position);
        }
        
        // Apply movement based on target position
        MoveToTarget();
        
        if (isStoppingFromTouch)
        {
            Vector2 currentScreenPos = cam.WorldToScreenPoint(rb.position);
            Vector2 targetScreenPos = cam.WorldToScreenPoint(touchEndPosition);
            float pixelDistance = Vector2.Distance(currentScreenPos, targetScreenPos);
            
            if (pixelDistance <= pixelStopThreshold)
            {
                // Stop complete
                isStoppingFromTouch = false;
                rb.linearVelocity = Vector2.zero;
                velocityRef = Vector2.zero;
            }
        }
    }

    private void ProcessMouseInput()
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
    }

    private void MoveToTarget()
    {
        Vector2 currentPosition = rb.position;
        Vector2 direction = targetPosition - currentPosition;
        
        Vector2 currentScreenPos = cam.WorldToScreenPoint(currentPosition);
        Vector2 targetScreenPos = cam.WorldToScreenPoint(targetPosition);
        float pixelDistance = Vector2.Distance(currentScreenPos, targetScreenPos);
        
        float worldDistance = direction.magnitude;

        if (pixelDistance > pixelStopThreshold)
        {
            Vector2 newPosition = Vector2.SmoothDamp(currentPosition, targetPosition, ref velocityRef, accelerationTime, maxSpeed, Time.fixedDeltaTime);

            // Screen boundary clamping
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
        else if (!isStoppingFromTouch)
        {
            rb.linearVelocity = Vector2.zero;
            velocityRef = Vector2.zero;
        }

        if (worldDistance > stopThreshold)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float smoothedAngle = Mathf.LerpAngle(rb.rotation, targetAngle, rotationSpeed * Time.fixedDeltaTime / 360f);
            rb.rotation = smoothedAngle;
        }
    }

    private void OnDestroy()
    {
        // Clean up
        if (EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Disable();
        }
    }
}