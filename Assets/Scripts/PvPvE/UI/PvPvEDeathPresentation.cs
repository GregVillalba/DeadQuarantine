using System.Collections.Generic;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using UnityEngine;

namespace DeadQuarantine.PvPvE.UI {
 // Cosmetic local corpses never become independently simulated gameplay entities.
 public sealed class PvPvEDeathPresentation:MonoBehaviour {
  PvPvETeamDeployment deployment;readonly HashSet<(ulong Client,uint Life)> seen=new();readonly List<GameObject> corpses=new();Camera floating;
  public Camera FloatingCamera=>floating;
  public bool OwnsCollider(Collider collider)=>corpses.Any(c=>c&&collider.transform.IsChildOf(c.transform));
  public int VisibleCorpses=>corpses.Count(c=>c);
  public int RagdollBodies=>corpses.Where(c=>c).Sum(c=>c.GetComponentsInChildren<Rigidbody>().Length);
  void Awake(){deployment=GetComponent<PvPvETeamDeployment>();}
  void LateUpdate(){
   if(!deployment||!deployment.IsSpawned)return;
   if(PvPvEPreparationFlow.Active&&!PvPvEPreparationFlow.Active.InPreparation){ClearForLobby();return;}
   foreach(var row in deployment.Players){
    if(row.Life!=PvPvELifeStage.Dead||!deployment.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(row.ObjectId,out var player))continue;
    if(!seen.Contains((row.ClientId,row.Revision))){
     var presentation=player.GetComponent<NetworkWeaponPresentation>();if(presentation&&presentation.Body&&presentation.BodyAnimator&&presentation.BodyAnimator.isHuman){var corpse=CreateCorpse(presentation);seen.Add((row.ClientId,row.Revision));corpses.Add(corpse);Destroy(corpse,Mathf.Clamp(deployment.Settings.CorpseSeconds,0,deployment.Settings.RespawnSeconds));presentation.Body.gameObject.SetActive(false);}
    }
    if(!player.IsOwner)continue;
    player.GetComponent<PlayerMovement>().enabled=false;var cc=player.GetComponent<CharacterController>();if(cc)cc.enabled=false;
    foreach(var camera in player.GetComponentsInChildren<Camera>(true))camera.enabled=false;
    foreach(var listener in player.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
    if(!floating){
     floating=new GameObject("PvPvE floating death camera").AddComponent<Camera>();floating.gameObject.AddComponent<AudioListener>();
     var source=player.GetComponentsInChildren<Camera>(true).FirstOrDefault(c=>c.tag=="MainCamera")??player.GetComponentInChildren<Camera>(true);
     if(source){floating.CopyFrom(source);floating.enabled=true;}
     var origin=player.transform.position+Vector3.up*1.2f;
     Vector3 cameraPosition=origin;float best=float.NegativeInfinity;
     // A blocked rear view should use an open side, rather than retreat inside the corpse.
     for(int angle=0;angle<360;angle+=45){
      var desired=player.transform.position+Vector3.up*2-Quaternion.Euler(0,angle,0)*player.transform.forward*3;
      var direction=desired-origin;float distance=direction.magnitude;
      var obstacles=Physics.SphereCastAll(origin,.2f,direction.normalized,distance,~0,QueryTriggerInteraction.Ignore)
       .Where(h=>!h.collider.GetComponentInParent<PlayerHealth>()&&!corpses.Any(c=>c&&h.collider.transform.IsChildOf(c.transform))).OrderBy(h=>h.distance).ToArray();
      if(obstacles.Length>0)distance=Mathf.Max(.05f,obstacles[0].distance-.1f);
      float score=distance+(angle==0?.15f:0);if(score>best){best=score;cameraPosition=origin+direction.normalized*distance;}
     }
     floating.nearClipPlane=.05f;floating.transform.position=cameraPosition;
     floating.transform.LookAt(player.transform.position+Vector3.up*.6f);
    }
   }
   if(floating&&(!deployment.TryGet(deployment.NetworkManager.LocalClientId,out var local)||local.Life!=PvPvELifeStage.Dead)){Destroy(floating.gameObject);floating=null;}
   corpses.RemoveAll(c=>!c);
  }
  static GameObject CreateCorpse(NetworkWeaponPresentation source){
   var root=new GameObject("PvPvE cosmetic ragdoll");var map=new Dictionary<Transform,Transform>();
   CopyTransforms(source.Body,root.transform,map);
   foreach(var skin in source.Body.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    if(!skin.enabled||!skin.gameObject.activeInHierarchy||!skin.sharedMesh)continue;
    var renderer=map[skin.transform].gameObject.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=skin.sharedMesh;renderer.sharedMaterials=skin.sharedMaterials;
    renderer.bones=skin.bones.Select(b=>b&&map.TryGetValue(b,out var bone)?bone:null).ToArray();renderer.rootBone=skin.rootBone&&map.TryGetValue(skin.rootBone,out var rb)?rb:null;renderer.localBounds=skin.localBounds;renderer.updateWhenOffscreen=true;renderer.forceRenderingOff=false;
   }
   // Polygon variants also attach rigid meshes (helmet, glasses, boots, packs) to bones.
   foreach(var mesh in source.Body.GetComponentsInChildren<MeshRenderer>(true)){
    if(!mesh.enabled||!mesh.gameObject.activeInHierarchy||source.VisibleWeapon&&mesh.transform.IsChildOf(source.VisibleWeapon.transform))continue;
    var filter=mesh.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
    var t=map[mesh.transform];t.gameObject.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=mesh.sharedMaterials;renderer.forceRenderingOff=false;
   }
   var animator=source.BodyAnimator;var bodies=new Dictionary<Transform,Rigidbody>();
   var bones=new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg};
   if(animator&&animator.isHuman)foreach(var bone in bones){var original=animator.GetBoneTransform(bone);if(!original||!map.TryGetValue(original,out var t))continue;
    float scale=Mathf.Max(.001f,Mathf.Max(Mathf.Abs(t.lossyScale.x),Mathf.Max(Mathf.Abs(t.lossyScale.y),Mathf.Abs(t.lossyScale.z))));
    var next=bone switch{HumanBodyBones.Hips=>HumanBodyBones.Spine,HumanBodyBones.Spine=>HumanBodyBones.Chest,HumanBodyBones.Chest=>HumanBodyBones.Neck,HumanBodyBones.LeftUpperArm=>HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftLowerArm=>HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm=>HumanBodyBones.RightLowerArm,HumanBodyBones.RightLowerArm=>HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg=>HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftLowerLeg=>HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg=>HumanBodyBones.RightLowerLeg,HumanBodyBones.RightLowerLeg=>HumanBodyBones.RightFoot,_=>HumanBodyBones.LastBone};
    var end=next!=HumanBodyBones.LastBone?animator.GetBoneTransform(next):null;var delta=end?t.InverseTransformPoint(end.position):Vector3.zero;
    var collider=t.gameObject.AddComponent<CapsuleCollider>();collider.radius=(bone==HumanBodyBones.Head?.12f:.085f)/scale;collider.center=delta*.5f;
    collider.direction=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)&&Mathf.Abs(delta.x)>Mathf.Abs(delta.z)?0:Mathf.Abs(delta.z)>Mathf.Abs(delta.y)?2:1;
    collider.height=Mathf.Max(collider.radius*2,delta.magnitude+collider.radius);
    var body=t.gameObject.AddComponent<Rigidbody>();body.mass=bone==HumanBodyBones.Hips?8:2;body.linearDamping=.4f;body.angularDamping=1;body.maxAngularVelocity=8;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;bodies[t]=body;
   }
   foreach(var pair in bodies){var parent=pair.Key.parent;while(parent&&!bodies.ContainsKey(parent))parent=parent.parent;if(!parent)continue;
    var joint=pair.Key.gameObject.AddComponent<CharacterJoint>();joint.connectedBody=bodies[parent];joint.enableProjection=true;joint.lowTwistLimit=new SoftJointLimit{limit=-25};joint.highTwistLimit=new SoftJointLimit{limit=25};joint.swing1Limit=new SoftJointLimit{limit=35};joint.swing2Limit=new SoftJointLimit{limit=25};
   }
   var colliders=root.GetComponentsInChildren<Collider>();for(int i=0;i<colliders.Length;i++)for(int j=i+1;j<colliders.Length;j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
   // A cosmetic corpse must not shove a live owner-authoritative player on just one peer.
   foreach(var player in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))if(player.GetComponent<CharacterController>() is { } controller)foreach(var collider in colliders)Physics.IgnoreCollision(collider,controller);
   var hips=animator&&animator.isHuman?animator.GetBoneTransform(HumanBodyBones.Hips):null;if(hips&&map.TryGetValue(hips,out var hip)&&bodies.TryGetValue(hip,out var pelvis)){pelvis.AddForce(source.transform.forward*.8f,ForceMode.VelocityChange);pelvis.AddTorque(source.transform.right*1.2f,ForceMode.VelocityChange);}
   return root;
  }
  static void CopyTransforms(Transform source,Transform parent,Dictionary<Transform,Transform> map){
   var t=new GameObject(source.name).transform;t.SetParent(parent,false);t.SetPositionAndRotation(source.position,source.rotation);t.localScale=source.lossyScale;map[source]=t;
   // Set world scale only for the first node; descendants keep their authored local scale.
   if(map.TryGetValue(source.parent,out var copiedParent)){t.SetParent(copiedParent,false);t.localPosition=source.localPosition;t.localRotation=source.localRotation;t.localScale=source.localScale;}
   foreach(Transform child in source)CopyTransforms(child,t,map);
  }
  public void ClearForLobby(){seen.Clear();foreach(var corpse in corpses)if(corpse)Destroy(corpse);corpses.Clear();if(floating){floating.gameObject.SetActive(false);Destroy(floating.gameObject);floating=null;}}
  void OnDestroy(){if(floating)Destroy(floating.gameObject);foreach(var corpse in corpses)if(corpse)Destroy(corpse);}
 }
}
