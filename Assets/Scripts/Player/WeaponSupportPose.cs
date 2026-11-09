using System;using System.Collections.Generic;using UnityEngine;
[Serializable] public sealed class SupportFingerPose {public string bone;public Quaternion rotation;}
[Serializable] public sealed class SupportHandPoseData {
 public Vector3 position,elbow;public Quaternion rotation=Quaternion.identity;
 public SupportFingerPose[] fingers=Array.Empty<SupportFingerPose>();
}
// A two-bone correction of the support arm only. The gun, trigger arm, camera,
// locomotion and action animations remain controlled by the existing Animators.
public static class WeaponSupportPose {
 public sealed class Binding {
  public Transform upper,lower,hand;public readonly Dictionary<string,Transform> fingers=new();
  public Binding(Animator animator){
   if(animator.isHuman){upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);}
   else{foreach(var t in animator.GetComponentsInChildren<Transform>(true)){switch(t.name.ToLowerInvariant()){case "upperarm_l":upper=t;break;case "lowerarm_l":lower=t;break;case "hand_l":hand=t;break;}}}
   if(hand)foreach(var t in hand.GetComponentsInChildren<Transform>(true))if(t!=hand)fingers.TryAdd(t.name,t);
  }
  public void Apply(Transform socket,SupportHandPoseData pose){
   if(!socket||pose==null||!upper||!lower||!hand)return;
   Vector3 target=socket.TransformPoint(pose.position),hint=socket.TransformPoint(pose.elbow),origin=upper.position;
   float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);if(a<.001f||b<.001f)return;
   Vector3 ray=target-origin;float distance=Mathf.Clamp(ray.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);if(ray.sqrMagnitude<.000001f)return;
   Vector3 direction=ray.normalized,pole=Vector3.ProjectOnPlane(hint-origin,direction);if(pole.sqrMagnitude<.000001f)pole=Vector3.ProjectOnPlane(lower.position-origin,direction);if(pole.sqrMagnitude<.000001f)pole=Vector3.Cross(direction,socket.right);
   float cosine=Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance),-1,1);
   Vector3 elbow=origin+direction*(cosine*a)+pole.normalized*(Mathf.Sqrt(1-cosine*cosine)*a);
   upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
   lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,origin+direction*distance-lower.position)*lower.rotation;
   hand.rotation=socket.rotation*pose.rotation;
   foreach(var finger in pose.fingers)if(fingers.TryGetValue(finger.bone,out var t))t.localRotation=finger.rotation;
  }
 }
}
