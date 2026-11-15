using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class Intro : MonoBehaviour
{
    public Canvas introCanvas;
    public Image blackScreen;
    public TextMeshProUGUI introText;
    public AudioSource audioSource;
    public AudioClip textSound;

    public float introSpeed = 0.35f;
    public float shakeIntensity = 15f;
    public float waitBeforeVanish = 2f;

    void Start()
    {
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        Vector3 originalTextPos = introText.rectTransform.localPosition;
        introText.transform.localScale = Vector3.zero;
        blackScreen.color = new Color(0f, 0f, 0f, 1f);

        if (audioSource != null && textSound != null)
        {
            audioSource.PlayOneShot(textSound);
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / introSpeed;
            float clampedT = Mathf.Clamp01(t);

            float alpha = Mathf.Clamp01(1f - BackIn(clampedT));
            blackScreen.color = new Color(0f, 0f, 0f, alpha);

            float textScale = QuadIn(clampedT);
            introText.transform.localScale = new Vector3(textScale, textScale, textScale);
            introText.rectTransform.localPosition = originalTextPos + Random.insideUnitSphere * shakeIntensity;

            yield return null;
        }

        introText.transform.localScale = Vector3.one;
        introText.rectTransform.localPosition = originalTextPos;
        blackScreen.color = new Color(0f, 0f, 0f, 0f);

        yield return new WaitForSeconds(waitBeforeVanish);

        introText.transform.localScale = Vector3.zero;

        if (introCanvas != null)
        {
            introCanvas.enabled = false;
        }
    }

    private float QuadIn(float t)
    {
        return t * t;
    }

    private float BackIn(float t)
    {
        float s = 1.70158f;
        return t * t * ((s + 1f) * t - s);
    }
}