using System.Collections.Generic;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace DeadQuarantine.PvPvE {
 public enum PvPvEZombieKind:byte {Walker,Runner,Dog}
 // Adapted from the former director and Map.TryZombieSpawn; existing AI/Health remain authoritative.
 public sealed class PvPvEZombieDirector:MonoBehaviour {
  public static PvPvEZombieDirector Active{get;private set;}
  readonly List<ZombieHealth> spawned=new();readonly HashSet<ZombieHealth> deaths=new();
  double nextSpawn;PvPvECombat combat;NetworkManager network;readonly List<GameObject> clientHandlers=new();
  public int AliveCount {get{spawned.RemoveAll(z=>!z||!z.IsSpawned);deaths.RemoveWhere(z=>!z||!z.IsSpawned);return spawned.Count(z=>!z.IsDead);}}
  public IReadOnlyList<ZombieHealth> Spawned=>spawned;
  public Transform[] SpawnZones; // Optional override. Otherwise uses the active original scenario's zones.
  void Awake(){Active=this;combat=GetComponent<PvPvECombat>();network=combat.NetworkManager;
   if(combat.IsClient&&!combat.IsServer)foreach(var prefab in new[]{combat.Settings.WalkerPrefab,combat.Settings.DogPrefab})if(prefab&&!clientHandlers.Contains(prefab)&&network.PrefabHandler.AddHandler(prefab,new ClientZombie(prefab)))clientHandlers.Add(prefab);
  }
  // Instantiate below an inactive parent so the prefab's agent never enables on a client.
  // This preserves the original prefab instead of copying or mutating its asset.
  sealed class ClientZombie:INetworkPrefabInstanceHandler {
   readonly GameObject prefab;public ClientZombie(GameObject prefab){this.prefab=prefab;}
   public NetworkObject Instantiate(ulong ownerClientId,Vector3 position,Quaternion rotation){
    var gate=new GameObject("PvPvE client zombie setup");gate.SetActive(false);
    var go=Object.Instantiate(prefab,position,rotation,gate.transform);var agent=go.GetComponent<NavMeshAgent>();if(agent)agent.enabled=false;
    go.transform.SetParent(null,true);Object.Destroy(gate);return go.GetComponent<NetworkObject>();
   }
   public void Destroy(NetworkObject instance){if(instance)Object.Destroy(instance.gameObject);}
  }
  void Update(){
   if(!combat||!combat.IsServer||!combat.IsSpawned)return;
   if(!combat.IsPlaying){if(spawned.Count>0)Clear();return;}
   double now=combat.NetworkManager.ServerTime.Time;if(now<nextSpawn)return;
   var c=combat.Settings;nextSpawn=now+Mathf.Max(.1f,c.ZombieSpawnInterval);
   if(AliveCount>=Mathf.Max(0,c.MaxZombiesAlive)||!TryZombieSpawn(out var point))return;
   var kind=ChooseKind(Random.value,c.WalkerChance,c.RunnerChance,c.DogChance);
   if(!kind.HasValue)return;var prefab=kind==PvPvEZombieKind.Dog?c.DogPrefab:c.WalkerPrefab;
   if(!prefab)return;
   var go=Instantiate(prefab,point,Quaternion.identity);var health=go.GetComponent<ZombieHealth>();
   if(!health||!go.GetComponent<NetworkObject>()||!go.GetComponent<ZombieAI>()){Destroy(go);return;}
   health.NetworkObject.Spawn(true);go.GetComponent<ZombieAI>().SetPvPvEKindServer(kind.Value);spawned.Add(health);
  }
  public static PvPvEZombieKind? ChooseKind(float sample,float walker,float runner,float dog){
   walker=float.IsFinite(walker)?Mathf.Max(0,walker):0;runner=float.IsFinite(runner)?Mathf.Max(0,runner):0;dog=float.IsFinite(dog)?Mathf.Max(0,dog):0;
   float total=walker+runner+dog;if(total<=0||!float.IsFinite(total)||!float.IsFinite(sample))return null;
   float pick=Mathf.Clamp(sample,0,.999999f)*total;return pick<walker?PvPvEZombieKind.Walker:pick<walker+runner?PvPvEZombieKind.Runner:PvPvEZombieKind.Dog;
  }
  public bool Owns(ZombieHealth zombie)=>zombie&&zombie.IsPvPvEZombie&&spawned.Contains(zombie);
  public bool ValidTarget(PlayerHealth health)=>combat&&combat.IsPlaying&&combat.Alive(health);
  public Transform Closest(Vector3 point){
   PlayerHealth closest=null;float distance=float.PositiveInfinity;
   foreach(var client in combat.NetworkManager.ConnectedClientsList){var health=client.PlayerObject?client.PlayerObject.GetComponent<PlayerHealth>():null;if(!ValidTarget(health))continue;float d=(health.transform.position-point).sqrMagnitude;if(d<distance){distance=d;closest=health;}}
   return closest?closest.transform:null;
  }
  public static bool Blocked(Vector3 from,Vector3 to){
   foreach(var hit in Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))if(!hit.collider.GetComponentInParent<PlayerHealth>()&&!hit.collider.GetComponentInParent<ZombieHealth>())return true;
   return false;
  }
  public bool TryZombieSpawn(out Vector3 point){
   point=default;var zones=SpawnZones!=null&&SpawnZones.Length>0?SpawnZones:PvPvEMapContext.Active?.Scenario?.SpawnsZombies;
   if(zones==null||zones.Length==0||!combat.IsPlaying)return false;
   var players=combat.NetworkManager.ConnectedClientsList.Select(c=>c.PlayerObject?c.PlayerObject.GetComponent<PlayerHealth>():null).Where(ValidTarget).ToArray();if(players.Length==0)return false;
   var prefab=combat.Settings.WalkerPrefab;int agentType=prefab&&prefab.GetComponent<NavMeshAgent>()?prefab.GetComponent<NavMeshAgent>().agentTypeID:0;
   var filter=new NavMeshQueryFilter{agentTypeID=agentType,areaMask=NavMesh.AllAreas};var path=new NavMeshPath();
   foreach(var zone in zones.OrderBy(_=>Random.value)){
    if(!zone||!zone.gameObject.activeInHierarchy)continue;var offset=Random.insideUnitCircle*2;
    if(!NavMesh.SamplePosition(zone.position+new Vector3(offset.x,0,offset.y),out var hit,3,filter)||Mathf.Abs(hit.position.y-zone.position.y)>3)continue;
    bool safe=true,reachable=false;
    foreach(var player in players){var position=player.transform.position;float distance=Vector3.Distance(hit.position,position);float min=Mathf.Max(2,combat.Settings.ZombieSafeDistance);
     if(distance<min||distance<min*2&&Vector3.Dot(player.transform.forward,(hit.position-position).normalized)>.5f&&!Blocked(position+Vector3.up*1.5f,hit.position+Vector3.up)){safe=false;break;}
     if(!reachable&&NavMesh.SamplePosition(position,out var target,2,filter)&&NavMesh.CalculatePath(hit.position,target.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete)reachable=true;
    }
    if(safe&&reachable){point=hit.position;return true;}
   }
   return false;
  }
  public void ZombieDied(ZombieHealth zombie,ulong attacker){
   if(!combat||!combat.IsServer||!combat.IsPlaying||!Owns(zombie)||!zombie.IsDead||!deaths.Add(zombie))return;
   combat.RecordZombieKillServer(attacker,zombie);
  }
  public void Clear(){
   if(!combat||!combat.NetworkManager||!combat.NetworkManager.IsServer)return;
   foreach(var zombie in spawned.ToArray())if(zombie&&zombie.IsSpawned)zombie.NetworkObject.Despawn(true);
   spawned.Clear();deaths.Clear();nextSpawn=0;
  }
  void OnDestroy(){Clear();if(network)foreach(var prefab in clientHandlers)network.PrefabHandler.RemoveHandler(prefab);clientHandlers.Clear();if(Active==this)Active=null;}
 }
}