using UnityEngine;
public partial class Weapon {
 PackWeaponPresentation packPresentation;Transform packOptic,originalAttachmentSight;bool attachmentSightCaptured;
 Quaternion packAppliedRotation=Quaternion.identity,packLastRotation;
 Vector3 packAimOffset,packAppliedOffset,packLastWritten;bool packOpticCalibrated;
 public Transform AttachmentDefaultSight=>attachmentSightCaptured?originalAttachmentSight:adsSightReference;
 bool UsePackAim=>packPresentation&&(!packPresentation.preserveLegacyAnimation||RuntimeAttachments!=null&&!string.IsNullOrEmpty(RuntimeAttachments.ApprovedChoice.scope));
 bool UseAuthoredRecoil=>packPresentation&&packPresentation.AuthoredRecoil;
 void SetPackAttachmentSight(Transform sight){
  if(!attachmentSightCaptured){originalAttachmentSight=adsSightReference;attachmentSightCaptured=true;}
  adsSightReference=sight?sight:originalAttachmentSight;packOpticCalibrated=false;
 }
 static bool SamePackRotation(Quaternion a,Quaternion b)=>Mathf.Abs(a.x-b.x)+Mathf.Abs(a.y-b.y)+Mathf.Abs(a.z-b.z)+Mathf.Abs(a.w-b.w)<.0000001f;
 void RestorePackAim(){
  if(weaponAnimator&&packAppliedOffset!=Vector3.zero&&(weaponAnimator.transform.localPosition-packLastWritten).sqrMagnitude<.00000001f)weaponAnimator.transform.localPosition-=packAppliedOffset;
  if(weaponAnimator&&SamePackRotation(weaponAnimator.transform.localRotation,packLastRotation))weaponAnimator.transform.localRotation=Quaternion.Inverse(packAppliedRotation)*weaponAnimator.transform.localRotation;
  packAppliedRotation=Quaternion.identity;packAppliedOffset=Vector3.zero;
 }
 void ApplyPackAim(){
  RestorePackAim();if(!weaponAnimator||!adsSightReference||!weaponCamera||aimingBlend<=.001f)return;
  if(!packOpticCalibrated||packOptic!=adsSightReference){
   var physical=WeaponAttachments.Find(transform,"root");if(!physical)return;
   var point=packPresentation.aimPhysicalToCamera.MultiplyPoint3x4(physical.InverseTransformPoint(adsSightReference.position));
   packAimOffset=packPresentation.cameraToRigParent.MultiplyVector(new Vector3(-point.x,-point.y,0));packOptic=adsSightReference;packOpticCalibrated=true;
  }
  bool iron=packPresentation.alignIronSightLine&&(RuntimeAttachments==null||string.IsNullOrEmpty(RuntimeAttachments.ApprovedChoice.scope));
  packAppliedRotation=iron?Quaternion.Slerp(Quaternion.identity,packPresentation.ironAimRotation,Mathf.Clamp01(aimingBlend)):Quaternion.identity;
  weaponAnimator.transform.localRotation=packAppliedRotation*weaponAnimator.transform.localRotation;packLastRotation=weaponAnimator.transform.localRotation;
  packAppliedOffset=(iron?packPresentation.ironAimOffset:packAimOffset)*Mathf.Clamp01(aimingBlend);weaponAnimator.transform.localPosition+=packAppliedOffset;packLastWritten=weaponAnimator.transform.localPosition;
 }
}
