using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon;

    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private Image staminaFill;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshairRoot;
    [SerializeField] private RectTransform dashTop;
    [SerializeField] private RectTransform dashBottom;
    [SerializeField] private RectTransform dashLeft;
    [SerializeField] private RectTransform dashRight;
    [SerializeField] private float crosshairMinGap = 6f;
    [SerializeField] private float crosshairMaxGap = 22f;

    private void Update()
    {
        UpdateHealthText();
        UpdateAmmoText();
        UpdateStaminaBar();
        UpdateCrosshair();
    }

    private void UpdateHealthText()
    {
        healthText.text = "Vida: " + playerHealth.CurrentHealth;
    }

    private void UpdateAmmoText()
    {
        ammoText.text = weapon.CurrentAmmo + " / " + weapon.MaxAmmo;
    }

    private void UpdateStaminaBar()
    {
        staminaFill.fillAmount = playerMovement.CurrentStamina / playerMovement.MaxStamina;
    }

    private void UpdateCrosshair()
    {
        if (weapon.IsAiming)
        {
            crosshairRoot.SetActive(false);
            return;
        }

        crosshairRoot.SetActive(true);

        float gap = Mathf.Lerp(crosshairMinGap, crosshairMaxGap, weapon.CurrentSpreadNormalized);

        dashTop.anchoredPosition = new Vector2(0f, gap);
        dashBottom.anchoredPosition = new Vector2(0f, -gap);
        dashLeft.anchoredPosition = new Vector2(-gap, 0f);
        dashRight.anchoredPosition = new Vector2(gap, 0f);
    }
}