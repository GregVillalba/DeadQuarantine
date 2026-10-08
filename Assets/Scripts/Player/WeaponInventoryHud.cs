using UnityEngine;

/// <summary>
/// HUD de armas primaria / secundaria. Va en la raíz del prefab InventoryContainer (dentro de HUD_Canvas).
///
/// Casillero 1 = arma primaria, casillero 2 = arma secundaria. Cada casillero muestra el objeto de icono del
/// arma que le toca (campo Category de cada slot del WeaponSwitcher) y se marca el de la categoría que tenés
/// en la mano. Si todavía no tenés ninguna arma de esa categoría, queda vacío.
///
/// El texto de cada casillero es la tecla REAL de PlayerControls (acciones SelectPrimary / SelectSecondary):
/// si el jugador cambia la tecla, el texto cambia solo.
///
/// Todo se busca solo: el WeaponSwitcher del mismo jugador y los casilleros hijos (el primero que aparece
/// en la jerarquía es el primario, el segundo el secundario).
/// </summary>
public class WeaponInventoryHUD : MonoBehaviour
{
    [Header("Referencias (si están vacías se buscan solas)")]
    [SerializeField] private WeaponSwitcher switcher;
    [SerializeField] private WeaponsInventoryUISlot primarySlot;
    [SerializeField] private WeaponsInventoryUISlot secondarySlot;

    private bool subscribed;

    private void Awake()
    {
        if (primarySlot == null || secondarySlot == null)
        {
            WeaponsInventoryUISlot[] found = GetComponentsInChildren<WeaponsInventoryUISlot>(true);

            if (primarySlot == null && found.Length > 0)
                primarySlot = found[0];

            if (secondarySlot == null && found.Length > 1)
                secondarySlot = found[1];
        }
    }

    private void OnEnable()
    {
        if (switcher == null)
            switcher = transform.root.GetComponentInChildren<WeaponSwitcher>(true);

        if (switcher != null && !subscribed)
        {
            switcher.InventoryChanged += OnSwitcherChanged;
            switcher.BindingsChanged += OnSwitcherChanged;
            subscribed = true;
        }

        // El HUD se apaga y se prende con la pausa: al volver, se releen las teclas por si el jugador las cambió.
        if (switcher != null && switcher.IsReady)
            switcher.RefreshBindings();

        Refresh(true);
    }

    private void Start()
    {
        // El WeaponSwitcher termina de equipar el arma inicial en su Start.
        Refresh(true);
    }

    private void OnDisable()
    {
        if (switcher != null && subscribed)
        {
            switcher.InventoryChanged -= OnSwitcherChanged;
            switcher.BindingsChanged -= OnSwitcherChanged;
        }

        subscribed = false;
    }

    private void OnSwitcherChanged()
    {
        Refresh(false);
    }

    private void Refresh(bool instant)
    {
        if (switcher == null || !switcher.IsReady)
            return;

        UpdateSlot(primarySlot, WeaponSlotCategory.Primary, instant);
        UpdateSlot(secondarySlot, WeaponSlotCategory.Secondary, instant);
    }

    private void UpdateSlot(WeaponsInventoryUISlot slot, WeaponSlotCategory category, bool instant)
    {
        if (slot == null)
            return;

        int index = switcher.GetIndexForCategory(category);

        // Arma desbloqueada pero sin objeto de icono: el casillero muestra el indicador de "vacío" (un cuadrado).
        if (index >= 0 && switcher.GetIconObject(index) == null)
            Debug.LogWarning("[WeaponInventoryHUD] El slot " + index + " del WeaponSwitcher no tiene un Weapon Icon válido " +
                             "(tiene que ser un prefab de UI, no un Sprite ni vacío). Por eso el casillero muestra el cuadrado.", this);

        slot.SetIcon(
            index >= 0 ? switcher.GetIconObject(index) : null,
            index >= 0 ? switcher.GetAttachments(index) : null);
        slot.SetKeyLabel(switcher.GetKeyLabel(category));
        slot.SetSelected(switcher.SelectedCategory == category, instant);
    }
}