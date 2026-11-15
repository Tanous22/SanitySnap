using UnityEngine;

public class EnemyGunnerSight : MonoBehaviour
{
    [Header("Vision Settings")]
    public float sightDistance = 30f;
    public float fieldOfView = 90f;
    public LayerMask obstacleLayers;

    [Header("Combat Settings")]
    public float shootDistance = 20f;
    public float stopDistance = 5f;
    public float fireRate = 0.5f;
    public float bulletDamage = 15f;
    public float aimDelay = 0.5f;

    public Transform playerTarget;

    private EnemyController enemyController;
    private Animator animator;
    private float lastShootTime = -999f;
    private float aimTimer = 0f;
    private Vector3 lastKnownPos;
    private bool isInvestigating = false;
    private float investigateTimer = 0f;

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
            enemyController.isAiming = false;
        }
    }

    public void SuspectAndInvestigate(Vector3 pos)
    {
        lastKnownPos = pos;
        isInvestigating = true;
        investigateTimer = 8f;
    }

    void Update()
    {
        if (enemyController == null || !enemyController.enabled || enemyController.isPossessed || enemyController.isStunned || enemyController.isDead)
        {
            if (enemyController != null)
            {
                enemyController.aiIsChasing = false;
                enemyController.aiInput = Vector2.zero;
                enemyController.isAiming = false;
            }
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
                    enemyController.isAiming = false;
                    return;
                }
            }
        }

        if (GetComponentInChildren<Rigidbody>() != null && !GetComponentInChildren<Rigidbody>().isKinematic) return;

        if (playerTarget != null || isInvestigating)
        {
            HandleGunnerBehavior();
        }
    }

    private void HandleGunnerBehavior()
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
            isInvestigating = true;
            investigateTimer = 8f;
            
            enemyController.isAiming = true;
            aimTimer += Time.deltaTime;
            
            Vector3 lookPos = playerTarget.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);

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

            if (distance <= shootDistance && aimTimer >= aimDelay && Time.time >= lastShootTime + fireRate)
            {
                lastShootTime = Time.time;
                if (animator != null) animator.SetTrigger("Shoot");
                
                enemyController.PlayShootSound();

                Sanity targetSanity = playerTarget.GetComponent<Sanity>();
                if (targetSanity != null) targetSanity.TakeDamage(bulletDamage);
                
                EnemyController targetEnemy = playerTarget.GetComponent<EnemyController>();
                if (targetEnemy != null) targetEnemy.TakeDamage(bulletDamage, transform);
            }
        }
        else
        {
            enemyController.isAiming = false;
            aimTimer = 0f;

            if (isInvestigating)
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
                    isInvestigating = false;
                    enemyController.aiIsChasing = false;
                    enemyController.aiInput = Vector2.zero;
                }
            }
            else
            {
                enemyController.aiIsChasing = false;
                enemyController.aiInput = Vector2.zero;
            }
        }
    }
}