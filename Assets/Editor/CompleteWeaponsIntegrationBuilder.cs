#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Integrador idempotente de las armas C.A. 5-10 para Dead Quarantine.
///
/// No modifica Handgun / Rifle / Escopeta existentes. Genera assets nuevos bajo
/// weapon-assets:/NewWeapons y agrega/actualiza únicamente los IDs:
/// AutoPistol, SemiRifle, BurstRifle, P90, Vector y MAC10.
///
/// El integrador toma Player.prefab y Mercader.prefab como fuente de verdad para
/// referencias compartidas (cámaras, recoil, trail, muzzle FX, audio mixer, casing, etc.).
/// </summary>
public static partial class CompleteWeaponsIntegrationBuilder
{
    private const string Root = "weapon-assets:/NewWeapons";
    private const string ControllersDir = Root + "/Controllers";
    private const string HudDir = Root + "/HUD";
    private const string PresentationDir = Root + "/ThirdPerson";
    private const string GeneratedShopDir = Root + "/Tienda";
    private const string BackupDir = Root + "/Backup";
    private const string ReportPath = Root + "/IntegrationReport.txt";

    private static readonly string[] ManagedIds =
    {
        "AutoPistol", "SemiRifle", "BurstRifle", "P90", "Vector", "MAC10"
    };

    private enum Family { Handgun, AR, SMG }

    private sealed class Spec
    {
        public string Id;
        public string DisplayName;
        public string Model;
        public Family Family;
        public int Damage;
        public float FireInterval;
        public float ReloadTime;
        public float Range;
        public int Magazine;
        public int Reserve;
        public bool Automatic;
        public bool Burst;
        public int BurstCount;
        public float BurstShotInterval;
        public float BurstCooldown;
        public string HudSprite;
        public string ScopeSprite;
        public string MagazineSprite;
        public string ScopeModel;
        public string MagazineModel;
        public string ReloadSound;
        public string ReloadEmptySound;
        public string FireSound;
        public int LegacyCost;
        public int Cans;
        public int Water;
        public int Fuel;
        public string Description;

        // Se llenan durante ResolveAssets.
        public GameObject ModelAsset;
        public GameObject ScopeModelAsset;
        public GameObject MagazineModelAsset;
        public Sprite HudSpriteAsset;
        public Sprite ScopeSpriteAsset;
        public Sprite MagazineSpriteAsset;
        public AudioClip ReloadSoundAsset;
        public AudioClip ReloadEmptySoundAsset;
        public AudioClip FireSoundAsset;
        public ClipSet FP;
        public ClipSet TP;
        public AnimationClip PartsFireFP;
        public AnimationClip PartsReloadFP;
        public AnimationClip PartsReloadEmptyFP;
        public AnimationClip PartsFireTP;
        public AnimationClip PartsReloadTP;
        public AnimationClip PartsReloadEmptyTP;
    }

    private sealed class ClipSet
    {
        public AnimationClip AimPose;
        public AnimationClip Fire;
        public AnimationClip AimFire;
        public AnimationClip Idle;
        public AnimationClip Reload;
        public AnimationClip ReloadEmpty;
        public AnimationClip AimReload;
        public AnimationClip AimReloadEmpty;
        public AnimationClip Holster;
        public AnimationClip Unholster;
        public AnimationClip Inspect;
        public AnimationClip Run;
        public AnimationClip Jog;
        public AnimationClip Sprint;
        public AnimationClip SprintImpulse;
        public AnimationClip CrouchPose;
        public AnimationClip CrouchAimPose;
    }

    private sealed class SlotTemplate
    {
        public string Id;
        public GameObject Root;
        public Weapon Weapon;
        public GameObject Icon;
        public GameObject Casing;
        public Transform Eject;
        public AnimatorOverrideController FPOverride;
        public AnimatorOverrideController TPOverride;
    }

    private sealed class GeneratedWeapon
    {
        public Spec Spec;
        public GameObject Root;
        public Weapon Weapon;
        public AnimatorOverrideController FPOverride;
        public AnimatorOverrideController TPOverride;
        public GameObject HudIconPrefab;
        public GameObject Casing;
        public Transform Eject;
        public GameObject TPPrefab;
        public Vector3 TPGripPosition;
        public Vector3 TPGripRotation;
        public Sprite ShopSprite;
    }

    private static readonly List<string> report = new List<string>();
    private static readonly List<string> warnings = new List<string>();

    [MenuItem("Game/Dead Quarantine/Armas/Restaurar backup previo a armas nuevas")]
    public static void RestoreBackups()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Restaurar armas", "Salí del Play Mode antes de restaurar.", "OK");
            return;
        }

        string playerPath = FindPrefabPathWithComponent<WeaponSwitcher>("Player");
        string merchantPath = FindPrefabPathWithComponent<NPCWeaponVendor>("Mercader");
        string playerBackup = BackupDir + "/Player_ANTES_ARMAS.prefab";
        string merchantBackup = BackupDir + "/Mercader_ANTES_ARMAS.prefab";

        bool restored = false;
        if (!string.IsNullOrEmpty(playerPath) && AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(playerBackup)) != null)
        {
            File.Copy(WeaponAssetLocations.Resolve(AssetPathToAbsolute(playerBackup)),WeaponAssetLocations.Resolve( AssetPathToAbsolute(playerPath)), true);
            AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(playerPath), ImportAssetOptions.ForceUpdate);
            restored = true;
        }
        if (!string.IsNullOrEmpty(merchantPath) && AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(merchantBackup)) != null)
        {
            File.Copy(WeaponAssetLocations.Resolve(AssetPathToAbsolute(merchantBackup)),WeaponAssetLocations.Resolve( AssetPathToAbsolute(merchantPath)), true);
            AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(merchantPath), ImportAssetOptions.ForceUpdate);
            restored = true;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Restaurar armas",
            restored ? "Se restauraron los prefabs desde el backup previo a la integración." : "No encontré backups para restaurar.",
            "OK");
    }

    [MenuItem("Game/Dead Quarantine/Armas/REPARAR armas nuevas (restaurar + reconstruir)")]
    public static void RepairAndReintegrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Reparar armas", "Salí del Play Mode antes de reparar.", "OK");
            return;
        }

        string playerPath = FindPrefabPathWithComponent<WeaponSwitcher>("Player");
        string merchantPath = FindPrefabPathWithComponent<NPCWeaponVendor>("Mercader");
        string playerBackup = BackupDir + "/Player_ANTES_ARMAS.prefab";
        string merchantBackup = BackupDir + "/Mercader_ANTES_ARMAS.prefab";

        if (string.IsNullOrEmpty(playerPath) || string.IsNullOrEmpty(merchantPath) ||
            AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(playerBackup)) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(merchantBackup)) == null)
        {
            EditorUtility.DisplayDialog(
                "Reparar armas",
                "No encontré los backups limpios de Player.prefab y Mercader.prefab. No voy a reparar encima de prefabs contaminados.",
                "OK");
            return;
        }

        // Volver exactamente al estado previo a la primera integración.
        File.Copy(WeaponAssetLocations.Resolve(AssetPathToAbsolute(playerBackup)),WeaponAssetLocations.Resolve( AssetPathToAbsolute(playerPath)), true);
        File.Copy(WeaponAssetLocations.Resolve(AssetPathToAbsolute(merchantBackup)),WeaponAssetLocations.Resolve( AssetPathToAbsolute(merchantPath)), true);
        AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(playerPath), ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(merchantPath), ImportAssetOptions.ForceUpdate);

        // Borrar solamente assets generados por este integrador. Se conserva Backup.
        foreach (string dir in new[] { ControllersDir, HudDir, PresentationDir, GeneratedShopDir })
        {
            if (AssetDatabase.IsValidFolder(WeaponAssetLocations.Resolve(dir)))
                AssetDatabase.DeleteAsset(WeaponAssetLocations.Resolve(dir));
        }
        AssetDatabase.Refresh();

        Integrate();
    }

    [MenuItem("Game/Dead Quarantine/Armas/Integrar armas nuevas (COMPLETO)")]
    public static void Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Armas nuevas", "Salí del Play Mode antes de ejecutar el integrador.", "OK");
            return;
        }

        report.Clear();
        warnings.Clear();

        try
        {
            EnsureFolders();
            report.Add("DEAD QUARANTINE - INTEGRACIÓN ARMAS C.A. 5-10 - VISUAL FIX");
            report.Add("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.Add("");

            Spec[] specs = CreateSpecs();
            ResolveAssets(specs);

            string playerPath = FindPrefabPathWithComponent<WeaponSwitcher>("Player");
            string merchantPath = FindPrefabPathWithComponent<NPCWeaponVendor>("Mercader");

            if (string.IsNullOrEmpty(playerPath))
                throw new Exception("No encontré Player.prefab con WeaponSwitcher.");
            if (string.IsNullOrEmpty(merchantPath))
                throw new Exception("No encontré Mercader.prefab con NPCWeaponVendor.");

            BackupOnce(playerPath, BackupDir + "/Player_ANTES_ARMAS.prefab");
            BackupOnce(merchantPath, BackupDir + "/Mercader_ANTES_ARMAS.prefab");

            IntegratePlayer(playerPath, specs);
            IntegrateMerchant(merchantPath, specs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.Add("");
            report.Add("RESULTADO: integración completada.");
            if (warnings.Count > 0)
            {
                report.Add("");
                report.Add("ADVERTENCIAS:");
                foreach (string w in warnings)
                    report.Add("- " + w);
            }

            WriteReport();

            Debug.Log("[NewWeapons] Integración completa (visual fix). Revisá: " + ReportPath);
            EditorUtility.DisplayDialog(
                "Armas integradas",
                "Se integraron AutoPistol, SemiRifle, BurstRifle, P90, Vector y MAC10.\n\n" +
                "Se actualizaron Player.prefab y Mercader.prefab, se generaron overrides, controladores de partes, " +
                "prefabs TP, iconos HUD y tarjetas de tienda.\n\n" +
                "Revisá el reporte en:\n" + ReportPath,
                "OK");
        }
        catch (Exception ex)
        {
            report.Add("");
            report.Add("ERROR: " + ex);
            WriteReport();
            Debug.LogError("[NewWeapons] Integración cancelada: " + ex);
            EditorUtility.DisplayDialog(
                "No se pudo integrar",
                "No se modificó intencionalmente nada después del punto del error.\n\n" + ex.Message +
                "\n\nDetalle: " + ReportPath,
                "OK");
        }
    }

    // =====================================================================
    // ESPECIFICACIONES
    // =====================================================================

    private static Spec[] CreateSpecs()
    {
        // Los C.A. no indican precios. Estos valores son solo precios de diseño iniciales y quedan
        // editables en Mercader.prefab. No afectan estadísticas ni lógica del arma.
        return new[]
        {
            new Spec {
                Id="AutoPistol", DisplayName="Pistola automática", Model="SK_Handgun_04", Family=Family.Handgun,
                Damage=18, FireInterval=0.09f, ReloadTime=1.8f, Range=55f, Magazine=20, Reserve=120,
                Automatic=true, Burst=false,
                HudSprite="T_HUD_Handgun_04_D", ScopeSprite="T_HUD_Handgun_04_Scope_Default_D",
                MagazineSprite="T_HUD_Handgun_04_Magazine_Default_D", ScopeModel="SM_Handgun_04_Scope_Default",
                MagazineModel="SM_Handgun_04_Magazine_Default", ReloadSound="S_WEP_Handgun_Reload",
                ReloadEmptySound="S_WEP_Handgun_Reload_Empty", FireSound="S_WEP_Fire_001",
                LegacyCost=8000, Cans=2, Water=2, Fuel=0,
                Description="Pistola automática compacta. Alta cadencia y buena movilidad a corta distancia."
            },
            new Spec {
                Id="SemiRifle", DisplayName="Rifle semiautomático", Model="SK_AR_03", Family=Family.AR,
                Damage=70, FireInterval=0.28f, ReloadTime=2.5f, Range=180f, Magazine=10, Reserve=60,
                Automatic=false, Burst=false,
                HudSprite="T_HUD_AR_03_D", ScopeSprite="T_HUD_AR_03_Scope_Default_D",
                MagazineSprite="T_HUD_AR_03_Magazine_Default_D", ScopeModel="SM_AR_03_Scope_Default",
                MagazineModel="SM_AR_03_Magazine_Default", ReloadSound="S_WEP_AR_03_Reload",
                ReloadEmptySound="S_WEP_AR_03_Reload_Empty", FireSound="S_WEP_Fire_004",
                LegacyCost=15000, Cans=3, Water=3, Fuel=1,
                Description="Rifle semiautomático de alto daño y gran alcance. Premia los disparos precisos."
            },
            new Spec {
                Id="BurstRifle", DisplayName="Rifle de ráfagas", Model="SK_AR_02", Family=Family.AR,
                Damage=30, FireInterval=0.25f, ReloadTime=2.3f, Range=140f, Magazine=30, Reserve=120,
                Automatic=false, Burst=true, BurstCount=3, BurstShotInterval=0.07f, BurstCooldown=0.25f,
                HudSprite="T_HUD_AR_02_D", ScopeSprite="T_HUD_AR_02_Scope_Default_D",
                MagazineSprite="T_HUD_AR_02_Magazine_Default_D", ScopeModel="SM_AR_02_Scope_Default",
                MagazineModel="SM_AR_02_Magazine_Default", ReloadSound="S_WEP_AR_02_Reload",
                ReloadEmptySound="S_WEP_AR_02_Reload_Empty", FireSound="S_WEP_Fire_003",
                LegacyCost=16000, Cans=4, Water=2, Fuel=1,
                Description="Rifle de tres disparos por pulsación. Cada ráfaga combina control y daño sostenido."
            },
            new Spec {
                Id="P90", DisplayName="P90", Model="SK_SMG_04", Family=Family.SMG,
                Damage=20, FireInterval=0.064f, ReloadTime=2.6f, Range=85f, Magazine=50, Reserve=200,
                Automatic=true, Burst=false,
                HudSprite="T_HUD_SMG_04_D", ScopeSprite="T_HUD_SMG_04_Scope_Default_D",
                MagazineSprite="T_HUD_SMG_04_Magazine_Default_D", ScopeModel="SM_SMG_04_Scope_Default",
                MagazineModel="SM_SMG_04_Magazine_Default", ReloadSound="S_WEP_SMG_04_Reload",
                ReloadEmptySound="S_WEP_SMG_04_Reload_Empty", FireSound="S_WEP_Fire_002",
                LegacyCost=17000, Cans=4, Water=3, Fuel=1,
                Description="Subfusil P90 con cargador de 50 balas y cadencia muy alta. Ideal para mantener presión."
            },
            new Spec {
                Id="Vector", DisplayName="Vector", Model="SK_SMG_01", Family=Family.SMG,
                Damage=18, FireInterval=0.055f, ReloadTime=2.2f, Range=55f, Magazine=25, Reserve=150,
                Automatic=true, Burst=false,
                HudSprite="T_HUD_SMG_01_D", ScopeSprite="T_HUD_SMG_01_Scope_Default_D",
                MagazineSprite="T_HUD_SMG_01_Magazine_Default_D", ScopeModel="SM_SMG_01_Scope_Default",
                MagazineModel="SM_SMG_01_Magazine_Default", ReloadSound="S_WEP_SMG_01_Reload",
                ReloadEmptySound="S_WEP_SMG_01_Reload_Empty", FireSound="S_WEP_Fire_005",
                LegacyCost=17000, Cans=4, Water=2, Fuel=1,
                Description="Vector de altísima cadencia para combate cercano. Vacía el cargador con rapidez."
            },
            new Spec {
                Id="MAC10", DisplayName="MAC-10", Model="SK_SMG_03", Family=Family.SMG,
                Damage=17, FireInterval=0.06f, ReloadTime=2.0f, Range=45f, Magazine=30, Reserve=180,
                Automatic=true, Burst=false,
                HudSprite="T_HUD_SMG_03_D", ScopeSprite="T_HUD_SMG_03_Scope_Default_D",
                MagazineSprite="T_HUD_SMG_03_Magazine_Default_D", ScopeModel="SM_SMG_03_Scope_Default",
                MagazineModel="SM_SMG_03_Magazine_Default", ReloadSound="S_WEP_SMG_03_Reload",
                ReloadEmptySound="S_WEP_SMG_03_Reload_Empty", FireSound="S_WEP_Fire_007",
                LegacyCost=12000, Cans=3, Water=2, Fuel=0,
                Description="Subfusil compacto de corto alcance y elevada cadencia. El modelo SMG_03 es el equivalente visual disponible en el pack."
            }
        };
    }

    // =====================================================================
    // RESOLUCIÓN Y VALIDACIÓN DE ASSETS
    // =====================================================================

    private static void ResolveAssets(IEnumerable<Spec> specs)
    {
        foreach (Spec s in specs)
        {
            s.ModelAsset = RequireAsset<GameObject>(s.Model);
            s.ScopeModelAsset = RequireAsset<GameObject>(s.ScopeModel);
            s.MagazineModelAsset = RequireAsset<GameObject>(s.MagazineModel);
            s.HudSpriteAsset = RequireAsset<Sprite>(s.HudSprite);
            s.ScopeSpriteAsset = RequireAsset<Sprite>(s.ScopeSprite);
            s.MagazineSpriteAsset = RequireAsset<Sprite>(s.MagazineSprite);
            s.ReloadSoundAsset = RequireAsset<AudioClip>(s.ReloadSound);
            s.ReloadEmptySoundAsset = RequireAsset<AudioClip>(s.ReloadEmptySound);
            s.FireSoundAsset = RequireAsset<AudioClip>(s.FireSound);

            string modelPath = AssetDatabase.GetAssetPath(s.ModelAsset);
            EnsureEmbeddedModelClips(modelPath);

            s.FP = ResolveFPClips(s);
            s.TP = ResolveTPClips(s);
            ResolvePartsClips(s, modelPath);

            ValidateRequiredClips(s);
            report.Add($"Assets OK: {s.Id} -> {s.Model}");
        }

        if (warnings.Count > 0)
            foreach (string w in warnings)
                Debug.LogWarning("[NewWeapons] " + w);
    }

    private static ClipSet ResolveFPClips(Spec s)
    {
        ClipSet c = new ClipSet();
        if (s.Family == Family.Handgun)
        {
            c.AimPose = RequireClip("A_FP_PCH_Handgun_Aim_Pose");
            c.Fire = RequireClip("A_FP_PCH_Handgun_Fire");
            c.AimFire = RequireClip("A_FP_PCH_Handgun_Aim_Fire");
            c.Idle = RequireClip("A_FP_PCH_Handgun_Idle_Pose");
            c.Reload = RequireClip("A_FP_PCH_Handgun_Reload");
            c.ReloadEmpty = RequireClip("A_FP_PCH_Handgun_Reload_Empty");
            c.AimReload = c.Reload;
            c.AimReloadEmpty = c.ReloadEmpty;
            c.Holster = RequireClip("A_FP_PCH_Handgun_Holster");
            c.Unholster = RequireClip("A_FP_PCH_Handgun_Unholster");
            c.Inspect = RequireClip("A_FP_PCH_Handgun_Inspect");
            c.Run = RequireClip("A_FP_PCH_Handgun_Run_01_Additive");
            return c;
        }

        string family = s.Family == Family.AR ? "AR" : "SMG";
        string code = s.Model.Replace("SK_", ""); // AR_02 / SMG_04
        c.AimPose = RequireClip($"A_FP_PCH_{code}_Aim_Pose");
        c.Fire = RequireClip($"A_FP_PCH_{family}_Fire");
        c.AimFire = RequireClip($"A_FP_PCH_{family}_Aim_Fire");
        c.Idle = RequireClip($"A_FP_PCH_{code}_Idle_Pose");
        c.Reload = RequireClip($"A_FP_PCH_{code}_Reload");
        c.ReloadEmpty = RequireClip($"A_FP_PCH_{code}_Reload_Empty");
        c.AimReload = OptionalClip($"A_FP_PCH_{code}_Aim_Reload") ?? c.Reload;
        c.AimReloadEmpty = OptionalClip($"A_FP_PCH_{code}_Aim_Reload_Empty") ?? c.ReloadEmpty;
        c.Holster = OptionalClip($"A_FP_PCH_{code}_Holster") ?? RequireClip("A_FP_PCH_Handgun_Holster");
        c.Unholster = OptionalClip($"A_FP_PCH_{code}_Unholster") ?? RequireClip("A_FP_PCH_Handgun_Unholster");
        c.Inspect = OptionalClip($"A_FP_PCH_{code}_Inspect");
        c.Run = OptionalClip("A_FP_PCH_Run_02_Additive") ?? RequireClip("A_FP_PCH_Handgun_Run_01_Additive");
        return c;
    }

    private static ClipSet ResolveTPClips(Spec s)
    {
        ClipSet c = new ClipSet();
        if (s.Family == Family.Handgun)
        {
            c.AimPose = RequireClip("A_TP_CH_Handgun_Aim_Pose");
            c.Fire = RequireClip("A_TP_CH_Handgun_Fire");
            c.Idle = RequireClip("A_TP_CH_Handgun_Idle_Pose");
            c.Reload = RequireClip("A_TP_CH_Handgun_Reload");
            c.ReloadEmpty = RequireClip("A_TP_CH_Handgun_Reload_Empty");
            c.Inspect = RequireClip("A_TP_CH_Handgun_Inspect");
            c.Jog = RequireClip("A_TP_CH_Handgun_Jog_F");
            c.Sprint = RequireClip("A_TP_CH_Handgun_Sprint_F");
            c.SprintImpulse = RequireClip("A_TP_CH_Handgun_Sprint_Impulse_F");
            c.CrouchPose = RequireClip("A_TP_CH_Handgun_Crouch_Pose");
            c.CrouchAimPose = RequireClip("A_TP_CH_Handgun_Crouch_Aim_Pose");
            return c;
        }

        string family = s.Family == Family.AR ? "AR" : "SMG";
        string code = s.Model.Replace("SK_", "");
        c.AimPose = RequireClip($"A_TP_CH_{code}_Aim_Pose");
        c.Fire = s.Family == Family.AR
            ? RequireClip("A_TP_CH_AR_Fire")
            : (OptionalClip($"A_TP_CH_{code}_Fire") ?? RequireClip("A_TP_CH_SMG_01_Fire"));
        c.Idle = RequireClip($"A_TP_CH_{code}_Idle_Pose");
        c.Reload = RequireClip($"A_TP_CH_{code}_Reload");
        c.ReloadEmpty = RequireClip($"A_TP_CH_{code}_Reload_Empty");
        c.Inspect = OptionalClip($"A_TP_CH_{code}_Inspect");
        c.Jog = RequireClip($"A_TP_CH_{code}_Jog_F");
        c.Sprint = RequireClip($"A_TP_CH_{code}_Sprint_F");
        c.SprintImpulse = RequireClip($"A_TP_CH_{code}_Sprint_Impulse_F");
        c.CrouchPose = RequireClip($"A_TP_CH_{code}_Crouch_Pose");
        c.CrouchAimPose = RequireClip($"A_TP_CH_{code}_Crouch_Aim_Pose");
        return c;
    }

    private static void ResolvePartsClips(Spec s, string modelPath)
    {
        string code = s.Model.Replace("SK_", "");
        s.PartsReloadFP = FindEmbeddedClip(modelPath, $"A_FP_WEP_{code}_Reload");
        s.PartsReloadEmptyFP = FindEmbeddedClip(modelPath, $"A_FP_WEP_{code}_Reload_Empty");
        s.PartsReloadTP = FindEmbeddedClip(modelPath, $"A_TP_WEP_{code}_Reload");
        s.PartsReloadEmptyTP = FindEmbeddedClip(modelPath, $"A_TP_WEP_{code}_Reload_Empty");

        string fireName = s.Family == Family.Handgun
            ? $"A_WEP_{code}_Fire"
            : $"A_WEP_{code}_Fire";

        s.PartsFireFP = FindEmbeddedClip(modelPath, fireName);
        s.PartsFireTP = s.PartsFireFP;

        // El AR_02 del pack no trae take de Fire para las piezas. Se genera más adelante una
        // animación pequeña del Bolt para no dejar el arma totalmente estática al disparar.
        if (s.PartsFireFP == null && s.Model != "SK_AR_02")
            warnings.Add($"{s.Id}: no se encontró clip de partes '{fireName}'. Se usará Idle para el disparo de partes.");
    }

    private static void ValidateRequiredClips(Spec s)
    {
        if (s.FP.Fire == null || s.FP.Reload == null || s.FP.ReloadEmpty == null || s.FP.AimPose == null)
            throw new Exception($"{s.Id}: faltan clips críticos de primera persona.");
        if (s.TP.Fire == null || s.TP.Reload == null || s.TP.ReloadEmpty == null || s.TP.AimPose == null)
            throw new Exception($"{s.Id}: faltan clips críticos de tercera persona.");
        if (s.PartsReloadFP == null || s.PartsReloadEmptyFP == null ||
            s.PartsReloadTP == null || s.PartsReloadEmptyTP == null)
            throw new Exception($"{s.Id}: faltan clips críticos de recarga de las PARTES del arma (FP/TP) en {s.Model}. No se integra a medias.");
        if (s.PartsFireFP == null && s.Model != "SK_AR_02")
            throw new Exception($"{s.Id}: falta el clip de disparo de las PARTES del arma en {s.Model}.");
    }

    // =====================================================================
    // PLAYER
    // =====================================================================

    private static void IntegratePlayer(string playerPath, Spec[] specs)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve(playerPath));
        try
        {
            WeaponSwitcher switcher = root.GetComponentInChildren<WeaponSwitcher>(true);
            NetworkWeaponPresentation presentation = root.GetComponentInChildren<NetworkWeaponPresentation>(true);
            if (switcher == null || presentation == null)
                throw new Exception("Player.prefab no contiene WeaponSwitcher y NetworkWeaponPresentation esperados.");

            SerializedObject swSO = new SerializedObject(switcher);
            RuntimeAnimatorController fpBase = swSO.FindProperty("baseController").objectReferenceValue as RuntimeAnimatorController;
            RuntimeAnimatorController tpBase = swSO.FindProperty("tpBaseController").objectReferenceValue as RuntimeAnimatorController;
            if (fpBase == null || tpBase == null)
                throw new Exception("Player.prefab no tiene asignados los controllers base FP/TP en WeaponSwitcher.");

            SlotTemplate handgun = ReadSlotTemplate(swSO, "Handgun");
            SlotTemplate rifle = ReadSlotTemplate(swSO, "Rifle");
            if (handgun == null || rifle == null)
                throw new Exception("No encuentro los slots existentes Handgun y Rifle para usarlos como plantilla segura.");

            NetworkWeaponPresentation.Definition handgunTP = FindDefinition(presentation.Weapons, "Handgun");
            NetworkWeaponPresentation.Definition rifleTP = FindDefinition(presentation.Weapons, "Rifle");
            if (handgunTP == null || rifleTP == null)
                throw new Exception("NetworkWeaponPresentation no contiene las definiciones Handgun/Rifle actuales.");

            RemoveManagedPlayerEntries(swSO, presentation, root);
            swSO.ApplyModifiedPropertiesWithoutUndo();

            List<GeneratedWeapon> generated = new List<GeneratedWeapon>();
            foreach (Spec s in specs)
            {
                SlotTemplate familyTemplate = s.Family == Family.Handgun ? handgun : rifle;
                NetworkWeaponPresentation.Definition familyTP = s.Family == Family.Handgun ? handgunTP : rifleTP;

                AnimatorOverrideController fpOverride = BuildFPOverride(s, fpBase);
                AnimatorOverrideController tpOverride = BuildTPOverride(s, tpBase);
                AnimatorController partsFP = BuildPartsController(s, false);
                AnimatorController partsTP = BuildPartsController(s, true);
                GameObject hudIcon = BuildHudIconPrefab(s, familyTemplate.Icon);

                GeneratedWeapon gw = BuildFirstPersonWeapon(root, s, familyTemplate, familyTP, fpOverride, tpOverride, partsFP, partsTP, hudIcon);
                generated.Add(gw);
            }

            AppendSlots(swSO, generated);
            swSO.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(switcher);

            RebuildPresentationDefinitions(presentation, generated);
            EditorUtility.SetDirty(presentation);

            PrefabUtility.SaveAsPrefabAsset(root,WeaponAssetLocations.Resolve( playerPath));
            report.Add($"Player actualizado: {playerPath}");
            foreach (GeneratedWeapon g in generated)
                report.Add($"  + {g.Spec.Id}: FP + TP + HUD + casing + overrides");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GeneratedWeapon BuildFirstPersonWeapon(
        GameObject playerRoot,
        Spec s,
        SlotTemplate template,
        NetworkWeaponPresentation.Definition templateTP,
        AnimatorOverrideController fpOverride,
        AnimatorOverrideController tpOverride,
        AnimatorController partsFP,
        AnimatorController partsTP,
        GameObject hudIcon)
    {
        Transform parent = template.Root != null ? template.Root.transform.parent : null;
        if (parent == null)
            throw new Exception($"{s.Id}: no pude determinar el padre de Weapon_Inventory.");

        GameObject weaponRoot = PrefabUtility.InstantiatePrefab(s.ModelAsset, parent) as GameObject;
        if (weaponRoot == null)
            weaponRoot = UnityEngine.Object.Instantiate(s.ModelAsset, parent);
        weaponRoot.name = "DQ_Weapon_" + s.Id;
        weaponRoot.layer = template.Root.layer;
        SetLayerRecursively(weaponRoot.transform, template.Root.layer);
        AlignBySocket(weaponRoot.transform, template.Root.transform, "SOCKET_Grip");

        Transform muzzleSocket = FindDeep(weaponRoot.transform, "SOCKET_Muzzle");
        Transform ejectSocket = FindDeep(weaponRoot.transform, "SOCKET_Eject") ?? FindDeep(weaponRoot.transform, "SOCKET_Eject_C");
        if (muzzleSocket == null)
            throw new Exception($"{s.Id}: {s.Model} no tiene SOCKET_Muzzle.");
        if (ejectSocket == null)
            throw new Exception($"{s.Id}: {s.Model} no tiene SOCKET_Eject.");

        AddDefaultAttachments(weaponRoot, s);

        Muzzle muzzle = muzzleSocket.GetComponent<Muzzle>();
        if (muzzle == null)
            muzzle = muzzleSocket.gameObject.AddComponent<Muzzle>();
        Muzzle templateMuzzle = template.Root.GetComponentInChildren<Muzzle>(true);
        if (templateMuzzle != null)
            muzzle.CopySettingsFrom(templateMuzzle);
        SerializedObject muzzleSO = new SerializedObject(muzzle);
        muzzleSO.FindProperty("socket").objectReferenceValue = muzzleSocket;
        muzzleSO.ApplyModifiedPropertiesWithoutUndo();

        Animator partsAnimator = weaponRoot.GetComponent<Animator>();
        if (partsAnimator == null)
            partsAnimator = weaponRoot.AddComponent<Animator>();
        partsAnimator.runtimeAnimatorController = partsFP;
        partsAnimator.applyRootMotion = false;
        partsAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Weapon weapon = weaponRoot.GetComponent<Weapon>();
        if (weapon == null)
            weapon = weaponRoot.AddComponent<Weapon>();
        EditorUtility.CopySerialized(template.Weapon, weapon);
        ConfigureWeaponComponent(weapon, s, muzzleSocket, muzzle, partsAnimator);

        ConfigureAttachmentsComponent(weaponRoot, s);
        weaponRoot.SetActive(false);

        string tpPath = PresentationDir + "/" + s.Id + "_TP.prefab";
        GameObject tpPrefab = BuildThirdPersonPrefab(s, templateTP, partsTP, tpPath);
        ComputeThirdPersonGrip(templateTP, tpPrefab, out Vector3 gripPos, out Vector3 gripRot);

        Sprite shop = BuildShopSprite(s);

        return new GeneratedWeapon
        {
            Spec = s,
            Root = weaponRoot,
            Weapon = weapon,
            FPOverride = fpOverride,
            TPOverride = tpOverride,
            HudIconPrefab = hudIcon,
            Casing = template.Casing,
            Eject = ejectSocket,
            TPPrefab = tpPrefab,
            TPGripPosition = gripPos,
            TPGripRotation = gripRot,
            ShopSprite = shop
        };
    }

    private static void ConfigureWeaponComponent(Weapon weapon, Spec s, Transform muzzleSocket, Muzzle muzzle, Animator parts)
    {
        SerializedObject so = new SerializedObject(weapon);
        SetObject(so, "firePoint", muzzleSocket);
        SetObject(so, "muzzle", muzzle);
        SetObject(so, "partsAnimator", parts);
        SetBool(so, "isAutomatic", s.Automatic);
        SetFloat(so, "fireRate", s.FireInterval);
        SetFloat(so, "reloadTime", s.ReloadTime);
        SetFloat(so, "range", s.Range);
        SetInt(so, "damage", s.Damage);
        SetInt(so, "maxAmmo", s.Magazine);
        SetInt(so, "reserveAmmoCapacity", s.Reserve);
        SetBool(so, "forceReserveLimit", true);
        SetString(so, "weaponName", s.DisplayName);

        SetBool(so, "usesBurstFire", s.Burst);
        SetInt(so, "burstCount", s.Burst ? s.BurstCount : 3);
        SetFloat(so, "burstShotInterval", s.Burst ? s.BurstShotInterval : 0.07f);
        SetFloat(so, "burstCooldown", s.Burst ? s.BurstCooldown : s.FireInterval);

        SetBool(so, "enforceReloadDuration", true);
        float reloadSpeed = s.FP.Reload != null ? Mathf.Max(0.01f, s.FP.Reload.length / s.ReloadTime) : 1f;
        float reloadEmptySpeed = s.FP.ReloadEmpty != null ? Mathf.Max(0.01f, s.FP.ReloadEmpty.length / s.ReloadTime) : reloadSpeed;
        SetFloat(so, "reloadClipSpeedMultiplier", reloadSpeed);
        SetFloat(so, "reloadEmptyClipSpeedMultiplier", reloadEmptySpeed);

        float partsReloadSpeed = s.PartsReloadFP != null ? Mathf.Max(0.01f, s.PartsReloadFP.length / s.ReloadTime) : 1f;
        float partsReloadEmptySpeed = s.PartsReloadEmptyFP != null ? Mathf.Max(0.01f, s.PartsReloadEmptyFP.length / s.ReloadTime) : partsReloadSpeed;
        SetFloat(so, "partsReloadClipSpeedMultiplier", partsReloadSpeed);
        SetFloat(so, "partsReloadEmptyClipSpeedMultiplier", partsReloadEmptySpeed);

        float tpReloadSpeed = s.TP.Reload != null ? Mathf.Max(0.01f, s.TP.Reload.length / s.ReloadTime) : 1f;
        float tpReloadEmptySpeed = s.TP.ReloadEmpty != null ? Mathf.Max(0.01f, s.TP.ReloadEmpty.length / s.ReloadTime) : tpReloadSpeed;
        SetFloat(so, "tpReloadClipSpeedMultiplier", tpReloadSpeed);
        SetFloat(so, "tpReloadEmptyClipSpeedMultiplier", tpReloadEmptySpeed);

        float tpPartsReloadSpeed = s.PartsReloadTP != null ? Mathf.Max(0.01f, s.PartsReloadTP.length / s.ReloadTime) : partsReloadSpeed;
        float tpPartsReloadEmptySpeed = s.PartsReloadEmptyTP != null ? Mathf.Max(0.01f, s.PartsReloadEmptyTP.length / s.ReloadTime) : partsReloadEmptySpeed;
        SetFloat(so, "tpPartsReloadClipSpeedMultiplier", tpPartsReloadSpeed);
        SetFloat(so, "tpPartsReloadEmptyClipSpeedMultiplier", tpPartsReloadEmptySpeed);

        SetObject(so, "shootSound", s.FireSoundAsset);
        SetObject(so, "reloadSound", s.ReloadSoundAsset);
        SetObject(so, "reloadEmptySound", s.ReloadEmptySoundAsset);

        SetBool(so, "firesMultiplePellets", false);
        SetBool(so, "requiresBoltActionAfterFire", false);
        SetBool(so, "usesMultiPartReload", false);

        // C.A. define daño/alcance nominal. No reducimos el daño dentro de ese alcance.
        SetFloat(so, "damageFalloffStart", s.Range);
        SetFloat(so, "damageFalloffEnd", s.Range);
        SetFloat(so, "minDamageMultiplier", 1f);

        int recoil = s.Family == Family.Handgun ? (int)RecoilPreset.Handgun : (s.Family == Family.SMG ? (int)RecoilPreset.SMG : (int)RecoilPreset.AR);
        SetEnumInt(so, "recoilPreset", recoil);

        // Dispersión, FOV, cámaras, trail, AudioSource y demás comportamiento no especificado
        // se heredan de la plantilla estable (Handgun o Rifle) para no inventar valores nuevos.

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);
    }

    // =====================================================================
    // OVERRIDES
    // =====================================================================

    private static AnimatorOverrideController BuildFPOverride(Spec s, RuntimeAnimatorController baseController)
    {
        string path = ControllersDir + "/" + s.Id + "_FP.overrideController";
        DeleteIfExists(path);
        AnimatorOverrideController aoc = new AnimatorOverrideController(baseController) { name = s.Id + "_FP" };
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        aoc.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            AnimationClip replacement = MapFPOverride(original, s.FP);
            if (replacement != null)
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
        }
        aoc.ApplyOverrides(overrides);
        AssetDatabase.CreateAsset(aoc,WeaponAssetLocations.Resolve( path));
        return aoc;
    }

    private static AnimationClip MapFPOverride(AnimationClip original, ClipSet c)
    {
        if (original == null) return null;
        string n = original.name.ToLowerInvariant();
        if (!n.Contains("handgun")) return null;
        if (n.Contains("aim_fire")) return c.AimFire ?? c.Fire;
        if (n.Contains("fire") && !n.Contains("empty")) return c.Fire;
        if (n.Contains("aim_reload_empty")) return c.AimReloadEmpty ?? c.ReloadEmpty;
        if (n.Contains("aim_reload")) return c.AimReload ?? c.Reload;
        if (n.Contains("reload_empty")) return c.ReloadEmpty;
        if (n.Contains("reload")) return c.Reload;
        if (n.Contains("aim_pose")) return c.AimPose;
        if (n.Contains("idle_pose")) return c.Idle;
        if (n.Contains("unholster")) return c.Unholster;
        if (n.Contains("holster")) return c.Holster;
        if (n.Contains("inspect")) return c.Inspect;
        if (n.Contains("run")) return c.Run;
        return null;
    }

    private static AnimatorOverrideController BuildTPOverride(Spec s, RuntimeAnimatorController baseController)
    {
        string path = ControllersDir + "/" + s.Id + "_TP.overrideController";
        DeleteIfExists(path);
        AnimatorOverrideController aoc = new AnimatorOverrideController(baseController) { name = s.Id + "_TP" };
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        aoc.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            AnimationClip replacement = MapTPOverride(original, s.TP);
            if (replacement != null)
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, replacement);
        }
        aoc.ApplyOverrides(overrides);
        AssetDatabase.CreateAsset(aoc,WeaponAssetLocations.Resolve( path));
        return aoc;
    }

    private static AnimationClip MapTPOverride(AnimationClip original, ClipSet c)
    {
        if (original == null) return null;
        string n = original.name.ToLowerInvariant();
        if (!n.Contains("handgun")) return null;
        if (n.Contains("reload_empty")) return c.ReloadEmpty;
        if (n.Contains("reload")) return c.Reload;
        if (n.Contains("crouch_aim")) return c.CrouchAimPose;
        if (n.Contains("crouch_pose")) return c.CrouchPose;
        if (n.Contains("aim_pose")) return c.AimPose;
        if (n.Contains("sprint_impulse")) return c.SprintImpulse;
        if (n.Contains("sprint")) return c.Sprint;
        if (n.Contains("jog_f")) return c.Jog;
        if (n.Contains("idle_pose")) return c.Idle;
        if (n.Contains("inspect")) return c.Inspect;
        if (n.Contains("fire")) return c.Fire;
        return null;
    }

    // =====================================================================
    // ANIMATOR DE PARTES DEL ARMA
    // =====================================================================

    private static AnimatorController BuildPartsController(Spec s, bool thirdPerson)
    {
        string suffix = thirdPerson ? "TPParts" : "FPParts";
        string path = ControllersDir + "/" + s.Id + "_" + suffix + ".controller";
        DeleteIfExists(path);

        AnimationClip fire = thirdPerson ? s.PartsFireTP : s.PartsFireFP;
        AnimationClip reload = thirdPerson ? (s.PartsReloadTP ?? s.PartsReloadFP) : s.PartsReloadFP;
        AnimationClip reloadEmpty = thirdPerson ? (s.PartsReloadEmptyTP ?? s.PartsReloadEmptyFP) : s.PartsReloadEmptyFP;

        if (fire == null && s.Model == "SK_AR_02")
            fire = BuildProceduralAR02BoltClip(s, thirdPerson);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(WeaponAssetLocations.Resolve(path));
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        sm.name = "WeaponParts";
        AnimatorState idle = sm.AddState("Idle");
        sm.defaultState = idle;
        AnimatorState stFire = sm.AddState("Fire");
        AnimatorState stReload = sm.AddState("Reload");
        AnimatorState stReloadEmpty = sm.AddState("Reload Empty");
        stFire.motion = fire;
        stReload.motion = reload;
        stReloadEmpty.motion = reloadEmpty;
        if (fire != null)
        {
            float visualShotInterval = s.Burst ? s.BurstShotInterval : s.FireInterval;
            stFire.speed = Mathf.Max(1f, fire.length / Mathf.Max(0.04f, visualShotInterval));
        }
        AddReturnToIdle(stFire, idle, 0.03f);
        AddReturnToIdle(stReload, idle, 0.02f);
        AddReturnToIdle(stReloadEmpty, idle, 0.02f);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddReturnToIdle(AnimatorState from, AnimatorState to, float duration)
    {
        AnimatorStateTransition tr = from.AddTransition(to);
        tr.hasExitTime = true;
        tr.exitTime = 1f;
        tr.hasFixedDuration = true;
        tr.duration = duration;
    }

    private static AnimationClip BuildProceduralAR02BoltClip(Spec s, bool thirdPerson)
    {
        string path = ControllersDir + "/" + s.Id + (thirdPerson ? "_TP" : "_FP") + "_BoltFire.anim";
        DeleteIfExists(path);
        AnimationClip clip = new AnimationClip { name = s.Id + "_BoltFire", frameRate = 60f };

        GameObject tempParent = new GameObject("__DQ_BOLT_BUILD_" + s.Id);
        tempParent.SetActive(false);
        GameObject temp = PrefabUtility.InstantiatePrefab(s.ModelAsset, tempParent.transform) as GameObject;
        if (temp == null) temp = UnityEngine.Object.Instantiate(s.ModelAsset, tempParent.transform);
        try
        {
            Transform bolt = FindDeep(temp.transform, "Bolt");
            if (bolt == null)
            {
                warnings.Add("BurstRifle/AR_02: no encontré transform 'Bolt'; el clip de partes de disparo quedará vacío.");
                AssetDatabase.CreateAsset(clip,WeaponAssetLocations.Resolve( path));
                return clip;
            }

            string relative = AnimationUtility.CalculateTransformPath(bolt, temp.transform);
            Vector3 p = bolt.localPosition;
            // El recorrido es pequeño y local. Se evita asumir un eje de mundo del FBX.
            Vector3 dir = Vector3.back * 0.035f;
            AddPositionCurve(clip, relative, "m_LocalPosition.x", p.x, p.x + dir.x);
            AddPositionCurve(clip, relative, "m_LocalPosition.y", p.y, p.y + dir.y);
            AddPositionCurve(clip, relative, "m_LocalPosition.z", p.z, p.z + dir.z);
            AssetDatabase.CreateAsset(clip,WeaponAssetLocations.Resolve( path));
            return clip;
        }
        finally
        {
            if (tempParent != null)
                UnityEngine.Object.DestroyImmediate(tempParent);
        }
    }

    private static void AddPositionCurve(AnimationClip clip, string path, string property, float start, float back)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start),
            new Keyframe(0.035f, back),
            new Keyframe(0.09f, start));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }

    // =====================================================================
    // MODELOS, ATTACHMENTS, MUZZLE, TP
    // =====================================================================

    private static void AddDefaultAttachments(GameObject root, Spec s)
    {
        // IMPORTANTE: en este pack la mira metálica/default NO se monta en SOCKET_Scope.
        // SOCKET_Scope es para miras opcionales. Las armas originales del proyecto (por ejemplo
        // Handgun_03) colocan la mira default en SOCKET_Default, que además sigue al bolt/slide.
        Transform defaultScopeSocket = FindDeep(root.transform, "SOCKET_Default") ?? FindDeep(root.transform, "SOCKET_Scope");
        Transform magSocket = FindDeep(root.transform, "SOCKET_Magazine");

        if (defaultScopeSocket != null && s.ScopeModelAsset != null && FindDirect(defaultScopeSocket, "DQ_DefaultScope") == null)
        {
            GameObject scope = PrefabUtility.InstantiatePrefab(s.ScopeModelAsset, defaultScopeSocket) as GameObject;
            if (scope == null) scope = UnityEngine.Object.Instantiate(s.ScopeModelAsset, defaultScopeSocket);
            scope.name = "DQ_DefaultScope";
            ResetLocal(scope.transform);
            SetLayerRecursively(scope.transform, root.layer);
            ValidateAttachmentNearSocket(scope, defaultScopeSocket, s.Id, "mira default");
        }

        if (magSocket != null && s.MagazineModelAsset != null && FindDirect(magSocket, "DQ_DefaultMagazine") == null)
        {
            GameObject mag = PrefabUtility.InstantiatePrefab(s.MagazineModelAsset, magSocket) as GameObject;
            if (mag == null) mag = UnityEngine.Object.Instantiate(s.MagazineModelAsset, magSocket);
            mag.name = "DQ_DefaultMagazine";
            ResetLocal(mag.transform);
            SetLayerRecursively(mag.transform, root.layer);
            ValidateAttachmentNearSocket(mag, magSocket, s.Id, "cargador");
        }

        ValidateGeneratedWeaponGeometry(root, s.Id);
    }

    private static void ValidateAttachmentNearSocket(GameObject attachment, Transform socket, string id, string label)
    {
        if (attachment == null || socket == null) return;
        Renderer[] rs = attachment.GetComponentsInChildren<Renderer>(true);
        if (rs == null || rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++)
            if (rs[i] != null) b.Encapsulate(rs[i].bounds);

        // El socket debe quedar dentro o muy cerca del accesorio. Si no, el FBX está
        // montado en un nodo incorrecto y terminaría flotando como en la integración anterior.
        float distance = Mathf.Sqrt(b.SqrDistance(socket.position));
        if (distance > 0.75f)
            throw new Exception($"{id}: {label} quedó a {distance:0.00}m de su socket. Integración cancelada para evitar piezas flotando.");
    }

    private static void ValidateGeneratedWeaponGeometry(GameObject root, string id)
    {
        if (root == null) return;

        // Todos los SK_* del pack vienen a escala 1. Si el cálculo de grip produjo una escala
        // distinta, la integración está mal y es preferible abortar antes de guardar el Player.
        Vector3 scale = root.transform.localScale;
        if (Mathf.Abs(scale.x - 1f) > 0.02f || Mathf.Abs(scale.y - 1f) > 0.02f || Mathf.Abs(scale.z - 1f) > 0.02f)
            throw new Exception($"{id}: escala inválida del modelo ({scale}). Debe permanecer en (1,1,1).");

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        Bounds all = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            if (renderers[i] != null) all.Encapsulate(renderers[i].bounds);

        // Armas de este pack están alrededor de 0.2-2 m. Un bounds gigantesco indica casi
        // siempre un accesorio parentado al socket equivocado o una escala heredada de huesos.
        float max = Mathf.Max(all.size.x, Mathf.Max(all.size.y, all.size.z));
        if (max > 5f)
            throw new Exception($"{id}: geometría fuera de rango ({all.size}). Revisá sockets/escala; se cancela para evitar piezas flotando.");
    }

    private static void ConfigureAttachmentsComponent(GameObject root, Spec s)
    {
        WeaponAttachments attachments = root.GetComponent<WeaponAttachments>();
        if (attachments == null)
            attachments = root.AddComponent<WeaponAttachments>();

        Transform scope = FindDeep(root.transform, "DQ_DefaultScope");
        Transform mag = FindDeep(root.transform, "DQ_DefaultMagazine");
        SerializedObject so = new SerializedObject(attachments);
        SetObject(so, "bodySprite", s.HudSpriteAsset);
        SetBool(so, "hideDefaultScopeModelWhenScoped", true);
        SetBool(so, "applyToModel", true);

        SerializedProperty scopeDefault = so.FindProperty("scopeDefault");
        scopeDefault.FindPropertyRelative("name").stringValue = "Mira estándar";
        scopeDefault.FindPropertyRelative("model").objectReferenceValue = scope != null ? scope.gameObject : null;
        scopeDefault.FindPropertyRelative("sprite").objectReferenceValue = s.ScopeSpriteAsset;

        SerializedProperty scopes = so.FindProperty("scopes"); scopes.arraySize = 0;
        SetInt(so, "scopeIndex", -1);

        SerializedProperty mags = so.FindProperty("magazines");
        mags.arraySize = 1;
        SerializedProperty m0 = mags.GetArrayElementAtIndex(0);
        m0.FindPropertyRelative("name").stringValue = "Cargador estándar";
        m0.FindPropertyRelative("model").objectReferenceValue = mag != null ? mag.gameObject : null;
        m0.FindPropertyRelative("sprite").objectReferenceValue = s.MagazineSpriteAsset;
        SetInt(so, "magazineIndex", 0);

        foreach (string p in new[] { "lasers", "grips", "muzzles" })
            so.FindProperty(p).arraySize = 0;
        SetInt(so, "laserIndex", -1); SetInt(so, "gripIndex", -1); SetInt(so, "muzzleIndex", -1);
        so.ApplyModifiedPropertiesWithoutUndo();
        attachments.ApplyModels();
        EditorUtility.SetDirty(attachments);
    }

    private static GameObject BuildThirdPersonPrefab(Spec s, NetworkWeaponPresentation.Definition templateTP, AnimatorController partsController, string path)
    {
        DeleteIfExists(path);

        // IMPORTANTE: varios modelos del pack traen scripts/componentes que pueden ejecutar
        // OnEnable apenas se instancian. Esos scripts esperan que exista un Animator en la
        // raiz. Construimos el prefab debajo de un padre INACTIVO para que nada se ejecute
        // antes de agregar/configurar el Animator.
        GameObject stagingRoot = new GameObject("__DQ_TP_BUILD_" + s.Id);
        stagingRoot.SetActive(false);

        GameObject go = null;
        try
        {
            go = PrefabUtility.InstantiatePrefab(s.ModelAsset, stagingRoot.transform) as GameObject;
            if (go == null)
                go = UnityEngine.Object.Instantiate(s.ModelAsset, stagingRoot.transform);

            if (go == null)
                throw new Exception($"{s.Id}: no pude instanciar el modelo TP {s.Model}.");

            go.name = s.Id + "_TP";
            go.transform.localScale = Vector3.one;

            // El Animator se agrega PRIMERO. No mover este bloque debajo de Muzzle,
            // attachments u otros componentes que puedan consultar GetComponent<Animator>().
            Animator anim = go.GetComponent<Animator>();
            if (anim == null)
                anim = go.AddComponent<Animator>();

            if (anim == null)
                throw new Exception($"{s.Id}: Unity no pudo agregar Animator al prefab TP.");

            anim.enabled = false;
            anim.runtimeAnimatorController = partsController;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            AddDefaultAttachments(go, s);

            Transform muzzleSocket = FindDeep(go.transform, "SOCKET_Muzzle");
            if (muzzleSocket != null)
            {
                Muzzle muzzle = muzzleSocket.GetComponent<Muzzle>();
                if (muzzle == null)
                    muzzle = muzzleSocket.gameObject.AddComponent<Muzzle>();

                Muzzle sourceMuzzle = templateTP.Model != null
                    ? templateTP.Model.GetComponentInChildren<Muzzle>(true)
                    : null;

                if (sourceMuzzle != null)
                    muzzle.CopySettingsFrom(sourceMuzzle);

                SerializedObject mso = new SerializedObject(muzzle);
                SetObject(mso, "socket", muzzleSocket);
                mso.ApplyModifiedPropertiesWithoutUndo();
            }

            // Se guarda habilitado para runtime. El padre temporal sigue inactivo, por lo
            // que OnEnable no corre durante la construccion.
            anim.enabled = true;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go,WeaponAssetLocations.Resolve( path));
            if (saved == null)
                throw new Exception($"No pude guardar el prefab TP de {s.Id}.");

            Animator savedAnimator = saved.GetComponent<Animator>();
            if (savedAnimator == null)
                throw new Exception($"{s.Id}: el prefab TP se guardo sin Animator. Integracion cancelada para no dejar referencias rotas.");

            return saved;
        }
        finally
        {
            if (stagingRoot != null)
                UnityEngine.Object.DestroyImmediate(stagingRoot);
        }
    }

    private static void ComputeThirdPersonGrip(NetworkWeaponPresentation.Definition reference, GameObject targetPrefab, out Vector3 position, out Vector3 rotation)
    {
        position = reference.GripPosition;
        rotation = reference.GripRotation;
        if (reference.Model == null || targetPrefab == null)
            return;

        GameObject parent = new GameObject("__DQ_GripCalc");
        GameObject refGo = null;
        GameObject targetGo = null;
        try
        {
            refGo = UnityEngine.Object.Instantiate(reference.Model, parent.transform);
            refGo.transform.localPosition = reference.GripPosition;
            refGo.transform.localRotation = Quaternion.Euler(reference.GripRotation);
            refGo.transform.localScale = Vector3.one;
            targetGo = UnityEngine.Object.Instantiate(targetPrefab, parent.transform);
            ResetLocal(targetGo.transform);

            Transform refGrip = FindDeep(refGo.transform, "SOCKET_Grip");
            Transform targetGrip = FindDeep(targetGo.transform, "SOCKET_Grip");
            if (refGrip == null || targetGrip == null)
            {
                warnings.Add("No se pudo calcular Grip TP por socket; se usa la posición de la familia existente.");
                return;
            }

            Matrix4x4 desiredGrip = parent.transform.worldToLocalMatrix * refGrip.localToWorldMatrix;
            Matrix4x4 targetGripRelativeRoot = targetGo.transform.worldToLocalMatrix * targetGrip.localToWorldMatrix;
            Matrix4x4 rootMatrix = desiredGrip * targetGripRelativeRoot.inverse;
            position = rootMatrix.GetColumn(3);
            rotation = rootMatrix.rotation.eulerAngles;
        }
        finally
        {
            if (refGo != null) UnityEngine.Object.DestroyImmediate(refGo);
            if (targetGo != null) UnityEngine.Object.DestroyImmediate(targetGo);
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    private static void AlignBySocket(Transform target, Transform reference, string socketName)
    {
        if (target == null || reference == null)
            return;

        // Los modelos del pack comparten unidades. NUNCA tomar lossyscale de un hueso/socket:
        // rigs importados pueden tener escalas internas y eso termina agrandando el arma completa.
        target.localScale = Vector3.one;

        Transform a = FindDeep(reference, socketName);
        Transform b = FindDeep(target, socketName);
        if (a == null || b == null)
        {
            target.localPosition = reference.localPosition;
            target.localRotation = reference.localRotation;
            target.localScale = Vector3.one;
            warnings.Add(target.name + ": no pude alinear por " + socketName + "; copié posición/rotación de la familia y mantuve escala 1.");
            return;
        }

        // Alineación en world-space solamente por pose (posición + rotación).
        // 1) Rotar el arma para que el SOCKET_Grip tenga la misma orientación.
        Quaternion rotationDelta = a.rotation * Quaternion.Inverse(b.rotation);
        target.rotation = rotationDelta * target.rotation;

        // 2) Trasladar para que ambos grips coincidan exactamente.
        Vector3 positionDelta = a.position - b.position;
        target.position += positionDelta;

        // Mantener la escala del asset intacta.
        target.localScale = Vector3.one;
    }

    // =====================================================================
    // HUD
    // =====================================================================

    private static GameObject BuildHudIconPrefab(Spec s, GameObject templatePrefab)
    {
        if (templatePrefab == null)
            throw new Exception($"{s.Id}: la familia no tiene Weapon Icon de plantilla.");
        string sourcePath = AssetDatabase.GetAssetPath(templatePrefab);
        if (string.IsNullOrEmpty(sourcePath))
            throw new Exception($"{s.Id}: no pude obtener path del icono HUD plantilla.");

        string path = HudDir + "/" + s.Id + "_Icon.prefab";
        DeleteIfExists(path);
        GameObject contents = PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve(sourcePath));
        try
        {
            contents.name = s.Id + "_Icon";
            SetImageSprite(contents.transform, "Body", s.HudSpriteAsset);
            SetImageSprite(contents.transform, "Weapon", s.HudSpriteAsset);
            SetImageSprite(contents.transform, "Scope_Default", s.ScopeSpriteAsset);
            SetImageSprite(contents.transform, "Magazine", s.MagazineSpriteAsset);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents,WeaponAssetLocations.Resolve( path));
            if (saved == null) throw new Exception("No pude crear icono HUD " + s.Id);
            return saved;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void SetImageSprite(Transform root, string name, Sprite sprite)
    {
        Transform t = FindDeep(root, name);
        if (t == null) return;
        Image img = t.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
            EditorUtility.SetDirty(img);
        }
    }

    // =====================================================================
    // TIENDA
    // =====================================================================

    private static void IntegrateMerchant(string merchantPath, Spec[] specs)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(WeaponAssetLocations.Resolve(merchantPath));
        try
        {
            NPCWeaponVendor vendor = root.GetComponentInChildren<NPCWeaponVendor>(true);
            if (vendor == null) throw new Exception("Mercader.prefab no contiene NPCWeaponVendor.");

            SerializedObject so = new SerializedObject(vendor);
            SerializedProperty offers = so.FindProperty("offers");
            foreach (Spec s in specs)
            {
                int idx = FindOfferIndex(offers, s.Id);
                if (idx < 0)
                {
                    idx = offers.arraySize;
                    offers.InsertArrayElementAtIndex(idx);
                }
                SerializedProperty o = offers.GetArrayElementAtIndex(idx);
                Sprite shopSprite = LoadGeneratedShopSprite(s.Id) ?? s.HudSpriteAsset;

                SetRelativeString(o, "weaponId", s.Id);
                SetRelativeString(o, "weaponName", s.DisplayName);
                SetRelativeInt(o, "cost", s.LegacyCost);
                SetRelativeObject(o, "weaponIcon", s.HudSpriteAsset);
                SetRelativeObject(o, "imagenTarjeta", shopSprite);
                SetRelativeObject(o, "iconoHud", s.HudSpriteAsset);
                SetRelativeInt(o, "categoria", (int)CategoriaTienda.Armas);
                SetRelativeBool(o, "disponible", true);
                SetRelativeInt(o, "precioPilaDeLatas", s.Cans);
                SetRelativeInt(o, "precioCantimplora", s.Water);
                SetRelativeInt(o, "precioBidon", s.Fuel);
                SetRelativeInt(o, "dificultades", 7);
                SetRelativeInt(o, "modos", 7);
                SetRelativeString(o, "descripcion", s.Description);
                SetRelativeInt(o, "dano", s.Damage);
                SetRelativeFloat(o, "alcance", s.Range);
                SetRelativeBool(o, "esArrojadiza", false);
                SetRelativeInt(o, "capacidadCargador", s.Magazine);
                SetRelativeFloat(o, "cadencia", s.Burst ? s.BurstCooldown : s.FireInterval);
                SetRelativeBool(o, "esConsumible", false);
                SetRelativeInt(o, "puntosDeSalud", 0);
                SetRelativeBool(o, "esMunicion", false);
                SetRelativeInt(o, "armadura", 0);
                SetRelativeBool(o, "esEquipamiento", false);
                SetRelativeInt(o, "piezaBlindaje", 0);
                SetRelativeBool(o, "esEspacioInventario", false);
                SetRelativeInt(o, "slotExtra", 0);
                SetRelativeBool(o, "esBarril", false);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(vendor);
            PrefabUtility.SaveAsPrefabAsset(root,WeaponAssetLocations.Resolve( merchantPath));
            report.Add($"Mercader actualizado: {merchantPath} (+6 ofertas de armas)");
            report.Add("Nota: precios de recursos son valores iniciales porque los C.A. no especifican precios.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int FindOfferIndex(SerializedProperty offers, string id)
    {
        for (int i = 0; i < offers.arraySize; i++)
        {
            SerializedProperty p = offers.GetArrayElementAtIndex(i).FindPropertyRelative("weaponId");
            if (p != null && p.stringValue == id) return i;
        }
        return -1;
    }

    // =====================================================================
    // TARJETAS DE TIENDA DESDE EL MODELO REAL
    // =====================================================================

    private static string ResolveShopDirectory()
    {
        const string preferred = "Assets/Art/Sprites/Tienda";
        if (AssetDatabase.IsValidFolder(WeaponAssetLocations.Resolve(preferred)))
            return preferred;

        // Si la carpeta tiene otra mayúscula/minúscula o está un nivel distinto,
        // buscamos una ruta que termine en Art/Sprites/Tienda antes de usar Generated.
        string assetsAbsolute = Application.dataPath.Replace('\\', '/');
        if (Directory.Exists(WeaponAssetLocations.Resolve(assetsAbsolute)))
        {
            foreach (string dir in Directory.GetDirectories(assetsAbsolute, "Tienda", SearchOption.AllDirectories))
            {
                string normalized = dir.Replace('\\', '/');
                if (normalized.EndsWith("/Art/Sprites/Tienda", StringComparison.OrdinalIgnoreCase))
                    return "Assets" + normalized.Substring(assetsAbsolute.Length);
            }
        }

        return GeneratedShopDir;
    }

    private static Sprite BuildShopSprite(Spec s)
    {
        string pngAsset = ResolveShopDirectory() + "/Tienda_" + s.Model + ".png";
        string absolute = AssetPathToAbsolute(pngAsset);
        try
        {
            GameObject previewRoot = UnityEngine.Object.Instantiate(s.ModelAsset);
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
            previewRoot.transform.localScale = Vector3.one;
            AddDefaultAttachments(previewRoot, s);
            try
            {
                Texture2D texture = RenderWeaponPreview(previewRoot, 1024, 512);
                if (texture == null) throw new Exception("PreviewRenderUtility devolvió null.");
                Directory.CreateDirectory(WeaponAssetLocations.Resolve(Path.GetDirectoryName(WeaponAssetLocations.Resolve(absolute))));
                File.WriteAllBytes(WeaponAssetLocations.Resolve(absolute), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
            finally
            {
                if (previewRoot != null)
                    UnityEngine.Object.DestroyImmediate(previewRoot);
            }

            AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(pngAsset), ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(WeaponAssetLocations.Resolve(pngAsset)) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WeaponAssetLocations.Resolve(pngAsset));
            if (sprite != null)
            {
                report.Add("  Tarjeta tienda generada: " + pngAsset);
                return sprite;
            }
        }
        catch (Exception e)
        {
            warnings.Add($"{s.Id}: no pude renderizar tarjeta de tienda desde el modelo ({e.Message}). Se usa el sprite HUD como fallback.");
        }
        return s.HudSpriteAsset;
    }

    private static Texture2D RenderWeaponPreview(GameObject go, int width, int height)
    {
        PreviewRenderUtility preview = new PreviewRenderUtility();
        try
        {
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            preview.camera.orthographic = true;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 100f;
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 210f, 0f);
            preview.ambientColor = new Color(0.35f, 0.35f, 0.35f, 1f);

            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            preview.AddSingleGO(go);

            Bounds b = CalculateBounds(go);
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));

            // Elegimos una vista verdaderamente lateral usando Grip -> Muzzle como eje longitudinal.
            // Así funciona aunque un FBX tenga el arma orientada en X, Y o Z.
            Transform grip = FindDeep(go.transform, "SOCKET_Grip");
            Transform muzzle = FindDeep(go.transform, "SOCKET_Muzzle");
            Vector3 barrel = (grip != null && muzzle != null)
                ? (muzzle.position - grip.position).normalized
                : Vector3.forward;
            if (barrel.sqrMagnitude < 0.5f) barrel = Vector3.forward;

            Vector3 side = Vector3.Cross(Vector3.up, barrel).normalized;
            if (side.sqrMagnitude < 0.5f) side = Vector3.forward;
            Vector3 view = (side + Vector3.up * 0.08f).normalized;

            preview.camera.transform.position = b.center - view * Mathf.Max(2f, maxDim * 3f);
            preview.camera.transform.LookAt(b.center, Vector3.up);

            // Calcula el encuadre en el espacio de la cámara (no con X/Y del mundo).
            float halfHeight, halfWidth;
            CameraProjectedHalfExtents(preview.camera, b, out halfWidth, out halfHeight);
            float aspect = width / (float)height;
            preview.camera.orthographicSize = Mathf.Max(0.15f,
                Mathf.Max(halfHeight * 1.22f, (halfWidth / Mathf.Max(0.01f, aspect)) * 1.22f));

            Rect rect = new Rect(0, 0, width, height);
            preview.BeginPreview(rect, GUIStyle.none);
            preview.Render(true, false);
            Texture rendered = preview.EndPreview();
            RenderTexture rt = rendered as RenderTexture;
            if (rt == null)
                throw new Exception("El preview no devolvió RenderTexture.");

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            copy.ReadPixels(rect, 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            return copy;
        }
        finally
        {
            preview.Cleanup();
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private static void CameraProjectedHalfExtents(Camera camera, Bounds b, out float halfWidth, out float halfHeight)
    {
        halfWidth = 0.1f;
        halfHeight = 0.1f;
        Vector3 c = b.center;
        Vector3 e = b.extents;
        Vector3[] corners = new Vector3[8]
        {
            c + new Vector3(-e.x,-e.y,-e.z), c + new Vector3(-e.x,-e.y, e.z),
            c + new Vector3(-e.x, e.y,-e.z), c + new Vector3(-e.x, e.y, e.z),
            c + new Vector3( e.x,-e.y,-e.z), c + new Vector3( e.x,-e.y, e.z),
            c + new Vector3( e.x, e.y,-e.z), c + new Vector3( e.x, e.y, e.z)
        };
        Matrix4x4 worldToCamera = camera.worldToCameraMatrix;
        Vector3 cc = worldToCamera.MultiplyPoint3x4(c);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 p = worldToCamera.MultiplyPoint3x4(corners[i]);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(p.x - cc.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(p.y - cc.y));
        }
    }

    // =====================================================================
    // SLOTS Y DEFINICIONES DE RED
    // =====================================================================

    private static SlotTemplate ReadSlotTemplate(SerializedObject switcherSO, string id)
    {
        SerializedProperty slots = switcherSO.FindProperty("slots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            SerializedProperty p = slots.GetArrayElementAtIndex(i);
            if (p.FindPropertyRelative("weaponId").stringValue != id) continue;
            return new SlotTemplate
            {
                Id = id,
                Root = p.FindPropertyRelative("weaponRoot").objectReferenceValue as GameObject,
                Weapon = p.FindPropertyRelative("weaponComponent").objectReferenceValue as Weapon,
                FPOverride = p.FindPropertyRelative("overrideController").objectReferenceValue as AnimatorOverrideController,
                TPOverride = p.FindPropertyRelative("tpOverrideController").objectReferenceValue as AnimatorOverrideController,
                Icon = p.FindPropertyRelative("weaponIcon").objectReferenceValue as GameObject,
                Casing = p.FindPropertyRelative("casingPrefab").objectReferenceValue as GameObject,
                Eject = p.FindPropertyRelative("casingEjectPoint").objectReferenceValue as Transform
            };
        }
        return null;
    }

    private static void RemoveManagedPlayerEntries(SerializedObject switcherSO, NetworkWeaponPresentation presentation, GameObject root)
    {
        SerializedProperty slots = switcherSO.FindProperty("slots");
        for (int i = slots.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty id = slots.GetArrayElementAtIndex(i).FindPropertyRelative("weaponId");
            if (ManagedIds.Contains(id.stringValue))
                slots.DeleteArrayElementAtIndex(i);
        }

        if (presentation.Weapons != null)
            presentation.Weapons = presentation.Weapons.Where(d => d != null && !ManagedIds.Contains(d.Id)).ToArray();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name.StartsWith("DQ_Weapon_", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
    }

    private static void AppendSlots(SerializedObject switcherSO, List<GeneratedWeapon> generated)
    {
        SerializedProperty slots = switcherSO.FindProperty("slots");
        foreach (GeneratedWeapon g in generated)
        {
            int i = slots.arraySize;
            slots.InsertArrayElementAtIndex(i);
            SerializedProperty p = slots.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("weaponId").stringValue = g.Spec.Id;
            p.FindPropertyRelative("weaponRoot").objectReferenceValue = g.Root;
            p.FindPropertyRelative("weaponComponent").objectReferenceValue = g.Weapon;
            p.FindPropertyRelative("overrideController").objectReferenceValue = g.FPOverride;
            p.FindPropertyRelative("tpOverrideController").objectReferenceValue = g.TPOverride;
            SerializedProperty tpId = p.FindPropertyRelative("tpWeaponId");
            if (tpId != null) tpId.intValue = g.Spec.Family == Family.Handgun ? 1 : 2;
            p.FindPropertyRelative("unlockedByDefault").boolValue = false;
            p.FindPropertyRelative("weaponIcon").objectReferenceValue = g.HudIconPrefab;
            // Campo legacy/no operativo, pero se conserva la misma categoría serializada que las familias actuales:
            // Handgun=Primary(0), Rifle/SMG=Secondary(1). Así no cambiamos datos históricos del prefab.
            p.FindPropertyRelative("category").enumValueIndex =
                g.Spec.Family == Family.Handgun ? (int)WeaponSlotCategory.Primary : (int)WeaponSlotCategory.Secondary;
            p.FindPropertyRelative("casingPrefab").objectReferenceValue = g.Casing;
            p.FindPropertyRelative("casingEjectPoint").objectReferenceValue = g.Eject;
        }
    }

    private static void RebuildPresentationDefinitions(NetworkWeaponPresentation presentation, List<GeneratedWeapon> generated)
    {
        List<NetworkWeaponPresentation.Definition> defs = presentation.Weapons != null
            ? presentation.Weapons.ToList()
            : new List<NetworkWeaponPresentation.Definition>();

        foreach (GeneratedWeapon g in generated)
        {
            defs.Add(new NetworkWeaponPresentation.Definition
            {
                Id = g.Spec.Id,
                Source = g.Weapon,
                Model = g.TPPrefab,
                Controller = g.TPOverride,
                AnimatorWeaponId = g.Spec.Family == Family.Handgun ? 1 : 2,
                Casing = g.Casing,
                GripPosition = g.TPGripPosition,
                GripRotation = g.TPGripRotation
            });
        }
        presentation.Weapons = defs.ToArray();
    }

    private static NetworkWeaponPresentation.Definition FindDefinition(NetworkWeaponPresentation.Definition[] defs, string id)
    {
        return defs == null ? null : Array.Find(defs, d => d != null && d.Id == id);
    }

    // =====================================================================
    // ASSET HELPERS
    // =====================================================================

    private static T RequireAsset<T>(string exactName) where T : UnityEngine.Object
    {
        T result = FindAsset<T>(exactName);
        if (result == null) throw new Exception("Falta asset requerido: " + exactName + " (" + typeof(T).Name + ")");
        return result;
    }

    private static T FindAsset<T>(string exactName) where T : UnityEngine.Object
    {
        string typeName = typeof(T) == typeof(GameObject) ? "GameObject" : typeof(T).Name;
        string[] guids = AssetDatabase.FindAssets(exactName + " t:" + typeName);
        T fallback = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T obj = AssetDatabase.LoadAssetAtPath<T>(WeaponAssetLocations.Resolve(path));
            if (obj == null) continue;
            string basename = Path.GetFileNameWithoutExtension(path);
            if (obj.name == exactName || basename == exactName) return obj;
            if (fallback == null) fallback = obj;
        }
        return fallback;
    }

    private static AnimationClip RequireClip(string exactName)
    {
        AnimationClip c = OptionalClip(exactName);
        if (c == null) throw new Exception("Falta AnimationClip requerido: " + exactName);
        return c;
    }

    private static AnimationClip OptionalClip(string exactName)
    {
        string[] guids = AssetDatabase.FindAssets(exactName);
        AnimationClip fallback = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(WeaponAssetLocations.Resolve(path)))
            {
                AnimationClip c = o as AnimationClip;
                if (c == null || c.name.StartsWith("__preview__")) continue;
                if (c.name == exactName || Path.GetFileNameWithoutExtension(path) == exactName)
                    return c;
                if (fallback == null && c.name.IndexOf(exactName, StringComparison.OrdinalIgnoreCase) >= 0)
                    fallback = c;
            }
        }
        return fallback;
    }

    private static AnimationClip FindEmbeddedClip(string modelPath, string exactName)
    {
        AnimationClip suffixMatch = null;
        foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(WeaponAssetLocations.Resolve(modelPath)))
        {
            AnimationClip c = o as AnimationClip;
            if (c == null || c.name.StartsWith("__preview__")) continue;
            if (c.name == exactName) return c;
            if (suffixMatch == null &&
                (c.name.EndsWith("|" + exactName, StringComparison.OrdinalIgnoreCase) ||
                 c.name.EndsWith(exactName, StringComparison.OrdinalIgnoreCase)))
                suffixMatch = c;
        }
        return suffixMatch;
    }

    private static void EnsureEmbeddedModelClips(string modelPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(WeaponAssetLocations.Resolve(modelPath)) as ModelImporter;
        if (importer == null) return;

        // Algunos FBX del pack (especialmente Handgun_04 / AR_02 / AR_03) vienen con
        // los takes internos nombrados como "SKEL_x|A_FP_WEP_x..." y clipAnimations vacío.
        // Los normalizamos al sufijo A_* para que los Animator de partes puedan encontrarlos
        // de forma determinista sin depender de cómo Unity haya decidido importarlos.
        ModelImporterClipAnimation[] configured = importer.clipAnimations;
        ModelImporterClipAnimation[] source = configured != null && configured.Length > 0
            ? configured
            : importer.defaultClipAnimations;

        if (source == null || source.Length == 0)
            return;

        bool changed = configured == null || configured.Length == 0;
        for (int i = 0; i < source.Length; i++)
        {
            string raw = !string.IsNullOrEmpty(source[i].takeName) ? source[i].takeName : source[i].name;
            if (string.IsNullOrEmpty(raw)) continue;
            int pipe = raw.LastIndexOf('|');
            string normalized = pipe >= 0 && pipe + 1 < raw.Length ? raw.Substring(pipe + 1) : raw;
            if (!string.Equals(source[i].name, normalized, StringComparison.Ordinal))
            {
                source[i].name = normalized;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = source;
            importer.SaveAndReimport();
            report.Add("Takes embebidos normalizados/expuestos: " + modelPath);
        }
    }

    private static string FindPrefabPathWithComponent<T>(string preferredName) where T : Component
    {
        string[] guids = AssetDatabase.FindAssets(preferredName + " t:Prefab");
        string fallback = null;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetLocations.Resolve(path));
            if (prefab == null || prefab.GetComponentInChildren<T>(true) == null) continue;
            if (Path.GetFileNameWithoutExtension(path) == preferredName) return path;
            if (fallback == null) fallback = path;
        }
        return fallback;
    }

    private static void BackupOnce(string source, string destination)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(WeaponAssetLocations.Resolve(destination)) != null) return;
        EnsureAssetFolder(Path.GetDirectoryName(WeaponAssetLocations.Resolve(destination)).Replace('\\', '/'));
        if (!AssetDatabase.CopyAsset(WeaponAssetLocations.Resolve(source),WeaponAssetLocations.Resolve( destination)))
            warnings.Add("No se pudo crear backup de " + source);
        else
            report.Add("Backup: " + destination);
    }

    private static void EnsureFolders()
    {
        foreach (string p in new[] { Root, ControllersDir, HudDir, PresentationDir, GeneratedShopDir, BackupDir })
            EnsureAssetFolder(p);
    }

    private static void EnsureAssetFolder(string path)
    {
        path = path.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(WeaponAssetLocations.Resolve(path))) return;
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(WeaponAssetLocations.Resolve(next))) AssetDatabase.CreateFolder(WeaponAssetLocations.Resolve(current), parts[i]);
            current = next;
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(WeaponAssetLocations.Resolve(path)) != null)
            AssetDatabase.DeleteAsset(WeaponAssetLocations.Resolve(path));
    }

    private static string AssetPathToAbsolute(string assetPath)
    {
        assetPath = WeaponAssetLocations.Resolve(assetPath);
        string project = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(project, assetPath).Replace('\\', '/');
    }

    private static Sprite LoadGeneratedShopSprite(string id)
    {
        Spec s = CreateSpecs().FirstOrDefault(x => x.Id == id);
        if (s == null) return null;
        return AssetDatabase.LoadAssetAtPath<Sprite>(WeaponAssetLocations.Resolve(ResolveShopDirectory() + "/Tienda_" + s.Model + ".png"));
    }

    private static void WriteReport()
    {
        try
        {
            EnsureAssetFolder(Root);
            File.WriteAllLines(WeaponAssetLocations.Resolve(AssetPathToAbsolute(ReportPath)), report);
            AssetDatabase.ImportAsset(WeaponAssetLocations.Resolve(ReportPath), ImportAssetOptions.ForceUpdate);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[NewWeapons] No pude escribir reporte: " + e.Message);
        }
    }

    // =====================================================================
    // SERIALIZED HELPERS
    // =====================================================================

    private static void SetObject(SerializedObject so, string property, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(property); if (p != null) p.objectReferenceValue = value;
    }
    private static void SetBool(SerializedObject so, string property, bool value)
    {
        SerializedProperty p = so.FindProperty(property); if (p != null) p.boolValue = value;
    }
    private static void SetInt(SerializedObject so, string property, int value)
    {
        SerializedProperty p = so.FindProperty(property); if (p != null) p.intValue = value;
    }
    private static void SetFloat(SerializedObject so, string property, float value)
    {
        SerializedProperty p = so.FindProperty(property); if (p != null) p.floatValue = value;
    }
    private static void SetString(SerializedObject so, string property, string value)
    {
        SerializedProperty p = so.FindProperty(property); if (p != null) p.stringValue = value;
    }
    private static void SetEnumInt(SerializedObject so, string property, int value)
    {
        SerializedProperty p = so.FindProperty(property); if (p != null) p.enumValueIndex = value;
    }

    private static void SetRelativeObject(SerializedProperty root, string property, UnityEngine.Object value)
    { SerializedProperty p = root.FindPropertyRelative(property); if (p != null) p.objectReferenceValue = value; }
    private static void SetRelativeString(SerializedProperty root, string property, string value)
    { SerializedProperty p = root.FindPropertyRelative(property); if (p != null) p.stringValue = value; }
    private static void SetRelativeInt(SerializedProperty root, string property, int value)
    { SerializedProperty p = root.FindPropertyRelative(property); if (p != null) p.intValue = value; }
    private static void SetRelativeFloat(SerializedProperty root, string property, float value)
    { SerializedProperty p = root.FindPropertyRelative(property); if (p != null) p.floatValue = value; }
    private static void SetRelativeBool(SerializedProperty root, string property, bool value)
    { SerializedProperty p = root.FindPropertyRelative(property); if (p != null) p.boolValue = value; }

    // =====================================================================
    // TRANSFORM HELPERS
    // =====================================================================

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            Transform f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }

    private static Transform FindDirect(Transform root, string name)
    {
        if (root == null) return null;
        foreach (Transform c in root) if (c.name == name) return c;
        return null;
    }

    private static void ResetLocal(Transform t)
    {
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform c in root) SetLayerRecursively(c, layer);
    }
}
#endif
