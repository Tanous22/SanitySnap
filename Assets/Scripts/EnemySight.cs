using UnityEngine;
using System.Collections;

public class EnemySight : MonoBehaviour
{
    [Header("Vision Settings")]
    public float sightDistance = 20f;
    public float fieldOfView = 90f;
    public LayerMask obstacleLayers;

    [Header("Combat Settings")]
    public float attackTriggerDistance = 2.5f;
    public float stopDistance = 1.0f;
    public float punchDuration = 1.2f;
    public float attackCooldown = 2.0f;
    public float retreatDuration = 1.0f;

    [Header("Damage Settings")]
    public float punchDamage = 25f;
    public float punchImpactDelay = 0.3f;

    public Transform playerTarget;

    private EnemyController enemyController;
    private Animator animator;

    private float lastAttackTime = -999f;
    private float stateTimer = 0f;
    private Vector3 lastKnownPos;
    private float investigateTimer = 0f;

    private enum AIState { Idle, Chasing, Attacking, Retreating, Investigating }
    private AIState currentState = AIState.Idle;

    void Start()
    {
        enemyController = GetComponent<EnemyController>();
        animator = GetComponentInChildren<Animator>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTarget = player.transform;
        }
    }

    void OnDisable()
    {
        if (enemyController != null)
        {
            enemyController.aiIsChasing = false;
            enemyController.aiInput = Vector2.zero;
        }
        currentState = AIState.Idle;
    }

    public void SuspectAndInvestigate(Vector3 pos)
    {
        lastKnownPos = pos;
        currentState = AIState.Investigating;
        investigateTimer = 8f;
    }

    void Update()
    {
        if (enemyController == null || enemyController.enabled == false || enemyController.isPossessed || enemyController.isStunned || enemyController.isDead)
        {
            if (enemyController != null)
            {
                enemyController.aiIsChasing = false;
                enemyController.aiInput = Vector2.zero;
            }
            currentState = AIState.Idle;
            return;
        }
        
        if (playerTarget != null)
        {
            Sanity pSanity = playerTarget.GetComponent<Sanity>();
            EnemyController pEnemy = playerTarget.GetComponent<EnemyController>();

            if ((pSanity != null && pSanity.isDead) || (pEnemy != null && pEnemy.isDead))
            {
                GameObject mainPlayer = GameObject.FindGameObjectWithTag("Player");
                if (mainPlayer != null && playerTarget != mainPlayer.transform)
                {
                    playerTarget = mainPlayer.transform;
                }
                else
                {
                    enemyController.aiIsChasing = false;
                    enemyController.aiInput = Vector2.zero;
                    currentState = AIState.Idle;
                    return;
                }
            }
        }

        if (playerTarget != null || currentState == AIState.Investigating)
        {
            HandleAIBehavior();
        }
    }

    private void HandleAIBehavior()
    {
        Vector3 directionToPlayer = playerTarget != null ? (playerTarget.position - transform.position) : Vector3.zero;
        float distance = directionToPlayer.magnitude;
        bool canSeePlayer = false;

        if (playerTarget != null && distance < sightDistance)
        {
            float angle = Vector3.Angle(transform.forward, directionToPlayer);
            if (angle < fieldOfView / 2f)
            {
                Vector3 rayOrigin = transform.position + Vector3.up;
                if (!Physics.Raycast(rayOrigin, directionToPlayer.normalized, distance, obstacleLayers))
                {
                    canSeePlayer = true;
                }
            }
        }

        if (canSeePlayer)
        {
            lastKnownPos = playerTarget.position;
            if (currentState == AIState.Idle || currentState == AIState.Investigating)
            {
                currentState = AIState.Chasing;
            }
        }
        else
        {
            if (currentState == AIState.Chasing || currentState == AIState.Attacking || currentState == AIState.Retreating)
            {
                currentState = AIState.Investigating;
                investigateTimer = 8f;
            }
        }

        if (currentState == AIState.Retreating)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) currentState = AIState.Chasing;
            else
            {
                enemyController.aiIsChasing = true;
                enemyController.aiInput = new Vector2(0f, -1f);
                return;
            }
        }
        else if (currentState == AIState.Attacking)
        {
            stateTimer -= Time.deltaTime;

            if (distance > stopDistance)
            {
                enemyController.aiIsChasing = true;
                enemyController.aiInput = new Vector2(0f, 1f);
            }
            else
            {
                enemyController.aiIsChasing = false;
                enemyController.aiInput = Vector2.zero;
            }

            if (stateTimer <= 0f)
            {
                currentState = AIState.Retreating;
                stateTimer = retreatDuration;
            }
            return;
        }
        else if (currentState == AIState.Investigating)
        {
            investigateTimer -= Time.deltaTime;
            if (investigateTimer > 0f)
            {
                float distToKnown = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(lastKnownPos.x, 0, lastKnownPos.z));
                if (distToKnown > 1.5f)
                {
                    Vector3 lookPos = lastKnownPos;
                    lookPos.y = transform.position.y;
                    transform.LookAt(lookPos);
                    enemyController.aiIsChasing = true;
                    enemyController.aiInput = new Vector2(0f, 1f);
                }
                else
                {
                    transform.Rotate(0, 60f * Time.deltaTime, 0);
                    enemyController.aiIsChasing = false;
                    enemyController.aiInput = Vector2.zero;
                }
            }
            else
            {
                currentState = AIState.Idle;
            }
        }
        else if (canSeePlayer)
        {
            Vector3 lookPos = playerTarget.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);

            if (distance <= attackTriggerDistance && Time.time >= lastAttackTime + attackCooldown)
            {
                ExecuteAttack();
            }
            else
            {
                if (distance > stopDistance)
                {
                    enemyController.aiIsChasing = true;
                    enemyController.aiInput = new Vector2(0f, 1f);
                }
                else
                {
                    enemyController.aiIsChasing = false;
                    enemyController.aiInput = Vector2.zero;
                }
            }
        }
        else
        {
            enemyController.aiIsChasing = false;
            enemyController.aiInput = Vector2.zero;
        }
    }

    private void ExecuteAttack()
    {
        lastAttackTime = Time.time;

        if (animator != null)
        {
            if (Random.value > 0.5f) animator.SetTrigger("PunchLeft");
            else animator.SetTrigger("PunchRight");
        }

        enemyController.PlayPunchSound();

        currentState = AIState.Attacking;
        stateTimer = punchDuration;

        StartCoroutine(DealDamageAfterDelay());
    }

    private IEnumerator DealDamageAfterDelay()
    {
        yield return new WaitForSeconds(punchImpactDelay);

        if (playerTarget != null)
        {
            float currentDist = Vector3.Distance(transform.position, playerTarget.position);
            if (currentDist <= attackTriggerDistance + 0.5f)
            {
                Sanity targetSanity = playerTarget.GetComponent<Sanity>();
                if (targetSanity != null) targetSanity.TakeDamage(punchDamage);

                EnemyController targetEnemy = playerTarget.GetComponent<EnemyController>();
                if (targetEnemy != null) targetEnemy.TakeDamage(punchDamage, transform);
            }
        }
    }
}