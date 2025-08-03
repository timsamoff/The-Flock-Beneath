using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class OptimizedSheepBehavior : MonoBehaviour
{
    private enum SheepState
    {
        Grazing,
        Wandering,
        Following,
        Disengaging,
        Corralled,
        SettlingInCorral
    }

    [Header("Wandering Settings")]
    [SerializeField] private float wanderMinDistance = 2f;
    [SerializeField] private float wanderMaxDistance = 8f;
    [SerializeField] private float wanderSpeed = 1.2f;

    [Header("Grazing Settings")]
    [SerializeField] private float minGrazingTime = 2f;
    [SerializeField] private float maxGrazingTime = 5f;

    [Header("Following Settings")]
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float shepherdFollowDistance = 2.5f;
    [SerializeField] private float minFollowTime = 4f;
    [SerializeField] private float disengageCheckInterval = 2f;
    [SerializeField] private float disengageChancePerCheck = 0.02f;

    [Header("Corral Settings")]
    [SerializeField] private float timeToSettleInCorral = 1.5f;

    [Header("Collision Settings")]
    [SerializeField] private float sheepDetectionRadius = 1.2f;
    [SerializeField] private float sheepCollisionDistance = 1.8f;
    [SerializeField] private float collisionCheckInterval = 0.1f;

    [Header("References")]
    [SerializeField] private Transform shepherd;
    [SerializeField] private Collider2D corralZone;

    private Rigidbody2D rb;
    private Collider2D sheepCollider;
    private Camera mainCam;

    private SheepState currentState;
    private Vector2 wanderTarget;
    private Vector2 velocityRef;
    private float stateTimer;
    private float followTimer;
    private float disengageCheckTimer;
    private bool isStuckOnFence = false;
    private float stuckTimer = 0f;
    private const float maxStuckTime = 2f;
    private float timeEnteredCorral = -1f;
    
    private float lastCollisionCheck = 0f;
    private bool hasCollisionThisFrame = false;
    private float shepherdFollowDistanceSqr;
    private float sheepCollisionDistanceSqr;
    private float sheepDetectionRadiusSqr;
    private Vector2 lastValidWanderTarget;
    private int pathfindingAttempts = 5;
    
    private static readonly Collider2D[] tempColliderArray = new Collider2D[10];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sheepCollider = GetComponent<Collider2D>();
        mainCam = Camera.main;

        if (shepherd == null)
            shepherd = GameObject.FindGameObjectWithTag("Player")?.transform;
            
        shepherdFollowDistanceSqr = shepherdFollowDistance * shepherdFollowDistance;
        sheepCollisionDistanceSqr = sheepCollisionDistance * sheepCollisionDistance;
        sheepDetectionRadiusSqr = sheepDetectionRadius * sheepDetectionRadius;
    }

    private void Start()
    {
        EnterGrazingState();
        lastValidWanderTarget = rb.position;
    }

    private void FixedUpdate()
    {
        hasCollisionThisFrame = false;
        if (Time.time - lastCollisionCheck > collisionCheckInterval)
        {
            hasCollisionThisFrame = IsCollidingWithOtherSheep();
            lastCollisionCheck = Time.time;
        }

        if (hasCollisionThisFrame)
        {
            HandleSheepCollision();
            rb.linearVelocity = Vector2.zero;
            return;
        }

        switch (currentState)
        {
            case SheepState.Grazing:
                HandleGrazing();
                break;
            case SheepState.Wandering:
                HandleWandering();
                break;
            case SheepState.Following:
                HandleFollowing();
                break;
            case SheepState.Disengaging:
                HandleDisengaging();
                break;
            case SheepState.SettlingInCorral:
                HandleSettlingInCorral();
                break;
            case SheepState.Corralled:
                HandleCorralled();
                break;
        }

        CheckScreenBounds();
        CheckIfStuck();
    }

    private void EnterGrazingState()
    {
        currentState = SheepState.Grazing;
        stateTimer = Random.Range(minGrazingTime, maxGrazingTime);
        rb.linearVelocity = Vector2.zero;
        isStuckOnFence = false;
        stuckTimer = 0f;
        
        if (IsFullyInsideCorral())
        {
            currentState = SheepState.Corralled;
        }
    }

    private void HandleGrazing()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (IsShepherdInRange() && currentState != SheepState.Corralled)
        {
            EnterFollowingState();
            return;
        }

        if (stateTimer <= 0f)
        {
            if (currentState == SheepState.Corralled)
            {
                wanderTarget = GetRandomPointInCorral();
            }
            else
            {
                wanderTarget = GetValidWanderTarget();
            }
            currentState = SheepState.Wandering;
        }
    }

    private void HandleWandering()
    {
        if (IsShepherdInRange() && currentState != SheepState.Corralled)
        {
            EnterFollowingState();
            return;
        }

        if (currentState == SheepState.Corralled)
        {
            MoveTowardInCorral(wanderTarget, wanderSpeed);
        }
        else
        {
            MoveToward(wanderTarget, wanderSpeed);
        }
        
        RotateTowardMovement();

        if ((rb.position - wanderTarget).sqrMagnitude < 0.04f)
        {
            EnterGrazingState();
        }
    }

    private void EnterFollowingState()
    {
        currentState = SheepState.Following;
        followTimer = 0f;
        disengageCheckTimer = disengageCheckInterval;
        isStuckOnFence = false;
        stuckTimer = 0f;
        timeEnteredCorral = -1f;
    }

    private void HandleFollowing()
    {
        followTimer += Time.fixedDeltaTime;
        disengageCheckTimer -= Time.fixedDeltaTime;

        if (IsFullyInsideCorral())
        {
            float distanceToShepherdSqr = (rb.position - (Vector2)shepherd.position).sqrMagnitude;
            float timeInCorral = GetTimeInCorral();
            
            bool shouldSettle = (timeInCorral > 1f && distanceToShepherdSqr < 9f) ||
                               timeInCorral > 3f ||
                               distanceToShepherdSqr > shepherdFollowDistanceSqr * 4f;
            
            if (shouldSettle)
            {
                EnterSettlingInCorralState();
                return;
            }
        }

        if (followTimer >= minFollowTime && disengageCheckTimer <= 0f)
        {
            disengageCheckTimer = disengageCheckInterval;
            if (Random.value < disengageChancePerCheck)
            {
                wanderTarget = GetValidWanderTarget();
                stateTimer = Random.Range(1f, 2f);
                currentState = SheepState.Disengaging;
                return;
            }
        }

        MoveToward(shepherd.position, followSpeed);
        RotateToward(shepherd.position);
    }

    private void EnterSettlingInCorralState()
    {
        currentState = SheepState.SettlingInCorral;
        stateTimer = timeToSettleInCorral;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        isStuckOnFence = false;
        stuckTimer = 0f;
    }

    private void HandleSettlingInCorral()
    {
        stateTimer -= Time.fixedDeltaTime;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (stateTimer <= 0f)
        {
            EnterGrazingState();
        }
    }

    private void HandleCorralled()
    {
        if (!IsFullyInsideCorral())
        {
            Vector2 corralCenter = corralZone.bounds.center;
            MoveToward(corralCenter, wanderSpeed * 0.5f);
            RotateToward(corralCenter);
            return;
        }

        HandleGrazing();
        
        if (currentState == SheepState.Wandering)
        {
            if (!corralZone.bounds.Contains(wanderTarget))
            {
                wanderTarget = GetRandomPointInCorral();
            }
            
            MoveTowardInCorral(wanderTarget, wanderSpeed);
            RotateTowardMovement();

            if ((rb.position - wanderTarget).sqrMagnitude < 0.04f)
            {
                EnterGrazingState();
            }
        }
    }

    private void HandleDisengaging()
    {
        MoveToward(wanderTarget, wanderSpeed);
        RotateTowardMovement();

        stateTimer -= Time.fixedDeltaTime;
        if (stateTimer <= 0f)
        {
            EnterGrazingState();
        }
    }

    private void CheckIfStuck()
    {
        if (rb.linearVelocity.sqrMagnitude < 0.0001f && 
            (currentState == SheepState.Wandering || currentState == SheepState.Disengaging))
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > maxStuckTime)
            {
                Vector2 awayFromFence = GetDirectionAwayFromFence();
                if (awayFromFence != Vector2.zero)
                {
                    wanderTarget = rb.position + awayFromFence * wanderMaxDistance;
                    lastValidWanderTarget = wanderTarget;
                    stuckTimer = 0f;
                }
                else
                {
                    EnterGrazingState();
                }
                
                isStuckOnFence = false;
            }
        }
        else if (rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            stuckTimer = 0f;
            isStuckOnFence = false;
        }
    }

    private bool IsShepherdInRange()
    {
        if (shepherd == null) return false;
        return (rb.position - (Vector2)shepherd.position).sqrMagnitude <= shepherdFollowDistanceSqr;
    }

    private bool IsFullyInsideCorral()
    {
        if (corralZone == null) return false;
        Bounds corralBounds = corralZone.bounds;
        Bounds sheepBounds = sheepCollider.bounds;
        return corralBounds.Contains(sheepBounds.min) && corralBounds.Contains(sheepBounds.max);
    }

    private Vector2 GetValidWanderTarget()
    {
        for (int i = 0; i < pathfindingAttempts; i++)
        {
            Vector2 target = GetRandomWanderTarget();
            if (!IsFenceBetweenSimple(rb.position, target))
            {
                lastValidWanderTarget = target;
                return target;
            }
        }
        
        if (!IsFenceBetweenSimple(rb.position, lastValidWanderTarget))
        {
            return lastValidWanderTarget;
        }
        
        return rb.position;
    }

    private Vector2 GetRandomWanderTarget()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(wanderMinDistance, wanderMaxDistance);
        return rb.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private Vector2 GetRandomPointInCorral()
    {
        if (corralZone == null) return rb.position;
        
        Bounds bounds = corralZone.bounds;
        float margin = 0.5f;
        return new Vector2(
            Random.Range(bounds.min.x + margin, bounds.max.x - margin),
            Random.Range(bounds.min.y + margin, bounds.max.y - margin)
        );
    }

    private void MoveToward(Vector2 target, float speed)
    {
        if (IsFenceBetweenSimple(rb.position, target))
        {
            Vector2 avoidanceDirection = GetDirectionAwayFromFence();
            if (avoidanceDirection != Vector2.zero)
            {
                Vector2 alternativeTarget = rb.position + avoidanceDirection * wanderMinDistance;
                
                if (!IsFenceBetweenSimple(rb.position, alternativeTarget))
                {
                    wanderTarget = alternativeTarget;
                    target = alternativeTarget;
                }
                else
                {
                    EnterGrazingState();
                    return;
                }
            }
            else
            {
                EnterGrazingState();
                return;
            }
        }

        Vector2 direction = (target - rb.position).normalized;
        Vector2 desiredVelocity = direction * speed;
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, desiredVelocity, ref velocityRef, 0.2f);
    }

    private void MoveTowardInCorral(Vector2 target, float speed)
    {
        if (!corralZone.bounds.Contains(target))
        {
            target = GetRandomPointInCorral();
        }
        
        Vector2 direction = (target - rb.position).normalized;
        Vector2 desiredVelocity = direction * speed;
        Vector2 newVelocity = Vector2.SmoothDamp(rb.linearVelocity, desiredVelocity, ref velocityRef, 0.2f);
        
        Vector2 futurePosition = rb.position + newVelocity * Time.fixedDeltaTime;
        if (corralZone.bounds.Contains(futurePosition))
        {
            rb.linearVelocity = newVelocity;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
            wanderTarget = GetRandomPointInCorral();
        }
    }

    private void RotateToward(Vector2 target)
    {
        Vector2 direction = target - rb.position;
        if (direction.sqrMagnitude < 0.000001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, 0.1f));
    }

    private void RotateTowardMovement()
    {
        Vector2 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        rb.MoveRotation(Mathf.LerpAngle(rb.rotation, targetAngle, 0.1f));
    }

    private bool IsFenceBetweenSimple(Vector2 from, Vector2 to)
    {
        Vector2 direction = (to - from).normalized;
        float distance = Vector2.Distance(from, to);
        
        RaycastHit2D hit = Physics2D.Raycast(from, direction, distance);
        return hit.collider != null && hit.collider.CompareTag("fence");
    }

    private Vector2 GetDirectionAwayFromFence()
    {
        Vector2[] directions = {
            Vector2.up, Vector2.down, Vector2.left, Vector2.right,
            new Vector2(1, 1).normalized, new Vector2(-1, 1).normalized,
            new Vector2(1, -1).normalized, new Vector2(-1, -1).normalized
        };
        
        foreach (Vector2 dir in directions)
        {
            Vector2 testPoint = rb.position + dir * (wanderMinDistance * 1.5f);
            if (!IsFenceBetweenSimple(rb.position, testPoint))
            {
                return dir;
            }
        }
        
        return Vector2.zero;
    }

    private float GetTimeInCorral()
    {
        if (timeEnteredCorral < 0f)
        {
            if (IsFullyInsideCorral())
            {
                timeEnteredCorral = Time.time;
                return 0f;
            }
            else
            {
                return 0f;
            }
        }
        
        return Time.time - timeEnteredCorral;
    }

    private bool IsCollidingWithOtherSheep()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(rb.position, sheepDetectionRadius, tempColliderArray);
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = tempColliderArray[i];
            if (col != sheepCollider && col.CompareTag("sheep"))
            {
                float distanceSqr = (rb.position - (Vector2)col.transform.position).sqrMagnitude;
                if (distanceSqr < sheepCollisionDistanceSqr)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private void HandleSheepCollision()
    {
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        
        EnterGrazingState();
        stateTimer = Random.Range(maxGrazingTime, maxGrazingTime * 1.5f);
    }

    private void CheckScreenBounds()
    {
        Vector3 sheepMin = mainCam.WorldToViewportPoint(sheepCollider.bounds.min);
        Vector3 sheepMax = mainCam.WorldToViewportPoint(sheepCollider.bounds.max);

        if (sheepMax.x < -0.1f || sheepMin.x > 1.1f || sheepMax.y < -0.1f || sheepMin.y > 1.1f)
        {
            Destroy(gameObject);
        }
    }
}