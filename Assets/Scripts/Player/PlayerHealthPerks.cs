using System;
using System.Linq;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerHealth
{
    [SerializeField] private PassivePerkCatalog perksCatalog;
    public PassivePerkCatalog PerksCatalog => perksCatalog;
    public readonly NetworkVariable<uint> Perks = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<double> SlideUntil = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public readonly NetworkVariable<Vector3> SlideDirection = new(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public event Action OnPerksChanged;
    double nextSlide;
    bool perksMatchEnded;
    Animator perkBodyAnimator; float bodySpeed; bool bodySpeedApplied;
    Vector3 observedPosition; double lastSprintMovement = -100;
    public bool TienePerk(PassivePerk perk) => (Perks.Value & PassivePerkCatalog.Bit(perk)) != 0;
    public float PerkReloadSpeed => TienePerk(PassivePerk.Gloves) && perksCatalog ? perksCatalog.reloadSpeed : 1;
    public float PerkSprintDrain => TienePerk(PassivePerk.Boots) && perksCatalog ? perksCatalog.sprintDrain : 1;
    public float PerkStaminaRegen => TienePerk(PassivePerk.Canteen) && perksCatalog ? perksCatalog.staminaRegen : 1;
    public float PerkSwitchSpeed => TienePerk(PassivePerk.Holster) && perksCatalog ? 1f / Mathf.Max(.01f, perksCatalog.switchDuration) : 1;
    bool EsJugadorPvPvE => PvPvETeamRoster.Active && PvPvETeamRoster.Active.IsSpawned && PvPvETeamRoster.Active.NetworkManager == NetworkManager && PvPvETeamRoster.Active.Contains(OwnerClientId);
    public bool Deslizando => IsSpawned && IsAlive && TienePerk(PassivePerk.Kneepads) && NetworkManager.ServerTime.Time < SlideUntil.Value;

    public bool ComprarPerkServidor(string id)
    {
        if (!IsServer || !IsSpawned || !IsAlive || perksMatchEnded || EsJugadorPvPvE || !perksCatalog) return false;
        var entry = perksCatalog.Find(id);
        if (entry == null || TienePerk(entry.perk)) return false;
        var vendor = FindObjectsByType<NPCWeaponVendor>(FindObjectsSortMode.None).FirstOrDefault(v =>
            Vector3.Distance(v.transform.position, transform.position) <= perksCatalog.merchantDistance &&
            v.Offers.Any(o => o.weaponId == id && o.perk == entry.perk && o.disponible && DisponibilidadTienda.SeMuestraEnPartidaActual(o)));
        if (!vendor || !vendor.PuedeComerciar) return false;
        var offer = vendor.Offers.First(o => o.weaponId == id && o.perk == entry.perk);
        var resources = GetComponent<PlayerRecursos>();
        if (!resources || !resources.GastarRecursos(offer.Precio)) return false;
        Perks.Value |= PassivePerkCatalog.Bit(entry.perk);
        return true;
    }
    public bool AplicarPerksDeClaseServidor(PvPvEClassDefinition selectedClass)
    {
        if (!IsServer || !IsSpawned || !EsJugadorPvPvE || perksMatchEnded || PvPvETeamRoster.Active.Closing.Value) return false;
        var catalog = PvPvETeamRoster.Active.Configuration.Loadouts;
        if (!catalog || catalog.perks != perksCatalog || !catalog.Validate(selectedClass, out _) ||
            !catalog.TryPerkMask(selectedClass, out var mask)) return false;
        Perks.Value = mask;
        if (!TienePerk(PassivePerk.Kneepads)) SlideUntil.Value = 0;
        return true;
    }
    public void LimpiarPerksServidor(bool endMatch = false)
    {
        if (!IsServer) return;
        Perks.Value = 0; SlideUntil.Value = 0; nextSlide = 0; lastSprintMovement = -100;
        LimpiarWildcardsServidor();
        if (endMatch) perksMatchEnded = true;
    }
    public static void FinPartidaPerksServidor()
    {
        foreach (var health in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None)) if (health.IsSpawned && health.IsServer) health.LimpiarPerksServidor(true);
    }
    void InicializarPerksServidor() { perksMatchEnded = false; LimpiarPerksServidor(); }
    void SuscribirPerks() { Perks.OnValueChanged += PerksChanged; State.OnValueChanged += PerkLifeChanged; }
    void DesuscribirPerks() { Perks.OnValueChanged -= PerksChanged; State.OnValueChanged -= PerkLifeChanged; RestorePerkBodySpeed(); }
    void PerksChanged(uint before, uint after) { if (!TienePerk(PassivePerk.Holster)) GetComponentInChildren<WeaponSwitcher>(true)?.EndPerkSwitchSpeed(); OnPerksChanged?.Invoke(); OnInventarioChanged?.Invoke(); }
    void PerkLifeChanged(PlayerState before, PlayerState after)
    {
        if (!IsServer) return;
        if (after == PlayerState.Alive) { perksMatchEnded = false; return; }
        SlideUntil.Value = 0;
        if (EsJugadorPvPvE || after == PlayerState.Dead || after == PlayerState.Spectating) LimpiarPerksServidor();
    }
    [ServerRpc]
    public void SolicitarDeslizamientoServerRpc(Vector3 direction)
    {
        if (!IsAlive || perksMatchEnded || !perksCatalog || !TienePerk(PassivePerk.Kneepads) || NetworkManager.ServerTime.Time < nextSlide ||
            !float.IsFinite(direction.x) || !float.IsFinite(direction.z)) return;
        var movement = GetComponent<PlayerMovement>();
        if (!movement || NetworkManager.ServerTime.Time - lastSprintMovement > .35) return;
        direction.y = 0; if (direction.sqrMagnitude < .5f || direction.sqrMagnitude > 1.5f) return;
        bool grounded = Physics.RaycastAll(transform.position + Vector3.up * .2f, Vector3.down, .7f, ~0, QueryTriggerInteraction.Ignore)
            .Any(h => h.collider.transform.root != transform.root && Vector3.Angle(h.normal, Vector3.up) < 50);
        if (!grounded) return;
        SlideDirection.Value = direction.normalized;
        SlideUntil.Value = NetworkManager.ServerTime.Time + perksCatalog.slideDuration;
        nextSlide = NetworkManager.ServerTime.Time + perksCatalog.slideCooldown;
    }
    void LateUpdate()
    {
        if (!IsSpawned) return;
        ActualizarArmasDeClase();
        if (IsServer)
        {
            var delta = transform.position - observedPosition; delta.y = 0;
            var movement = GetComponent<PlayerMovement>();
            if (movement && delta.magnitude > .01f && delta.magnitude < 2 && delta.magnitude / Mathf.Max(.001f, Time.deltaTime) >= movement.SprintSpeed * .8f) lastSprintMovement = NetworkManager.ServerTime.Time;
            observedPosition = transform.position;
        }
        if (!perkBodyAnimator) perkBodyAnimator = GetComponent<NetworkWeaponPresentation>()?.BodyAnimator;
        if (!perkBodyAnimator || !perkBodyAnimator.runtimeAnimatorController) return;
        bool accelerated = PerkSwitchSpeed != 1 && (perkBodyAnimator.GetBool("TP_Holstering") || perkBodyAnimator.GetBool("TP_Unholstering"));
        if (accelerated) { if (!bodySpeedApplied) { bodySpeed = perkBodyAnimator.speed; bodySpeedApplied = true; } perkBodyAnimator.speed = bodySpeed * PerkSwitchSpeed; }
        else RestorePerkBodySpeed();
    }
    void RestorePerkBodySpeed() { if (bodySpeedApplied && perkBodyAnimator) perkBodyAnimator.speed = bodySpeed; bodySpeedApplied = false; }
}
