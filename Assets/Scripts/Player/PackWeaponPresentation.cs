using UnityEngine;
[DefaultExecutionOrder(1200)]
public sealed class PackWeaponPresentation:MonoBehaviour {
 public bool preserveLegacyAnimation;
 public Matrix4x4 aimPhysicalToCamera=Matrix4x4.identity,cameraToRigParent=Matrix4x4.identity;
 public int[] firstPersonRestStates=System.Array.Empty<int>();
 public bool supportPoseEnabled,bareSupportPoseEnabled;
 public SupportHandPoseData bareFirstPerson,bareThirdPerson;
 public bool alignIronSightLine;
 public Vector3 ironRear,ironFront,ironAimOffset;
 public Quaternion ironAimRotation=Quaternion.identity;
 public bool AuthoredRecoil=>!preserveLegacyAnimation;
 Weapon weapon;WeaponSwitcher switcher;WeaponAttachments attachments;Transform socket;WeaponSupportPose.Binding arm;
 public SupportHandPoseData SupportPose(WeaponAttachments selected,bool thirdPerson){
  if(!supportPoseEnabled)return null;
  var entry=selected&&selected.Catalog?selected.Catalog.Find(selected.ApprovedChoice.support):null;
  if(entry!=null&&entry.supportHandPose)return thirdPerson?entry.thirdPersonHand:entry.firstPersonHand;
  return bareSupportPoseEnabled?(thirdPerson?bareThirdPerson:bareFirstPerson):null;
 }
 public Transform SupportFrame(WeaponAttachments selected,Transform fallback){
  var entry=selected&&selected.Catalog?selected.Catalog.Find(selected.ApprovedChoice.support):null;
  return entry!=null&&entry.supportHandPose?WeaponAttachments.Find(selected.transform,"DQ_ATT_"+entry.id)??fallback:fallback;
 }
 void LateUpdate(){
  if(!weapon){weapon=GetComponent<Weapon>();switcher=GetComponentInParent<WeaponSwitcher>();attachments=GetComponent<WeaponAttachments>();socket=WeaponAttachments.Find(transform,"SOCKET_Grip");}
  if(!weapon||!weapon.isActiveAndEnabled||weapon.IsReloading||weapon.IsChambering||switcher&&switcher.IsSwitching)return;
  var pose=SupportPose(attachments,false);if(pose==null||!switcher)return;
  arm??=new WeaponSupportPose.Binding(switcher.GetComponent<Animator>());arm.Apply(SupportFrame(attachments,socket),pose);
 }
}
