using System;
using UnityEngine;

/// <summary>
/// Un accesorio posible de un arma (una mira, un cargador, un grip...).
/// </summary>
[Serializable]
public class WeaponAttachmentOption
{
    [Tooltip("Nombre para reconocerlo en el Inspector (ej.: Mira holográfica).")]
    public string name = "";

    [Tooltip("Objeto 3D del accesorio dentro del modelo del arma (opcional). Solo está activo mientras está elegido.")]
    public GameObject model;

    [Tooltip("Sprite del accesorio para el icono del HUD (opcional).")]
    public Sprite sprite;
}

/// <summary>
/// Accesorios que tiene puestos un arma: mira, cargador, láser, grip y silenciador.
/// Va en el modelo del arma, dentro del objeto Weapon Root del slot en WeaponSwitcher (en la raíz o en cualquier hijo).
///
/// Es el equivalente del WeaponAttachmentManager del proyecto original, sin sus efectos de juego:
/// guarda QUÉ accesorio hay elegido en cada categoría, activa el objeto 3D correspondiente y avisa
/// (evento Changed) para que el icono del HUD (ImageWeapon) se actualice solo.
///
/// Para cambiar un accesorio mientras se juega (comprarlo en la tienda, un power-up...):
///     weaponAttachments.SetScope(1);   // la segunda mira de la lista
///     weaponAttachments.SetGrip(-1);   // sacar el grip
/// </summary>
public class WeaponAttachments : MonoBehaviour
{
    [Header("Icono del HUD")]
    [Tooltip("Sprite del cuerpo del arma (opcional). Si queda vacío, el icono conserva el que ya tiene su imagen 'Weapon'.")]
    [SerializeField] private Sprite bodySprite;

    [Header("Mira por defecto")]
    [Tooltip("Mira que el arma trae siempre (ej.: la mira metálica). En el icono tiene su propia imagen. " +
             "Dejala vacía si el arma no trae ninguna.")]
    [SerializeField] private WeaponAttachmentOption scopeDefault = new WeaponAttachmentOption();
    [Tooltip("Oculta el objeto 3D de la mira por defecto mientras haya otra mira puesta.")]
    [SerializeField] private bool hideDefaultScopeModelWhenScoped = true;

    [Header("Accesorios (el número elige uno de la lista; -1 = ninguno)")]
    [SerializeField] private WeaponAttachmentOption[] scopes = new WeaponAttachmentOption[0];
    [SerializeField] private int scopeIndex = -1;

    [SerializeField] private WeaponAttachmentOption[] magazines = new WeaponAttachmentOption[0];
    [SerializeField] private int magazineIndex = -1;

    [SerializeField] private WeaponAttachmentOption[] lasers = new WeaponAttachmentOption[0];
    [SerializeField] private int laserIndex = -1;

    [SerializeField] private WeaponAttachmentOption[] grips = new WeaponAttachmentOption[0];
    [SerializeField] private int gripIndex = -1;

    [SerializeField] private WeaponAttachmentOption[] muzzles = new WeaponAttachmentOption[0];
    [SerializeField] private int muzzleIndex = -1;

    [Header("Modelo 3D")]
    [Tooltip("Al empezar y al cambiar un accesorio, activa solo los objetos 3D de los accesorios elegidos.")]
    [SerializeField] private bool applyToModel = true;

    /// <summary>Se dispara cuando cambia cualquier accesorio. Lo escucha el icono del HUD.</summary>
    public event Action Changed;

    // ---------- Lo que lee el icono ----------

    public Sprite BodySprite => bodySprite;
    public Sprite ScopeDefaultSprite => scopeDefault != null ? scopeDefault.sprite : null;
    public Sprite ScopeSprite => SpriteAt(scopes, scopeIndex);
    public Sprite MagazineSprite => SpriteAt(magazines, magazineIndex);
    public Sprite LaserSprite => SpriteAt(lasers, laserIndex);
    public Sprite GripSprite => SpriteAt(grips, gripIndex);
    public Sprite MuzzleSprite => SpriteAt(muzzles, muzzleIndex);

    public int ScopeIndex => scopeIndex;
    public int MagazineIndex => magazineIndex;
    public int LaserIndex => laserIndex;
    public int GripIndex => gripIndex;
    public int MuzzleIndex => muzzleIndex;

    // ---------- Cambiar accesorios ----------

    public void SetScope(int index) { if (Assign(ref scopeIndex, scopes, index)) NotifyChanged(); }
    public void SetMagazine(int index) { if (Assign(ref magazineIndex, magazines, index)) NotifyChanged(); }
    public void SetLaser(int index) { if (Assign(ref laserIndex, lasers, index)) NotifyChanged(); }
    public void SetGrip(int index) { if (Assign(ref gripIndex, grips, index)) NotifyChanged(); }
    public void SetMuzzle(int index) { if (Assign(ref muzzleIndex, muzzles, index)) NotifyChanged(); }

    private void Awake()
    {
        ApplyModels();
    }

    private void OnValidate()
    {
        scopeIndex = ClampIndex(scopeIndex, scopes);
        magazineIndex = ClampIndex(magazineIndex, magazines);
        laserIndex = ClampIndex(laserIndex, lasers);
        gripIndex = ClampIndex(gripIndex, grips);
        muzzleIndex = ClampIndex(muzzleIndex, muzzles);
    }

    /// <summary>Activa los objetos 3D de los accesorios elegidos y apaga el resto de su categoría.</summary>
    [ContextMenu("Aplicar accesorios al modelo")]
    public void ApplyModels()
    {
        if (!applyToModel)
            return;

        SetModelsActive(scopes, scopeIndex);
        SetModelsActive(magazines, magazineIndex);
        SetModelsActive(lasers, laserIndex);
        SetModelsActive(grips, gripIndex);
        SetModelsActive(muzzles, muzzleIndex);

        if (scopeDefault != null && scopeDefault.model != null)
        {
            bool extraScopeEquipped = OptionAt(scopes, scopeIndex) != null;
            scopeDefault.model.SetActive(!(hideDefaultScopeModelWhenScoped && extraScopeEquipped));
        }
    }

    private void NotifyChanged()
    {
        ApplyModels();
        Changed?.Invoke();
    }

    // ---------- Utilidades ----------

    private static WeaponAttachmentOption OptionAt(WeaponAttachmentOption[] list, int index)
    {
        if (list == null || index < 0 || index >= list.Length)
            return null;

        return list[index];
    }

    private static Sprite SpriteAt(WeaponAttachmentOption[] list, int index)
    {
        WeaponAttachmentOption option = OptionAt(list, index);
        return option != null ? option.sprite : null;
    }

    private static int ClampIndex(int index, WeaponAttachmentOption[] list)
    {
        int length = list != null ? list.Length : 0;
        return Mathf.Clamp(index, -1, length - 1);
    }

    private static bool Assign(ref int field, WeaponAttachmentOption[] list, int value)
    {
        int clamped = ClampIndex(value, list);

        if (clamped == field)
            return false;

        field = clamped;
        return true;
    }

    private static void SetModelsActive(WeaponAttachmentOption[] list, int selectedIndex)
    {
        if (list == null)
            return;

        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].model != null)
                list[i].model.SetActive(i == selectedIndex);
        }
    }
}