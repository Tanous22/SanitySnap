using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public Image fadeOverlay;
    public GameObject mainMenu;
    public GameObject loreMenu;

    [Header("Settings")]
    public float fadeDuration = 1f;
    public string sceneToLoad = "GameScene";

    private void Start()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            StartCoroutine(FadeFromBlack());
        }
    }

    public void OnPlayPressed()
    {
        StartCoroutine(FadeToBlackAndAction(() =>
        {
            SceneManager.LoadScene(sceneToLoad);
        }));
    }

    public void OnQuitPressed()
    {
        StartCoroutine(FadeToBlackAndAction(() =>
        {
            Application.Quit();
        }));
    }

    public void OnLorePressed()
    {
        StartCoroutine(MenuTransition(mainMenu, loreMenu));
    }

    public void OnBackPressed()
    {
        StartCoroutine(MenuTransition(loreMenu, mainMenu));
    }

    private IEnumerator FadeFromBlack()
    {
        fadeOverlay.raycastTarget = true;
        Color color = fadeOverlay.color;
        color.a = 1f;
        fadeOverlay.color = color;

        float timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadeOverlay.color = color;
            yield return null;
        }

        fadeOverlay.raycastTarget = false;
    }

    private IEnumerator FadeToBlackAndAction(System.Action onComplete)
    {
        fadeOverlay.raycastTarget = true;
        Color color = fadeOverlay.color;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fadeOverlay.color = color;
            yield return null;
        }

        onComplete?.Invoke();
    }

    private IEnumerator MenuTransition(GameObject menuToHide, GameObject menuToShow)
    {
        fadeOverlay.raycastTarget = true;
        Color color = fadeOverlay.color;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fadeOverlay.color = color;
            yield return null;
        }

        if (menuToHide != null) menuToHide.SetActive(false);
        if (menuToShow != null) menuToShow.SetActive(true);

        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadeOverlay.color = color;
            yield return null;
        }

        fadeOverlay.raycastTarget = false;
    }
}