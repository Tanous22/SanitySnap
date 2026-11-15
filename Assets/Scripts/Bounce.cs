using System.Collections;
using UnityEngine;

public class Bounce : MonoBehaviour
{
    public FPController playerController;
    public Camera playerCamera;
    public float maxTargetDistance = 100f;
    public LayerMask targetLayers = ~0;
    public float chargeDuration = 1.5f;
    public float transferDuration = 0.4f;
    public float maxCameraShake = 0.3f;
    public float impactShakeIntensity = 0.5f;
    public float impactShakeDuration = 0.25f;

    private bool isPossessing = false;
    private Coroutine possessionRoutine;
    private Enemy currentTarget;
    private Vector3 originalCameraLocalPos;
    private Transform originalCameraParent;

    void Start()
    {
        if (playerController == null) playerController = GetComponent<FPController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        originalCameraLocalPos = playerCamera.transform.localPosition;
        originalCameraParent = playerCamera.transform.parent;
    }

    void Update()
    {
        if (isPossessing) return;

        if (Input.GetMouseButton(1))
        {
            if (possessionRoutine == null)
            {
                RaycastHit hit;
                if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, maxTargetDistance, targetLayers))
                {
                    if (hit.collider.CompareTag("Enemy"))
                    {
                        Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
                        if (enemy != null)
                        {
                            currentTarget = enemy;
                            possessionRoutine = StartCoroutine(PossessionSequence(enemy));
                        }
                    }
                }
            }
        }
        else
        {
            if (possessionRoutine != null)
            {
                CancelPossession();
            }
        }
    }

    private void CancelPossession()
    {
        if (currentTarget != null)
        {
            currentTarget.ResetGlow();
            currentTarget = null;
        }
        if (possessionRoutine != null)
        {
            StopCoroutine(possessionRoutine);
            possessionRoutine = null;
        }

        if (!isPossessing && playerCamera != null)
            playerCamera.transform.localPosition = originalCameraLocalPos;
    }

    private IEnumerator PossessionSequence(Enemy targetEnemy)
    {
        float elapsed = 0f;

        while (elapsed < chargeDuration)
        {
            if (!Input.GetMouseButton(1))
            {
                CancelPossession();
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / chargeDuration;
            float sineInCurve = 1f - Mathf.Cos((t * Mathf.PI) / 2f);

            Vector3 randomShake = Random.insideUnitSphere * (maxCameraShake * sineInCurve);
            playerCamera.transform.localPosition = originalCameraLocalPos + randomShake;

            targetEnemy.SetGlowIntensity(sineInCurve);

            yield return null;
        }

        isPossessing = true;
        if (playerController != null) playerController.enabled = false;
        targetEnemy.ResetGlow();

        Transform camT = playerCamera.transform;
        camT.SetParent(null);

        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;
        Vector3 flightDirection = (targetEnemy.headTransform.position - startPos).normalized;

        elapsed = 0f;
        while (elapsed < transferDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transferDuration;
            float moveT = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            camT.position = Vector3.Lerp(startPos, targetEnemy.headTransform.position, moveT);
            camT.rotation = Quaternion.Slerp(startRot, targetEnemy.headTransform.rotation, moveT);

            yield return null;
        }

        camT.SetParent(targetEnemy.headTransform);
        camT.localPosition = Vector3.zero;
        camT.localRotation = Quaternion.identity;

        EnemyController ec = targetEnemy.GetComponentInParent<EnemyController>();
        if (ec != null)
        {
            ec.enabled = true;
            ec.StartPossession(playerCamera, this);
        }

        elapsed = 0f;
        Vector3 impactStartPos = camT.localPosition;

        while (elapsed < impactShakeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / impactShakeDuration;
            float dampening = 1f - t;
            Vector3 inertiaWorldOffset = flightDirection * Mathf.Sin(t * Mathf.PI * 6f) * (impactShakeIntensity * dampening);

            camT.localPosition = impactStartPos + camT.InverseTransformDirection(inertiaWorldOffset);

            yield return null;
        }

        camT.localPosition = impactStartPos;
    }

    public void InitiateReturn()
    {
        StartCoroutine(ReturnSequence());
    }

    private IEnumerator ReturnSequence()
    {
        Transform camT = playerCamera.transform;
        camT.SetParent(null);

        Vector3 startPos = camT.position;
        Quaternion startRot = camT.rotation;

        Vector3 targetPos = originalCameraParent.TransformPoint(originalCameraLocalPos);
        Quaternion targetRot = originalCameraParent.rotation;

        float elapsed = 0f;
        while (elapsed < transferDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / transferDuration;
            float moveT = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            camT.position = Vector3.Lerp(startPos, targetPos, moveT);
            camT.rotation = Quaternion.Slerp(startRot, targetRot, moveT);

            yield return null;
        }

        camT.SetParent(originalCameraParent);
        camT.localPosition = originalCameraLocalPos;
        camT.localRotation = Quaternion.identity;

        if (playerController != null) playerController.enabled = true;
        isPossessing = false;
        currentTarget = null;
        possessionRoutine = null;
    }
}