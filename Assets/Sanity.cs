using UnityEngine;
using UnityEngine.UI;

public class Sanity : MonoBehaviour
{
    public float maxSanity = 100f;
    private float currentSanity;

    public Image sanityFill;
    public CanvasGroup deadScreenGroup;

    public AudioSource audioSource;
    public AudioClip deathSound;

    [HideInInspector] public bool isDead = false;

    void Start()
    {
        currentSanity = maxSanity;

        if (sanityFill != null) sanityFill.fillAmount = 1f;

        if (deadScreenGroup != null)
        {
            deadScreenGroup.alpha = 0f;
            deadScreenGroup.blocksRaycasts = false;
        }
    }

    public void TakeDamage(float amount)
    {
        if (currentSanity <= 0) return;

        currentSanity -= amount;

        if (sanityFill != null)
            sanityFill.fillAmount = currentSanity / maxSanity;

        if (currentSanity <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentSanity = 0;
        isDead = true;

        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        if (deadScreenGroup != null)
        {
            deadScreenGroup.alpha = 1f;
            deadScreenGroup.blocksRaycasts = true;
        }

        MonoBehaviour[] allScripts = GetComponents<MonoBehaviour>();
        foreach (var script in allScripts)
        {
            if (script != this) script.enabled = false;
        }
    }
}