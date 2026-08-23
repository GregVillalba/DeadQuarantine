using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class HUDController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Weapon weapon;

    [Header("Vida")]
    [SerializeField] private Image healthFill;
    [SerializeField] private TextMeshProUGUI healthPercentText;
    [SerializeField] private Color healthColorFull = Color.white;
    [SerializeField] private Color healthColorHalf = new Color(1f, 0.65f, 0.3f);
    [SerializeField] private Color healthColorLow = Color.red;

    [Header("Estamina")]
    [SerializeField] private Image staminaFill;

    [Header("Munición")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI ammoMaxText;
    [SerializeField] private TextMeshProUGUI weaponNameText;

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
        UpdateHealthBar();
        UpdateStaminaBar();
        UpdateAmmoText();
        UpdateCrosshair();
    }

    private void UpdateHealthBar()
    {
        float percentage = (float)playerHealth.CurrentHealth / playerHealth.MaxHealth;

        healthFill.fillAmount = percentage;
        healthPercentText.text = Mathf.RoundToInt(percentage * 100f) + "%";

        healthFill.color = percentage > 0.5f
            ? Color.Lerp(healthColorHalf, healthColorFull, (percentage - 0.5f) / 0.5f)
            : Color.Lerp(healthColorLow, healthColorHalf, percentage / 0.5f);
    }

    private void UpdateStaminaBar()
    {
        staminaFill.fillAmount = playerMovement.CurrentStamina / playerMovement.MaxStamina;
    }

    private void UpdateAmmoText()
    {
        ammoText.text = weapon.CurrentAmmo.ToString();
        ammoMaxText.text = "/ " + weapon.MaxAmmo;
        weaponNameText.text = weapon.WeaponName.ToUpper();
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