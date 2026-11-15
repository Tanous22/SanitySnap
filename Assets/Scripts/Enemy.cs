using UnityEngine;

public class Enemy : MonoBehaviour
{
    [Header("Possession Targets")]
    public Transform headTransform;

    [Header("Glow Settings")]
    public Renderer bodyRenderer;
    [ColorUsage(true, true)]
    public Color glowColor = new Color(2f, 0f, 0f);

    private Material mat;
    private Color originalColor;

    void Start()
    {
        gameObject.tag = "Enemy";

        if (bodyRenderer != null)
        {
            mat = bodyRenderer.material;
            if (mat.HasProperty("_EmissionColor"))
            {
                originalColor = mat.GetColor("_EmissionColor");
            }
        }
    }

    public void SetGlowIntensity(float intensity)
    {
        if (mat != null)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.Lerp(originalColor, glowColor * 10f, intensity));
        }
    }

    public void ResetGlow()
    {
        if (mat != null)
        {
            mat.SetColor("_EmissionColor", originalColor);
            mat.DisableKeyword("_EMISSION");
        }
    }
}