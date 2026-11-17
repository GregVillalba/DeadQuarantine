using System;
using UnityEngine;

public enum WeaponAttachmentKind : byte { Scope, Support, Utility, Muzzle }

[Serializable]
public struct WeaponAttachmentChoice : IEquatable<WeaponAttachmentChoice>
{
    public string scope, support, utility, muzzle;
    public string Get(WeaponAttachmentKind kind) => kind switch {
        WeaponAttachmentKind.Scope=>scope, WeaponAttachmentKind.Support=>support,
        WeaponAttachmentKind.Utility=>utility, _=>muzzle } ?? "";
    public void Set(WeaponAttachmentKind kind,string id) {
        switch(kind){case WeaponAttachmentKind.Scope:scope=id;break;case WeaponAttachmentKind.Support:support=id;break;
            case WeaponAttachmentKind.Utility:utility=id;break;case WeaponAttachmentKind.Muzzle:muzzle=id;break;}
    }
    public bool Equals(WeaponAttachmentChoice other) {
        foreach(WeaponAttachmentKind k in Enum.GetValues(typeof(WeaponAttachmentKind)))if(Get(k)!=other.Get(k))return false;
        return true;
    }
}

[CreateAssetMenu(menuName="Dead Quarantine/Weapon attachment catalog")]
public sealed class WeaponAttachmentCatalog : ScriptableObject
{
    [Serializable] public sealed class WeaponIcon {public string weapon;public Sprite icon;public Vector2 offset;}
    [Serializable] public sealed class WeaponMount {public string weapon;public Vector3 position;}
    [Serializable] public sealed class Entry {
        public string id, displayName, socket;
        public WeaponAttachmentKind kind;
        public GameObject model;
        public Sprite icon;
        public WeaponIcon[] weaponIcons=Array.Empty<WeaponIcon>();
        public string[] weapons=Array.Empty<string>();
        public bool magnifiedLens;
        public float lensFieldOfView=4.25f;
        public float zoom;
        public float traditionalZoom;
        public float LensFieldOfView(float hipFov, bool pvpve = true) {
            float magnification = !pvpve && traditionalZoom > 1 ? traditionalZoom : zoom;
            return magnification > 1 ? 2 * Mathf.Atan(Mathf.Tan(hipFov * .5f * Mathf.Deg2Rad) /
                magnification) * Mathf.Rad2Deg : lensFieldOfView;
        }
        public string StatDescription {
            get {
                var lines = new System.Collections.Generic.List<string>();
                void Percent(string label, float value, bool lowerIsBetter, bool conditional = false) {
                    if (Mathf.Abs(value - 1) < .0001f) return;
                    bool good = lowerIsBetter ? value < 1 : value > 1;
                    float change = label == "Estabilidad al apuntar" ? (1 - value) * 100 : (value - 1) * 100;
                    string sign = change > 0 ? "+" : "";
                    string color = good ? "#48EB7A" : "#FF6452";
                    lines.Add($"<color={color}>{sign}{change:0.#}% {label}</color>" +
                        (conditional && requiresBracing ? " · agachado, ADS e inmóvil" : ""));
                }
                if (zoom > 1) lines.Add($"<color=#48EB7A>Zoom {zoom:0.#}×</color>");
                Percent("Tiempo ADS", adsDuration, true);
                Percent("Estabilidad al apuntar", adsSway, true, true);
                Percent("Retroceso", recoil, true, true);
                Percent("Retroceso vertical", verticalRecoil, true, true);
                Percent("Retroceso horizontal", horizontalRecoil, true, true);
                Percent("Dispersión desde cadera", spread, true);
                Percent("Alcance de daño", range, false);
                Percent("Movilidad", mobility, false);
                Percent("Movimiento apuntando", adsMovement, false);
                if (suppressed) lines.Add("<color=#48EB7A>El disparo no revela en el minimapa</color>");
                if (laser) lines.Add("<color=#FF6452>Láser visible para enemigos</color>");
                if (flashlight) lines.Add("<color=#48EB7A>Ilumina zonas oscuras</color>\n<color=#FF6452>Luz visible para enemigos</color>");
                return string.Join("\n", lines);
            }
        }
        public float damage=1, spread=1, recoil=1, aimFov=50;
        public float range=1,adsDuration=1,mobility=1,adsMovement=1,adsSway=1,incomingShake=1;
        public float verticalRecoil=1,horizontalRecoil=1;
        [Range(0,1)] public float opticalHeight=.65f;
        public bool suppressed, requiresBracing, laser, flashlight;
        public AudioClip shotSound;
        public WeaponMount[] mounts=Array.Empty<WeaponMount>();
        public Vector3 MountPosition(string weapon)=>Array.Find(mounts,m=>m.weapon==weapon)?.position??Vector3.zero;
        public bool supportHandPose;
        public SupportHandPoseData firstPersonHand,thirdPersonHand;
        // Data only: progression remains outside phase 8.
        public int requiredLevel;
    }
    public Entry[] entries=Array.Empty<Entry>();
    public string[] eligibleWeapons=Array.Empty<string>();
    public GameObject[] mountingHardware=Array.Empty<GameObject>();
    public Material laserMaterial, transparentLens, reticleMaterial, scopeLensMaterial, scopeReticleMaterial;
    public Entry Find(string id)=>string.IsNullOrEmpty(id)?null:Array.Find(entries,e=>e.id==id);
    // Shared by server, presentation and statistics; bracing never removes permanent penalties.
    public float Modifier(WeaponAttachmentChoice choice,Func<Entry,float> select,bool braced=true,bool conditional=false){
        float result=1;
        foreach(WeaponAttachmentKind kind in Enum.GetValues(typeof(WeaponAttachmentKind))){
            var e=Find(choice.Get(kind));
            if(e==null || conditional && e.requiresBracing && !braced)continue;
            float factor=select(e);
            result*=float.IsFinite(factor)&&factor>=0 ? Mathf.Clamp(factor,0,5) : 1f;
        }
        return Mathf.Clamp(result,0,5);
    }
    public bool Eligible(string weapon)=>Array.IndexOf(eligibleWeapons,weapon)>=0;
    public bool Allows(string weapon,WeaponAttachmentKind kind,string id) {
        if(!Eligible(weapon))return false;
        if(string.IsNullOrEmpty(id))return true;
        var e=Find(id);return e!=null&&e.kind==kind&&e.model&&Array.IndexOf(e.weapons,weapon)>=0;
    }
    public bool Validate(string weapon,WeaponAttachmentChoice choice) {
        foreach(WeaponAttachmentKind k in Enum.GetValues(typeof(WeaponAttachmentKind)))if(!Allows(weapon,k,choice.Get(k)))return false;
        return true;
    }
}
