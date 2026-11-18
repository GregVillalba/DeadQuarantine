using System;
using UnityEngine;

[Serializable]
public sealed class WeaponHandlingStatistics
{
    [Min(.03f)] public float adsSeconds = .24f;
    [Min(0)] public float sprintToFireSeconds = .18f;
    [Min(0)] public float verticalRecoilDegrees = 1.35f;
    [Min(0)] public float horizontalRecoilDegrees = .35f;
    [Header("Patrón de ráfaga (multiplicadores de la patada por bala)")]
    [Range(.5f, 2f)] public float firstShotRecoilMultiplier = 1.15f;
    [Range(1f, 2f)] public float sustainedRecoilMultiplier = 1.3f;
    [Range(2, 20)] public int recoilRampShots = 8;
    [Range(-1, 1)] public float horizontalRecoilBias = .15f;
    [Min(.02f)] public float recoilRecoverySeconds = .2f;
    [Min(0)] public float aimSwayDegrees = .12f;
    [Range(.1f, 1.5f)] public float movement = 1f;
    [Range(.1f, 1f)] public float adsMovement = .65f;
}

public struct WeaponStatistics
{
    public float damage, roundsPerMinute, hipSpread, verticalRecoil, horizontalRecoil;
    public float recoilRecovery, aimSway, adsSeconds, sprintToFire, movement, adsMovement;
    public float reloadSeconds, range, fullDamageDistance, minimumDamageDistance;
    public float intermediateMultiplier, minimumMultiplier;
    public int magazine, reserve, pellets;

    public float DamageAt(float distance)
    {
        float scale = distance <= fullDamageDistance ? 1f :
            distance < minimumDamageDistance ? intermediateMultiplier : minimumMultiplier;
        if (distance > range || damage <= 0) return 0;
        int count = Mathf.Max(1, pellets);
        return Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Clamp01(scale) / count)) * count;
    }

    // Increasing every rating represents an advantage. Numeric details retain the real units.
    public float Rating(int category) => Mathf.Clamp01(category switch
    {
        0 => damage / 220f,
        1 => roundsPerMinute / 1200f,
        2 => .65f / (1f + hipSpread / 3f) + .35f / (1f + aimSway / .2f),
        3 => (movement / 1.2f + .18f / Mathf.Max(.03f, adsSeconds) + adsMovement) / 3f,
        4 => (fullDamageDistance / 140f + minimumDamageDistance / 220f) * .5f,
        _ => 1f / (1f + (verticalRecoil + horizontalRecoil) / 3f + recoilRecovery)
    }) * 100f;

    public string Details =>
        $"DAÑO 0–{fullDamageDistance:0.#} m  {DamageAt(0):0.#}\n" +
        $"{fullDamageDistance:0.#}–{minimumDamageDistance:0.#} m  {DamageAt(fullDamageDistance + .001f):0.#} · " +
        $"{minimumDamageDistance:0.#}–{range:0.#} m  {DamageAt(minimumDamageDistance):0.#}\n" +
        $"ADS {adsSeconds * 1000:0} ms · SPRINT {sprintToFire * 1000:0} ms\n" +
        $"RETROCESO V {verticalRecoil:0.##}° / H ±{horizontalRecoil:0.##}°\n" +
        $"RECUPERACIÓN {recoilRecovery * 1000:0} ms · OSCILACIÓN ±{aimSway:0.##}°\n" +
        $"CADERA {hipSpread:0.##}° · MOVIMIENTO ×{movement:0.##} / ADS ×{movement * adsMovement:0.##}\n" +
        $"RECARGA {reloadSeconds:0.##} s · MUNICIÓN {magazine} / {reserve}\n" +
        $"CADENCIA {roundsPerMinute:0} /min · ALCANCE {range:0.#} m";
}

public partial class Weapon
{
    [Header("Manejo y retroceso")]
    [SerializeField] private WeaponHandlingStatistics handling = new WeaponHandlingStatistics();
    [Range(0, 1)] [SerializeField] private float intermediateDamageMultiplier = .75f;
    float sprintFireReadyAt;

    public WeaponStatistics BaseStatistics => Statistics(default, null, false);
    public WeaponStatistics EffectiveStatistics =>
        Statistics(RuntimeAttachments ? RuntimeAttachments.ApprovedChoice : default,
            RuntimeAttachments ? RuntimeAttachments.Catalog : null,
            RuntimeAttachments && RuntimeAttachments.UsesPvPvEModifiers,
            RuntimeAttachments && RuntimeAttachments.IsBraced);

    public WeaponStatistics Statistics(WeaponAttachmentChoice choice, WeaponAttachmentCatalog catalog,
        bool applyModifiers = true, bool braced = false)
    {
        float Modifier(Func<WeaponAttachmentCatalog.Entry, float> get, bool conditional = false) =>
            applyModifiers && catalog ? catalog.Modifier(choice, get, braced, conditional) : 1f;
        float reach = Mathf.Clamp(Modifier(e => e.range), .1f, 3f);
        float cycle = usesBurstFire ? (Mathf.Max(1, burstCount) - 1) * burstShotInterval + burstCooldown : fireRate;
        float reload = ReloadStatisticsSeconds();
        return new WeaponStatistics {
            damage = Mathf.Max(0, damage * Modifier(e => e.damage, true)),
            roundsPerMinute = 60f * (usesBurstFire ? Mathf.Max(1, burstCount) : 1) / Mathf.Max(.01f, cycle),
            hipSpread = Mathf.Max(0, spreadIdle + (firesMultiplePellets ? pelletSpreadAngle : 0)) * Modifier(e => e.spread),
            verticalRecoil = Mathf.Max(0, handling.verticalRecoilDegrees * Modifier(e => e.recoil * e.verticalRecoil, true)),
            horizontalRecoil = Mathf.Max(0, handling.horizontalRecoilDegrees * Modifier(e => e.recoil * e.horizontalRecoil, true)),
            recoilRecovery = Mathf.Max(.02f, handling.recoilRecoverySeconds),
            aimSway = Mathf.Max(0, handling.aimSwayDegrees * Modifier(e => e.adsSway, true)),
            adsSeconds = Mathf.Max(.03f, handling.adsSeconds * Modifier(e => e.adsDuration)),
            sprintToFire = Mathf.Max(0, handling.sprintToFireSeconds),
            movement = Mathf.Clamp(handling.movement * Modifier(e => e.mobility), .1f, 1.5f),
            adsMovement = Mathf.Clamp(handling.adsMovement * Modifier(e => e.adsMovement), .1f, 1f),
            reloadSeconds = reload, magazine = maxAmmo, reserve = reserveAmmoCapacity,
            pellets = firesMultiplePellets ? Mathf.Max(1, pelletsPerShot) : 1,
            range = Mathf.Max(0, range),
            fullDamageDistance = Mathf.Max(0, damageFalloffStart * reach),
            minimumDamageDistance = Mathf.Max(damageFalloffStart, damageFalloffEnd) * reach,
            intermediateMultiplier = Mathf.Clamp01(intermediateDamageMultiplier),
            minimumMultiplier = Mathf.Min(Mathf.Clamp01(intermediateDamageMultiplier), Mathf.Clamp01(minDamageMultiplier))
        };
    }

    float ReloadStatisticsSeconds()
    {
        if (enforceReloadDuration || !usesMultiPartReload)
            return Mathf.Max(.05f, reloadTime);
        var switcher = GetComponentInParent<WeaponSwitcher>();
        return switcher ? switcher.ReloadCycleSeconds(this, reloadTime) : Mathf.Max(.05f, reloadTime);
    }

    public float DamageMultiplierAt(float distance, float distanceMultiplier = 1f)
    {
        float actualDistance = Mathf.Max(0, distance) / Mathf.Clamp(distanceMultiplier, .1f, 3f);
        return actualDistance <= damageFalloffStart ? 1f : actualDistance < damageFalloffEnd ?
            Mathf.Clamp01(intermediateDamageMultiplier) :
            Mathf.Min(Mathf.Clamp01(intermediateDamageMultiplier), Mathf.Clamp01(minDamageMultiplier));
    }

    public Vector2 EffectiveRecoilDegrees {
        get {
            var axes = AttachmentRecoilAxes;
            return new Vector2(Mathf.Max(0, handling.verticalRecoilDegrees * AttachmentRecoilMultiplier * axes.x),
                Mathf.Max(0, handling.horizontalRecoilDegrees * AttachmentRecoilMultiplier * axes.y));
        }
    }
    public float EffectiveAdsSeconds => Mathf.Max(.03f, handling.adsSeconds * AttachmentAdsDuration);
    public float MovementStatisticsMultiplier => Mathf.Clamp(handling.movement *
        (RuntimeAttachments ? RuntimeAttachments.MobilityMultiplier : 1f), .1f, 1.5f) *
        (IsAiming ? Mathf.Clamp(handling.adsMovement * (RuntimeAttachments ? RuntimeAttachments.AdsMovementMultiplier : 1f), .1f, 1f) : 1f);
    public float RecoilRecoverySeconds => Mathf.Max(.02f, handling.recoilRecoverySeconds);
    public float AimSwayDegrees => IsAiming ? Mathf.Max(0, handling.aimSwayDegrees * AttachmentSway) : 0f;
    public float SprintToFireSeconds => Mathf.Max(0, handling.sprintToFireSeconds);
}
