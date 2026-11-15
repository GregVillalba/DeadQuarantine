using UnityEngine;
[DefaultExecutionOrder(1200)]
public sealed class PackWeaponPresentation:MonoBehaviour {
 public bool preserveLegacyAnimation;
 public Matrix4x4 aimPhysicalToCamera=Matrix4x4.identity,cameraToRigParent=Matrix4x4.identity;
 public int[] firstPersonRestStates=System.Array.Empty<int>();
 public bool supportPoseEnabled,bareSupportPoseEnabled;
 private PlayerMovement movement;
 private Animator animator;
 private Transform weaponFrame;
 private WeaponSupportPose.Binding triggerArm;
 private readonly SupportHandPoseData sprintTriggerGrip = new();
 private readonly SupportHandPoseData sprintSupportGrip = new();
 [SerializeField] private SupportHandPoseData authoredSprintSupportGrip;
 [SerializeField] private SupportHandPoseData authoredSprintTriggerGrip;
 private bool HasAuthoredSprintGrip => attachments.StableWeaponId == "SMG02" &&
  authoredSprintSupportGrip?.fingers.Length > 0 && authoredSprintTriggerGrip?.fingers.Length > 0;
 private bool sprintGripsReady;
 private float sprintGripWeight;
 private string sprintGripAttachment;
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
 private bool KeepSprintGrip => !preserveLegacyAnimation && attachments &&
  attachments.StableWeaponId is "P90" or "SemiRifle" or "MAC10" or "Vector" or "SMG02";

 void LateUpdate(){
  if(!weapon){weapon=GetComponent<Weapon>();switcher=GetComponentInParent<WeaponSwitcher>();attachments=GetComponent<WeaponAttachments>();movement=GetComponentInParent<PlayerMovement>();socket=WeaponAttachments.Find(transform,"SOCKET_Grip");animator=switcher?switcher.GetComponent<Animator>():null;weaponFrame=WeaponAttachments.Find(transform,"root");}
  if(!weapon||!weapon.isActiveAndEnabled||weapon.IsReloading||weapon.IsChambering||switcher&&switcher.IsSwitching)
  {
   sprintGripWeight=0;
   return;
  }
  if (preserveLegacyAnimation)
  {
   if (movement && movement.IsSprinting) return;
   var legacyAnimator = switcher ? switcher.GetComponent<Animator>() : null;
   int layer = legacyAnimator ? legacyAnimator.GetLayerIndex("MovementActions") : -1;
   if (layer >= 0 && (legacyAnimator.GetCurrentAnimatorStateInfo(layer).IsName("Running") ||
       legacyAnimator.IsInTransition(layer) && legacyAnimator.GetNextAnimatorStateInfo(layer).IsName("Running"))) return;
  }
  var pose = SupportPose(attachments, false);
  if (!animator || (pose == null && !KeepSprintGrip)) return;
  arm ??= new WeaponSupportPose.Binding(animator);
  if (KeepSprintGrip && ApplySprintGrip(Time.deltaTime)) return;
  if (pose != null) arm.Apply(SupportFrame(attachments, socket), pose);
 }

 private bool ApplySprintGrip(float deltaTime)
 {
  if (!weaponFrame) return false;
  int layer = animator.GetLayerIndex("MovementActions");
  if (layer < 0) return false;
  bool transition = animator.IsInTransition(layer);
  bool running = animator.GetCurrentAnimatorStateInfo(layer).IsName("Running") ||
   transition && animator.GetNextAnimatorStateInfo(layer).IsName("Running");
  string support = attachments.ApprovedChoice.support;
  if (sprintGripAttachment != support)
  {
   sprintGripAttachment = support;
   sprintGripsReady = false;
  }

  if (running || sprintGripWeight > 0)
  {
   sprintGripWeight = Mathf.MoveTowards(sprintGripWeight, running ? 1f : 0f, deltaTime * 14f);
   bool authored=HasAuthoredSprintGrip;
   if ((!sprintGripsReady && !authored) || sprintGripWeight <= 0) return false;
   triggerArm ??= new WeaponSupportPose.Binding(animator, right:true);
   triggerArm.Apply(weaponFrame, authored ? authoredSprintTriggerGrip : sprintTriggerGrip, sprintGripWeight);
   var equippedPose = SupportPose(attachments, false);
   if (equippedPose != null)
    arm.Apply(SupportFrame(attachments, socket), equippedPose, sprintGripWeight);
   else
    arm.Apply(weaponFrame, authored ? authoredSprintSupportGrip : sprintSupportGrip, sprintGripWeight);
   return true;
  }

  int fire = animator.GetLayerIndex("Fire");
  if (HasAuthoredSprintGrip || sprintGripsReady || transition || weapon.IsAiming || movement && movement.IsSprinting ||
      fire >= 0 && (animator.IsInTransition(fire) || !animator.GetCurrentAnimatorStateInfo(fire).IsName("Default")))
   return false;
  int holster = animator.GetLayerIndex("Holster");
  if (holster >= 0 && (animator.IsInTransition(holster) || animator.GetLayerWeight(holster) > .01f))
   return false;

  // The full Run clip drives ik_hand_gun. Capture the weapon-specific
  // contacts from its neutral pose and solve both arms against that live frame.
  triggerArm ??= new WeaponSupportPose.Binding(animator, right:true);
  sprintGripsReady = triggerArm.Capture(weaponFrame, sprintTriggerGrip) &&
                     arm.Capture(weaponFrame, sprintSupportGrip);
  return false;
 }
}
