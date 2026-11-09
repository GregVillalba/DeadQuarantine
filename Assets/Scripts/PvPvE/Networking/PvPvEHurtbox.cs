using UnityEngine;
namespace DeadQuarantine.PvPvE.Networking {
 // Trigger used by authoritative rays even when a remote CharacterController is disabled.
 public sealed class PvPvEHurtbox:MonoBehaviour {
  PlayerHealth health;CharacterController controller;GameObject root;CapsuleCollider capsule;NetworkWeaponPresentation presentation;
  public bool Contains(Collider collider)=>collider==capsule;
  public float HeadThreshold=>capsule.bounds.max.y-capsule.radius*.9f;
  void Awake(){health=GetComponent<PlayerHealth>();controller=GetComponent<CharacterController>();presentation=GetComponent<NetworkWeaponPresentation>();root=new GameObject("PvPvE hurtbox");root.transform.SetParent(transform,false);capsule=root.AddComponent<CapsuleCollider>();capsule.isTrigger=true;}
  void LateUpdate(){
   var combat=PvPvECombat.Active;capsule.enabled=combat&&combat.Alive(health);if(!controller)return;
   float height=controller.height;var animator=presentation?presentation.BodyAnimator:null;
   if(animator&&animator.isHuman&&animator.GetBool("TP_IsCrouching")){var head=animator.GetBoneTransform(HumanBodyBones.Head);if(head)height=Mathf.Clamp(transform.InverseTransformPoint(head.position).y+.18f,controller.radius*2,controller.height);}
   capsule.center=new Vector3(controller.center.x,height*.5f,controller.center.z);capsule.height=height;capsule.radius=controller.radius;
  }
  void OnDestroy(){if(root)Destroy(root);}
 }
}
