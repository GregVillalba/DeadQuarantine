#if UNITY_EDITOR
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using UnityEditor;using UnityEditor.Animations;using UnityEngine;
public static partial class CompleteWeaponsIntegrationBuilder {
 public static void InstallAndBuildWeaponPoses(){InstallWeaponPoseCorrections();Phase25WeaponsValidation.Build();WeaponPoseModeBuild.Build();}
 public static void BuildWeaponPoseValidation(){Phase25WeaponsValidation.Build();}
 struct SupportReach {public Vector3 shoulder;public float radius;}
 static readonly Dictionary<SupportHandPoseData,SupportReach> supportReach=new();
 static string PoseOutput=>Environment.GetEnvironmentVariable("DQ_POSE_OUTPUT");
 public static void InstallWeaponPoseCorrections(){
  supportReach.Clear();const string path="Assets/Prefabs/Player.prefab";string before=File.ReadAllText(path);var root=PrefabUtility.LoadPrefabContents(path);var log=new StringBuilder();
  try{var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var fp=sw.GetComponent<Animator>();var presentation=root.GetComponent<NetworkWeaponPresentation>();
   var bareFP=CaptureSupport(root,so,presentation,"Rifle",false);var bareTP=CaptureSupport(root,so,presentation,"Rifle",true);
   var gripFP=CaptureSupport(root,so,presentation,"BurstRifle",false);var gripTP=CaptureSupport(root,so,presentation,"BurstRifle",true);
   var catalog=root.GetComponent<NetworkWeaponAttachments>().catalog;var grip=catalog.Find("Grip_01");grip.supportHandPose=true;grip.firstPersonHand=gripFP;grip.thirdPersonHand=gripTP;EditorUtility.SetDirty(catalog);
   var mountOffsets=new Dictionary<string,float>();
   foreach(var id in new[]{"Sniper01","Sniper02","Sniper03"}){var f=CaptureSupport(root,so,presentation,id,false);var t=CaptureSupport(root,so,presentation,id,true);var idle=CaptureSupport(root,so,presentation,id,false,false);float offset=Mathf.Max(0,MinimumMount(supportReach[f],gripFP.position),MinimumMount(supportReach[idle],gripFP.position),MinimumMount(supportReach[t],gripTP.position));mountOffsets[id]=offset;}
   grip.mounts=mountOffsets.Select(pair=>new WeaponAttachmentCatalog.WeaponMount{weapon=pair.Key,position=Vector3.up*pair.Value}).ToArray();
   foreach(var d in presentation.Weapons){var slot=ReadSlotTemplate(so,d.Id);var profile=slot.Root.GetComponent<PackWeaponPresentation>();var effective=slot.FPOverride?slot.FPOverride:(RuntimeAnimatorController)so.FindProperty("baseController").objectReferenceValue;var ctl=PoseController(effective);
    profile.firstPersonRestStates=ctl.layers.Select(l=>Animator.StringToHash(l.name+"."+l.stateMachine.defaultState.name)).ToArray();
    profile.supportPoseEnabled=d.Id!="Escopeta"&&grip.weapons.Contains(d.Id);profile.bareSupportPoseEnabled=d.Id is "SemiRifle" or "BurstRifle" or "Sniper01" or "Sniper02" or "Sniper03";
    profile.bareFirstPerson=profile.bareSupportPoseEnabled?bareFP:null;profile.bareThirdPerson=profile.bareSupportPoseEnabled?bareTP:null;
    if(d.Id.StartsWith("Sniper",StringComparison.Ordinal)){
     profile.bareFirstPerson=CaptureSupport(root,so,presentation,d.Id,false);profile.bareThirdPerson=CaptureSupport(root,so,presentation,d.Id,true);
     profile.bareFirstPerson.rotation=bareFP.rotation;profile.bareFirstPerson.fingers=bareFP.fingers;
     profile.bareThirdPerson.rotation=bareTP.rotation;profile.bareThirdPerson.fingers=bareTP.fingers;
     log.AppendLine(d.Id+" own guard hold; foregrip rail shift="+mountOffsets[d.Id]);
    }
    if(!profile.preserveLegacyAnimation){ReleaseEmptyPoseLayers(ctl,log);ReleaseEmptyPoseLayers(PoseController(d.Controller),log);}
    if(d.Id=="BurstRifle"){
     // Measured from the mesh: centre of rear aperture and tip of front post.
     // Bounding-box maxima select the protective wings instead of the line of sight.
     profile.alignIronSightLine=true;profile.ironRear=new Vector3(.000010f,.059678f,.1292325f);profile.ironFront=new Vector3(.000010f,-.420930f,.128990f);
     var marker=FindDeep(slot.Root.transform,"DQ_OpticalSight");marker.localPosition=profile.ironRear;
     var aim=effective.animationClips.First(c=>AssetDatabase.GetAssetPath(c).IndexOf("Aim_Pose",StringComparison.OrdinalIgnoreCase)>=0);var camera=(Camera)new SerializedObject(slot.Weapon).FindProperty("weaponCamera").objectReferenceValue;
     AnimationMode.StartAnimationMode();try{Sample6(fp.gameObject,aim);var physical=FindDeep(slot.Root.transform,"root");profile.aimPhysicalToCamera=camera.transform.worldToLocalMatrix*physical.localToWorldMatrix;profile.cameraToRigParent=fp.transform.parent.worldToLocalMatrix*camera.transform.localToWorldMatrix;
      var rear=profile.aimPhysicalToCamera.MultiplyPoint3x4(profile.ironRear);var front=profile.aimPhysicalToCamera.MultiplyPoint3x4(profile.ironFront);var turn=Quaternion.FromToRotation(front-rear,Vector3.forward);var origin=camera.transform.InverseTransformPoint(fp.transform.position);var rotated=origin+turn*(rear-origin);
      profile.ironAimRotation=profile.cameraToRigParent.rotation*turn*Quaternion.Inverse(profile.cameraToRigParent.rotation);profile.ironAimOffset=profile.cameraToRigParent.MultiplyVector(new Vector3(-rotated.x,-rotated.y,0));
      log.AppendLine("Burst physical rear="+profile.ironRear.ToString("F6")+" front="+profile.ironFront.ToString("F6")+" rotation="+profile.ironAimRotation.eulerAngles.ToString("F6")+" shift="+profile.ironAimOffset.ToString("F6"));
     }finally{AnimationMode.StopAnimationMode();}
    }
    EditorUtility.SetDirty(profile);log.AppendLine(d.Id+" rest states="+profile.firstPersonRestStates.Length+" bare support="+profile.bareSupportPoseEnabled+" grip="+profile.supportPoseEnabled);
   }
   PrefabUtility.SaveAsPrefabAsset(root,path);var old=System.Text.RegularExpressions.Regex.Matches(before,@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline).Cast<System.Text.RegularExpressions.Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
   string profileGuid=AssetDatabase.AssetPathToGUID("Assets/Scripts/Player/PackWeaponPresentation.cs");var burst=ReadSlotTemplate(so,"BurstRifle");AssetDatabase.TryGetGUIDAndLocalFileIdentifier(FindDeep(burst.Root.transform,"DQ_OpticalSight"),out string _,out long markerId);
   string saved=System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path),@"^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)",m=>{
    if(!old.TryGetValue(m.Groups[1].Value,out var prior))return m.Value;
    if(prior.StartsWith("--- !u!114 ")&&!prior.Contains("guid: "+profileGuid))return prior;
    if(prior.StartsWith("--- !u!4 ")&&m.Groups[1].Value!=markerId.ToString())return prior;
    if(prior.StartsWith("--- !u!1001 ")){var mods=System.Text.RegularExpressions.Regex.Match(prior,@"^    m_Modifications:.*?(?=^    m_RemovedComponents:)",System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline);if(mods.Success)return System.Text.RegularExpressions.Regex.Replace(m.Value,@"^    m_Modifications:.*?(?=^    m_RemovedComponents:)",_=>mods.Value,System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline);}
    return m.Value;
   },System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline);File.WriteAllText(path,saved);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);AssetDatabase.SaveAssets();
  }finally{PrefabUtility.UnloadPrefabContents(root);}File.WriteAllText(Path.Combine(PoseOutput,"Install.txt"),log.ToString());
 }
 static AnimatorController PoseController(RuntimeAnimatorController c)=>(AnimatorController)(c is AnimatorOverrideController a?a.runtimeAnimatorController:c);
 static void ReleaseEmptyPoseLayers(AnimatorController c,StringBuilder log){var layers=c.layers;for(int i=1;i<layers.Length;i++){var state=layers[i].stateMachine.defaultState;if(state.motion)continue;layers[i].defaultWeight=0;state.writeDefaultValues=false;if(!state.behaviours.OfType<WeaponActionLayerWeight>().Any())state.AddStateMachineBehaviour<WeaponActionLayerWeight>();EditorUtility.SetDirty(state);log.AppendLine(c.name+" releases "+layers[i].name+"."+state.name);}c.layers=layers;EditorUtility.SetDirty(c);}
 static float MinimumMount(SupportReach reach,Vector3 wrist){float radial=(wrist.x-reach.shoulder.x)*(wrist.x-reach.shoulder.x)+(wrist.z-reach.shoulder.z)*(wrist.z-reach.shoulder.z);float along=Mathf.Sqrt(Mathf.Max(.0001f,reach.radius*reach.radius-radial));return Mathf.Max(0,reach.shoulder.y-wrist.y-along)+.025f;}
 static SupportHandPoseData CaptureSupport(GameObject root,SerializedObject slots,NetworkWeaponPresentation presentation,string id,bool third,bool aiming=true){
  var slot=ReadSlotTemplate(slots,id);var definition=presentation.Weapons.Single(d=>d.Id==id);var animator=third?presentation.BodyAnimator:root.GetComponentInChildren<WeaponSwitcher>(true).GetComponent<Animator>();var controller=third?definition.Controller:slot.FPOverride;
  if(!controller)controller=(RuntimeAnimatorController)slots.FindProperty("baseController").objectReferenceValue;
  var clipName=(third?"A_TP_CH_":"A_FP_PCH_")+(id=="Rifle"?"AR_01":Specs7().Single(s=>s.Id==id).Model.Substring(3))+(aiming?"_Aim_Pose":"_Idle_Pose");
  // Raw pack TP files are Generic; use the already imported Humanoid variant
  // actually assigned to this weapon. A Generic sample on the body leaves bind bones.
  string imported="Assets/Generated/NewWeapons/Phase9/Source431/"+clipName+".fbx";
  var clip=third&&File.Exists(imported)?AssetDatabase.LoadAllAssetsAtPath(imported).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")):RequireClip(clipName);
  if(third&&!clip.isHumanMotion)throw new InvalidOperationException("Support reference must be Humanoid: "+AssetDatabase.GetAssetPath(clip));var hand=third?animator.GetBoneTransform(HumanBodyBones.LeftHand):FindDeep(animator.transform,"hand_l");var lower=third?animator.GetBoneTransform(HumanBodyBones.LeftLowerArm):FindDeep(animator.transform,"lowerarm_l");GameObject model=null;
  AnimationMode.StartAnimationMode();try{Sample6(animator.gameObject,clip);Transform socket;
   if(third){var right=animator.GetBoneTransform(HumanBodyBones.RightHand);model=UnityEngine.Object.Instantiate(definition.Model,right);model.transform.localPosition=definition.GripPosition;model.transform.localRotation=Quaternion.Euler(definition.GripRotation);socket=FindDeep(model.transform,"SOCKET_Grip");}else socket=FindDeep(slot.Root.transform,"SOCKET_Grip");
   File.AppendAllText(Path.Combine(PoseOutput,"References.txt"),id+" "+(third?"TP":"FP")+" "+AssetDatabase.GetAssetPath(clip)+" socket="+socket.localPosition+" hand="+hand.name+"\n");
   var pose=new SupportHandPoseData{position=socket.InverseTransformPoint(hand.position),rotation=Quaternion.Inverse(socket.rotation)*hand.rotation,elbow=socket.InverseTransformPoint(lower.position),fingers=hand.GetComponentsInChildren<Transform>(true).Where(t=>t!=hand).Select(t=>new SupportFingerPose{bone=t.name,rotation=t.localRotation}).ToArray()};
   var upper=third?animator.GetBoneTransform(HumanBodyBones.LeftUpperArm):FindDeep(animator.transform,"upperarm_l");supportReach[pose]=new SupportReach{shoulder=socket.InverseTransformPoint(upper.position),radius=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))/Mathf.Abs(socket.lossyScale.x)-.02f};
   if(id=="BurstRifle"){
    var catalog=root.GetComponent<NetworkWeaponAttachments>().catalog;var hold=UnityEngine.Object.Instantiate(catalog.Find("Grip_01").model,socket);hold.transform.localPosition=Vector3.zero;hold.transform.localRotation=Quaternion.identity;hold.transform.localScale=Vector3.one;
    var bounds=WeaponAttachments.BoundsIn(hold,socket);var fingers=hand.Cast<Transform>().ToArray();var palmLocal=fingers.Length>0?fingers.Aggregate(Vector3.zero,(sum,t)=>sum+t.localPosition)/fingers.Length*.6f:Vector3.zero;
    var palmOffset=socket.InverseTransformVector(hand.TransformVector(palmLocal));var approach=pose.position-bounds.center;approach.z=0;if(approach.sqrMagnitude<.000001f)approach=Vector3.up;
    pose.position=bounds.center+approach.normalized*(Mathf.Max(bounds.extents.x,bounds.extents.y)+.008f)-palmOffset;
    File.AppendAllText(Path.Combine(PoseOutput,"References.txt"),"Grip geometry="+bounds+" palm="+palmOffset.ToString("F4")+" corrected wrist="+pose.position.ToString("F4")+"\n");UnityEngine.Object.DestroyImmediate(hold);
   }
   return pose;
  }finally{if(model)UnityEngine.Object.DestroyImmediate(model);AnimationMode.StopAnimationMode();}
 }
 public static void DiagnoseWeaponPoses(){var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");var text=new StringBuilder();try{var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var rig=sw.GetComponent<Animator>();var defs=root.GetComponent<NetworkWeaponPresentation>();
 foreach(var d in defs.Weapons){var slot=ReadSlotTemplate(so,d.Id);var ctl=slot.FPOverride?slot.FPOverride:(RuntimeAnimatorController)so.FindProperty("baseController").objectReferenceValue;var controller=ctl is AnimatorOverrideController a?a.runtimeAnimatorController:ctl;var c=(AnimatorController)controller;text.AppendLine(d.Id+" FP controller="+AssetDatabase.GetAssetPath(ctl));foreach(var l in c.layers)text.AppendLine(l.name+" default="+l.stateMachine.defaultState.name+" motion="+(l.stateMachine.defaultState.motion?l.stateMachine.defaultState.motion.name:"EMPTY")+" weight="+l.defaultWeight+" blend="+l.blendingMode+" WD="+l.stateMachine.defaultState.writeDefaultValues);
 if(d.Id=="BurstRifle"){var phy=FindDeep(slot.Root.transform,"root");var scope=FindDeep(slot.Root.transform,"DQ_DefaultScope");var vertices=scope.GetComponentsInChildren<Renderer>(true).SelectMany(r=>{var mf=r.GetComponent<MeshFilter>();var sk=r as SkinnedMeshRenderer;var mesh=mf?mf.sharedMesh:sk?sk.sharedMesh:null;return mesh?mesh.vertices.Select(v=>phy.InverseTransformPoint(r.transform.TransformPoint(v))):Enumerable.Empty<Vector3>();}).ToArray();File.WriteAllText(Path.Combine(PoseOutput,"BurstVertices.json"),"["+string.Join(",",vertices.Select(v=>"["+v.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+v.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+v.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"]"))+"]");text.AppendLine("default scope="+scope.name+" / "+scope.localPosition+" / "+scope.localEulerAngles);}
 }
 var hand=ReadSlotTemplate(so,"Handgun");var old=hand.FPOverride?hand.FPOverride:(RuntimeAnimatorController)so.FindProperty("baseController").objectReferenceValue;rig.runtimeAnimatorController=old;rig.Rebind();rig.Update(0);var hashes=Enumerable.Range(0,rig.layerCount).Select(l=>rig.GetCurrentAnimatorStateInfo(l).fullPathHash).ToArray();foreach(var id in new[]{"SemiRifle","BurstRifle","P90"}){var slot=ReadSlotTemplate(so,id);rig.runtimeAnimatorController=slot.FPOverride;rig.Rebind();rig.Update(0);text.AppendLine(id+" initial cached invalid states: "+string.Join(",",Enumerable.Range(0,Mathf.Min(hashes.Length,rig.layerCount)).Where(l=>!rig.HasState(l,hashes[l])).Select(l=>rig.GetLayerName(l))));}
 }finally{PrefabUtility.UnloadPrefabContents(root);}File.WriteAllText(Path.Combine(PoseOutput,"Diagnostics.txt"),text.ToString());}
}
#endif
