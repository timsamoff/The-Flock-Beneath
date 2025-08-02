using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class SheepBehavior : MonoBehaviour
{
    private enum SheepState
    {
        Grazing,
        Wandering,
        Following,
        Disengaging
    }

    [Header("Movement")]
    [SerializeField] private float wanderSpeed = 1.2f;
    [SerializeField] private float followSpeed = 2f;
    [SerializeField] private float shepherdFollowDistance = 2.5f;
    [SerializeField] private float disengageChance = 0.01f;
    [SerializeField] private float disengageWanderTime = 1.5f;

    [Header("Grazing & Wandering Timing")]
    [SerializeField] private float minGrazingTime = 2f;
    [SerializeField] private float maxGrazingTime = 5f;

    [Header("References")]
    [SerializeField] private Transform shepherd;
    [SerializeField] private Collider2D corralZone;

    private Rigidbody2D rb;
    private Camera mainCam;

    private SheepState currentState = SheepState.Grazing;

    private float stateTimer;
    private Vector2 wanderTarget;
    private Vector2 velocityRef = Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;

        if (shepherd == null)
            shepherd = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    private void Start()
    {
        EnterGrazingState();
    }

    private void FixedUpdate()
    {
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
        }

        CheckScreenBounds();
    }

    private void EnterGrazingState()
    {
        currentState = SheepState.Grazing;
        stateTimer = Random.Range(minGrazingTime, maxGrazingTime);
        rb.linearVelocity = Vector2.zero;
    }

    private void HandleGrazing()
    {
        stateTimer -= Time.fixedDeltaTime;

        if (IsShepherdInRange())
        {
            currentState = SheepState.Following;
            return;
        }

        if (stateTimer <= 0f)
        {
            wanderTarget = GetRandomWanderTarget();
            currentState = SheepState.Wandering;
        }
    }

    private void HandleWandering()
    {
        if (IsShepherdInRange())
        {
            currentState = SheepState.Following;
            return;
        }

        MoveToward(wanderTarget, wanderSpeed);

        if (Vector2.Distance(rb.position, wanderTarget) < 0.2f)
        {
            EnterGrazingState();
        }
    }

    private void HandleFollowing()
    {
        if (corralZone.bounds.Contains(rb.position))
        {
            EnterGrazingState(); // Sheep is corralled
            return;
        }

        if (Random.value < disengageChance)
        {
            wanderTarget = GetRandomWanderTarget();
            stateTimer = disengageWanderTime;
            currentState = SheepState.Disengaging;
            return;
        }

        MoveToward(shepherd.position, followSpeed);
    }

    private void HandleDisengaging()
    {
        MoveToward(wanderTarget, wanderSpeed);

        stateTimer -= Time.fixedDeltaTime;

        if (stateTimer <= 0f)
        {
            EnterGrazingState();
        }
    }

    private bool IsShepherdInRange()
    {
        if (shepherd == null) return false;
        return Vector2.Distance(rb.position, shepherd.position) <= shepherdFollowDistance;
    }

    private Vector2 GetRandomWanderTarget()
    {
        float x = Random.Range(-20f, 20f); // no min/max — wide range
        float y = Random.Range(-20f, 20f);
        return (Vector2)transform.position + new Vector2(x, y);
    }

    private void MoveToward(Vector2 target, float speed)
    {
        Vector2 direction = (target - rb.position).normalized;
        Vector2 desiredVelocity = direction * speed;
        rb.linearVelocity = Vector2.SmoothDamp(rb.linearVelocity, desiredVelocity, ref velocityRef, 0.2f);
    }

    private void CheckScreenBounds()
    {
        Vector3 screenPoint = mainCam.WorldToViewportPoint(transform.position);
        if (screenPoint.x < 0f || screenPoint.x > 1f || screenPoint.y < 0f || screenPoint.y > 1f)
        {
            Destroy(gameObject);
            // TODO: Notify GameManager that a sheep was lost
        }
    }
}