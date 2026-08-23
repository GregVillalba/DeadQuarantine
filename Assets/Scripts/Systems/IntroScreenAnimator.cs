using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class IntroScreenAnimator : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private float fadeDuration = 0.6f;
    [SerializeField] private float panelSlideDistance = 40f;

    private CanvasGroup canvasGroup;
    private Vector2 panelRestingPosition;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (panel != null)
            panelRestingPosition = panel.anchoredPosition;
    }

    public void PlayIn()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(isEntering: true, onComplete: null));
    }

    public void PlayOut(System.Action onComplete)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(isEntering: false, onComplete: onComplete));
    }

    private IEnumerator FadeRoutine(bool isEntering, System.Action onComplete)
    {
        float from = isEntering ? 0f : 1f;
        float to = isEntering ? 1f : 0f;

        Vector2 startPos = isEntering ? panelRestingPosition - new Vector2(0f, panelSlideDistance) : panelRestingPosition;
        Vector2 endPos = isEntering ? panelRestingPosition : panelRestingPosition - new Vector2(0f, panelSlideDistance);

        canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            if (panel != null) panel.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

            yield return null;
        }

        canvasGroup.alpha = to;
        if (panel != null) panel.anchoredPosition = endPos;

        if (!isEntering)
        {
            canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        onComplete?.Invoke();
    }
}