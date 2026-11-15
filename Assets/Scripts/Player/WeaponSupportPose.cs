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
  public Binding(Animator animator, bool right = false){
   if(animator.isHuman)
   {
    upper=animator.GetBoneTransform(right?HumanBodyBones.RightUpperArm:HumanBodyBones.LeftUpperArm);
    lower=animator.GetBoneTransform(right?HumanBodyBones.RightLowerArm:HumanBodyBones.LeftLowerArm);
    hand=animator.GetBoneTransform(right?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
   }
   else
   {
    string suffix=right?"_r":"_l";
    foreach(var t in animator.GetComponentsInChildren<Transform>(true))
    {
     if(t.name.Equals("upperarm"+suffix,StringComparison.OrdinalIgnoreCase))upper=t;
     else if(t.name.Equals("lowerarm"+suffix,StringComparison.OrdinalIgnoreCase))lower=t;
     else if(t.name.Equals("hand"+suffix,StringComparison.OrdinalIgnoreCase))hand=t;
    }
   }
   if(hand)foreach(var t in hand.GetComponentsInChildren<Transform>(true))if(t!=hand)fingers.TryAdd(t.name,t);
  }
  public bool Capture(Transform frame, SupportHandPoseData pose)
  {
   if(!frame||!upper||!lower||!hand)return false;
   pose.position=frame.InverseTransformPoint(hand.position);
   pose.elbow=frame.InverseTransformPoint(lower.position);
   pose.rotation=Quaternion.Inverse(frame.rotation)*hand.rotation;
   if(pose.fingers.Length==0 && fingers.Count>0)
   {
    pose.fingers=new SupportFingerPose[fingers.Count];
    int index=0;
    foreach(var finger in fingers)pose.fingers[index++]=new SupportFingerPose{bone=finger.Key};
   }
   foreach(var finger in pose.fingers)finger.rotation=fingers[finger.bone].localRotation;
   return true;
  }

  public void Apply(Transform socket,SupportHandPoseData pose,float weight=1f){
   if(!socket||pose==null||!upper||!lower||!hand)return;
   Vector3 target=Vector3.Lerp(hand.position,socket.TransformPoint(pose.position),weight);
   Vector3 hint=Vector3.Lerp(lower.position,socket.TransformPoint(pose.elbow),weight);
   Quaternion handRotation=Quaternion.Slerp(hand.rotation,socket.rotation*pose.rotation,weight);
   Vector3 origin=upper.position;
   float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,hand.position);if(a<.001f||b<.001f)return;
   Vector3 ray=target-origin;float distance=Mathf.Clamp(ray.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);if(ray.sqrMagnitude<.000001f)return;
   Vector3 direction=ray.normalized,pole=Vector3.ProjectOnPlane(hint-origin,direction);if(pole.sqrMagnitude<.000001f)pole=Vector3.ProjectOnPlane(lower.position-origin,direction);if(pole.sqrMagnitude<.000001f)pole=Vector3.Cross(direction,socket.right);
   float cosine=Mathf.Clamp((a*a+distance*distance-b*b)/(2*a*distance),-1,1);
   Vector3 elbow=origin+direction*(cosine*a)+pole.normalized*(Mathf.Sqrt(1-cosine*cosine)*a);
   upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
   lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,origin+direction*distance-lower.position)*lower.rotation;
   hand.rotation=handRotation;
   foreach(var finger in pose.fingers)if(fingers.TryGetValue(finger.bone,out var t))t.localRotation=Quaternion.Slerp(t.localRotation,finger.rotation,weight);
  }
 }
}
