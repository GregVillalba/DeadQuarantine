using System.Collections;
using UnityEngine;
using TMPro;

public class RoundStartHUD : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Seguridad")]
    [SerializeField] private float maxVisibleTime = 8f; // por si el Hide() externo nunca llega

    private Coroutine autoHideCoroutine;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show(int round, int countdown)
    {
        gameObject.SetActive(true);

        if (roundText != null)
            roundText.text = round.ToString();

        if (countdownText != null)
            countdownText.text = countdown.ToString();

        // Cada Show() reinicia el temporizador de seguridad.
        if (autoHideCoroutine != null)
            StopCoroutine(autoHideCoroutine);

        autoHideCoroutine = StartCoroutine(AutoHideAfterDelay());
    }

    public void Hide()
    {
        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSeconds(maxVisibleTime);

        gameObject.SetActive(false);
        autoHideCoroutine = null;
    }
}