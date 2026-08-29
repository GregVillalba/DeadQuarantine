using UnityEngine;
using TMPro;

public class RoundStartHUD : MonoBehaviour
{
    [Header("Textos")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI countdownText;

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
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}