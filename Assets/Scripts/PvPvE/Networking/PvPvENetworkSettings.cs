using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking
{
    [System.Serializable] public sealed class PvPvEMapOption
    {
        public ConfiguracionPartidaSeleccionada.Escenario Id;
        public string Name;
        public Texture2D Preview,Minimap;
        public Texture2D[] LoadingViews=System.Array.Empty<Texture2D>();
        public Vector2 MinimapMin,MinimapMax;
    }
    [CreateAssetMenu(menuName="Dead Quarantine/PvPvE/Network settings")]
    public sealed class PvPvENetworkSettings : ScriptableObject
    {
        [Range(1,5)] public int PlayersPerTeam=5;
        [Min(1)] public int ProtocolVersion=5001;
        public string SessionPool="dq.pvpve.tdm";
        [Min(1)] public float SearchSeconds=4;
        [Min(5)] public float ConnectionSeconds=25;
        [Min(3)] public float ShutdownSeconds=10;
        public GameObject RosterPrefab;
        public CharacterCatalog Characters;
        public PvPvELoadoutCatalog Loadouts;
        public GameObject PlayerPrefab;
        public Unity.Netcode.NetworkPrefabsList GamePrefabs;
        public string MatchScene="PvPvEMatch";
        [Min(10)] public float MapLoadSeconds=180, PlayerPrepareSeconds=120;
        [Min(1)] public float PreMatchSeconds=5;
        [Min(1)] public float RespawnSeconds=10;
        [Min(0)] public float CorpseSeconds=3;
        [Min(5)] public float DeploymentSeconds=30;
        [Min(0)] public float SpawnProtectionSeconds=2;
        [Min(1)] public float SafeSpawnDistance=12;
        [Min(.5f)] public float SpawnSpacing=2;
        [Min(1)] public int KillLimit=50,KillScore=100;
        [Min(1)] public float HeadshotMultiplier=2,LongshotDistance=30;
        [Range(0,1)] public float HeadshotScoreBonus=.5f,PlayerLongshotScoreBonus=.25f,ZombieLongshotScoreBonus=.2f;
        [Min(1)] public float PistolLongshotDistance=20,ShotgunLongshotDistance=15,SmgLongshotDistance=25,RifleLongshotDistance=30,SniperLongshotDistance=50;
        public float LongshotThreshold(PvPvELoadoutCatalog.Gun gun)=>gun==null?LongshotDistance:gun.family switch{PvPvELoadoutCatalog.Gun.Family.Pistola=>PistolLongshotDistance,PvPvELoadoutCatalog.Gun.Family.Escopeta=>ShotgunLongshotDistance,PvPvELoadoutCatalog.Gun.Family.Subfusil=>SmgLongshotDistance,PvPvELoadoutCatalog.Gun.Family.Sniper=>SniperLongshotDistance,_=>RifleLongshotDistance};
        public (double Base,double Head,double Long,double Total) EliminationAward(bool zombie,bool head,bool far){double basis=System.Math.Max(0,zombie?ZombieKillScore:KillScore);double h=head?basis*(double)(decimal)HeadshotScoreBonus:0;double l=far?basis*(double)(decimal)(zombie?ZombieLongshotScoreBonus:PlayerLongshotScoreBonus):0;return(basis,h,l,basis+h+l);}
        [Min(.05f)] public float ShotOriginTolerance=.8f;
        [Range(0,.25f)] public float ReloadTimingGrace=.1f;
        [Min(0)] public int AssistScore=50;
        [Min(1)] public int MinimumAssistDamage=15;
        [Min(.1f)] public float DamageAssistWindow=10,FlashAssistWindow=8,StunAssistWindow=8,UtilityAssistWindow=8;
        public float ContributionWindow(ContributionType type)=>type==ContributionType.Damage?DamageAssistWindow:type==ContributionType.Flash?FlashAssistWindow:type==ContributionType.Stun?StunAssistWindow:UtilityAssistWindow;
        public bool FriendlyFire=false;
        [Min(10)] public float MinimapRange=40;
        [Min(.1f)] public float EnemyShotRevealSeconds=3;
        [Min(1)] public float ResultsSeconds=12;
        [Min(1)] public float MatchDurationSeconds=600,KillFeedSeconds=6;
        [Range(1,10)] public int KillFeedRows=5;
        public AudioClip KillSound;
        [Header("PvPvE zombies")]
        [Min(0)] public int MaxZombiesAlive=10,ZombieKillScore=10;
        [Min(.1f)] public float ZombieSpawnInterval=2;
        [Min(0)] public float WalkerChance=80,RunnerChance=15,DogChance=5;
        [Min(2)] public float ZombieSafeDistance=15;
        public GameObject WalkerPrefab,DogPrefab;
        [Min(1)] public float MapVoteSeconds=15;
        [Min(1)] public float PostVoteSeconds=10;
        public PvPvEMapOption[] Maps=System.Array.Empty<PvPvEMapOption>();
        public bool HasValidMaps {
            get {
                if(Maps==null || Maps.Length!=3)return false;
                var ids=new System.Collections.Generic.HashSet<ConfiguracionPartidaSeleccionada.Escenario>();
                foreach(var map in Maps)if(map==null || map.Id==ConfiguracionPartidaSeleccionada.Escenario.Ninguno || string.IsNullOrWhiteSpace(map.Name) || !map.Preview || !ids.Add(map.Id))return false;
                return true;
            }
        }
        public bool IsValidCharacter(int index)=>Characters && index>=0 && index<Characters.VariantCount && Characters.GetVariant(index)?.modelPrefab;
        public int TeamCapacity=>Mathf.Clamp(PlayersPerTeam,1,5);
        public int Capacity=>TeamCapacity*2;
        public string Compatibility=>ProtocolVersion+"-"+Capacity;
    }
}
