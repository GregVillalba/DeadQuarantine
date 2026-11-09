#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static partial class CompleteWeaponsIntegrationBuilder
{
 const string Phase6Dir="Assets/Generated/NewWeapons/Phase6";
 static Spec[] Specs6()=>new[]{
  Spec6("Handgun01","Pistola 01","Handgun_01",Family.Handgun,32,.27f,2.75f,70,12,72,false,"S_WEP_Fire_001"),
  Spec6("Handgun02","Pistola 02","Handgun_02",Family.Handgun,40,.35f,2.75f,80,8,48,false,"S_WEP_Fire_003"),
  Spec6("SMG02","Subfusil 02","SMG_02",Family.SMG,21,.085f,4.15f,80,30,180,true,"S_WEP_Fire_002"),
  Spec6("SMG05","Subfusil 05","SMG_05",Family.SMG,24,.10f,4.6f,95,30,180,true,"S_WEP_Fire_005"),
  Spec6("Sniper01","Rifle de cerrojo 01","Sniper_01",Family.AR,95,1.9f,5f,250,5,30,false,"S_WEP_Fire_003"),
  Spec6("Sniper02","Rifle de precisiÃ³n 02","Sniper_02",Family.AR,65,.48f,5f,220,10,50,false,"S_WEP_Fire_004"),
  Spec6("Sniper03","Rifle de precisiÃ³n 03","Sniper_03",Family.AR,60,.42f,4.4f,200,10,50,false,"S_WEP_Fire_006")};
 static Spec Spec6(string id,string label,string code,Family family,int damage,float interval,float reload,float range,int magazine,int reserve,bool automatic,string sound)
 =>new Spec{Id=id,DisplayName=label,Model="SK_"+code,Family=family,Damage=damage,FireInterval=interval,ReloadTime=reload,Range=range,Magazine=magazine,Reserve=reserve,Automatic=automatic,
 HudSprite="T_HUD_"+code+"_D",ScopeSprite="T_HUD_"+code+"_Scope_Default_D",MagazineSprite="T_HUD_"+code+"_Magazine_Default_D",ScopeModel="SM_"+code+"_Scope_Default",MagazineModel="SM_"+code+"_Magazine_Default",FireSound=sound};

 [MenuItem("Game/Dead Quarantine/PvPvE/Fase 6 - Integrar armas faltantes")]
 public static void IntegrateMissingPhase6()
 {
  var existing=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab").GetComponent<NetworkWeaponPresentation>();
  if(existing.Weapons.Any(w=>Specs6().Any(s=>s.Id==w.Id)))throw new InvalidOperationException("Fase 6 ya integrada; no se regeneran armas existentes.");
  EnsureFolders();Directory.CreateDirectory(Phase6Dir);AssetDatabase.Refresh();
  var specs=Specs6();foreach(var s in specs)Resolve6(s);
  const string path="Assets/Prefabs/Player.prefab";
  var root=PrefabUtility.LoadPrefabContents(path);
  try {
   var sw=root.GetComponentInChildren<WeaponSwitcher>(true);var so=new SerializedObject(sw);var presentation=root.GetComponent<NetworkWeaponPresentation>();
   // Append only. Existing slots and their models/controllers are never rebuilt.
   if(presentation.Weapons.Any(w=>specs.Any(s=>s.Id==w.Id)))throw new InvalidOperationException("Fase 6 ya integrada; no se regeneran armas existentes.");
   var generated=new List<GeneratedWeapon>();
   foreach(var s in specs){
    var template=ReadSlotTemplate(so,s.Family==Family.Handgun?"Handgun":"Rifle");var tp=FindDefinition(presentation.Weapons,template.Id);
    var fpController=Controller6(s,(AnimatorController)so.FindProperty("baseController").objectReferenceValue,false);
    var tpController=Controller6(s,(AnimatorController)so.FindProperty("tpBaseController").objectReferenceValue,true);
    var fpParts=s.Id=="Sniper01"?BoltParts6(s,false):BuildPartsController(s,false);
    var tpParts=s.Id=="Sniper01"?BoltParts6(s,true):BuildPartsController(s,true);
    var icon=BuildHudIconPrefab(s,template.Icon);
    var weapon=BuildFirstPersonWeapon(root,s,template,tp,fpController,tpController,fpParts,tpParts,icon);
    var settings=new SerializedObject(weapon.Weapon);
    SetBool(settings,"adsFireAlongVisualSight",true);SetBool(settings,"adsCenterVisualSight",true);
    // Default iron sights are authored in model coordinates; their mount is not the optical centre.
    SetObject(settings,"adsSightReference",FindDeep(weapon.Root.transform,"SOCKET_Scope"));
    if(s.Id=="Sniper01"){
     SetBool(settings,"requiresBoltActionAfterFire",true);SetBool(settings,"usesMultiPartReload",true);SetBool(settings,"enforceReloadDuration",false);SetFloat(settings,"boltActionDuration",1.85f);
     foreach(string key in new[]{"reloadClipSpeedMultiplier","reloadEmptyClipSpeedMultiplier","partsReloadClipSpeedMultiplier","partsReloadEmptyClipSpeedMultiplier","tpReloadClipSpeedMultiplier","tpReloadEmptyClipSpeedMultiplier","tpPartsReloadClipSpeedMultiplier","tpPartsReloadEmptyClipSpeedMultiplier"})SetFloat(settings,key,1);
     foreach(var pair in new[]{("reloadOpenSound","Open"),("reloadInsertSound","Insert"),("reloadCloseSound","Close"),("reloadBoltOpenSound","Bolt_Open"),("reloadBoltCloseSound","Bolt_Close")})
      SetObject(settings,pair.Item1,OptionalAudio6("S_WEP_Sniper_01_Reload_"+pair.Item2));
     weapon.Root.AddComponent<MultipartWeaponPartsSync>();
     var model=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(weapon.TPPrefab));
     try{model.AddComponent<MultipartWeaponPartsSync>();PrefabUtility.SaveAsPrefabAsset(model,AssetDatabase.GetAssetPath(weapon.TPPrefab));}finally{PrefabUtility.UnloadPrefabContents(model);}
    }
    settings.ApplyModifiedPropertiesWithoutUndo();generated.Add(weapon);
   }
   AppendSlots(so,generated);so.ApplyModifiedPropertiesWithoutUndo();RebuildPresentationDefinitions(presentation,generated);
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  RefineMissingPhase6();AssetDatabase.SaveAssets();Directory.CreateDirectory(Phase6Output);File.WriteAllLines(Path.Combine(Phase6Output,"integrated.txt"),specs.Select(s=>s.Id+"="+s.Model));
 }
 static AudioClip OptionalAudio6(string name)=>OptionalClipAsset6<AudioClip>(name);
 static T OptionalClipAsset6<T>(string name) where T:UnityEngine.Object{
  foreach(var guid in AssetDatabase.FindAssets(name+" t:"+typeof(T).Name)){
   string path=AssetDatabase.GUIDToAssetPath(guid);if(Path.GetFileNameWithoutExtension(path)==name)return AssetDatabase.LoadAssetAtPath<T>(path);
  }return null;
 }
 static void Resolve6(Spec s){
  string code=s.Model.Substring(3);bool bolt=s.Id=="Sniper01",sniper=s.Id.StartsWith("Sniper");
  s.ModelAsset=RequireAsset<GameObject>(s.Model);s.ScopeModelAsset=RequireAsset<GameObject>(s.ScopeModel);s.MagazineModelAsset=RequireAsset<GameObject>(s.MagazineModel);
  s.HudSpriteAsset=RequireAsset<Sprite>(s.HudSprite);s.ScopeSpriteAsset=RequireAsset<Sprite>(s.ScopeSprite);s.MagazineSpriteAsset=RequireAsset<Sprite>(s.MagazineSprite);
  s.FireSoundAsset=RequireAsset<AudioClip>(s.FireSound);
  s.ReloadSoundAsset=RequireAsset<AudioClip>("S_WEP_"+(s.Family==Family.Handgun?"Handgun":code)+"_Reload"+(bolt?"_Open":""));
  s.ReloadEmptySoundAsset=bolt?s.ReloadSoundAsset:RequireAsset<AudioClip>("S_WEP_"+(s.Family==Family.Handgun?"Handgun":code)+"_Reload_Empty");
  if(!sniper){s.FP=ResolveFPClips(s);s.TP=ResolveTPClips(s);ResolvePartsClips(s,AssetDatabase.GetAssetPath(s.ModelAsset));ValidateRequiredClips(s);}
  else{
   string fp="A_FP_PCH_"+code+"_",tp="A_TP_CH_"+code+"_";
   s.FP=new ClipSet{AimPose=RequireClip(fp+"Aim_Pose"),Idle=RequireClip(fp+"Idle_Pose"),Fire=RequireClip(bolt?fp+"Fire":"A_FP_PCH_Sniper_Fire"),AimFire=RequireClip(bolt?fp+"Aim_Fire":"A_FP_PCH_Sniper_Aim_Fire"),
    Holster=RequireClip(fp+"Holster"),Unholster=RequireClip(fp+"Unholster"),Inspect=RequireClip(fp+"Inspect"),Run=RequireClip("A_FP_PCH_Run_02_Additive"),Reload=RequireClip(fp+(bolt?"Reload_Open":"Reload")),ReloadEmpty=RequireClip(fp+(bolt?"Reload_Open":"Reload_Empty"))};
   s.TP=new ClipSet{AimPose=RequireClip(tp+"Aim_Pose"),Idle=RequireClip(tp+"Idle_Pose"),Fire=OptionalClip(tp+"Fire")??RequireClip("A_TP_CH_Sniper_03_Fire"),CrouchPose=RequireClip(tp+"Crouch_Pose"),CrouchAimPose=RequireClip(tp+"Crouch_Aim_Pose"),Jog=RequireClip(tp+"Jog_F"),Sprint=RequireClip(tp+"Sprint_F"),SprintImpulse=RequireClip(tp+"Sprint_Impulse_F"),Inspect=RequireClip(tp+"Inspect"),Reload=RequireClip(tp+(bolt?"Reload_Open":"Reload")),ReloadEmpty=RequireClip(tp+(bolt?"Reload_Open":"Reload_Empty"))};
   if(!bolt){ResolvePartsClips(s,AssetDatabase.GetAssetPath(s.ModelAsset));ValidateRequiredClips(s);}
  }
  // Local event-bearing copies: never edit imported clips or the protected controllers.
  s.FP.Fire=Events6(s,s.FP.Fire,"Fire",bolt?Array.Empty<(string,float)>():new[]{("OnEjectCasing",.18f)});
  s.FP.AimFire=Events6(s,s.FP.AimFire,"AimFire",bolt?Array.Empty<(string,float)>():new[]{("OnEjectCasing",.18f)});
  if(!bolt){s.FP.Reload=Events6(s,s.FP.Reload,"Reload",new[]{("OnAmmunitionFill",.70f),("OnAnimationEndedReload",.98f)});s.FP.ReloadEmpty=Events6(s,s.FP.ReloadEmpty,"ReloadEmpty",new[]{("OnAmmunitionFill",.70f),("OnAnimationEndedReload",.98f)});}
  s.FP.Holster=Events6(s,s.FP.Holster,"Holster",Array.Empty<(string,float)>());s.FP.Unholster=Events6(s,s.FP.Unholster,"Unholster",new[]{("OnAnimationEndedHolster",.98f)});
 }
 static AnimationClip Events6(Spec s,AnimationClip source,string token,(string,float)[] events){
  var clip=UnityEngine.Object.Instantiate(source);clip.name=s.Id+"_"+token;AnimationUtility.SetAnimationEvents(clip,events.Select(e=>new AnimationEvent{functionName=e.Item1,time=clip.length*e.Item2,messageOptions=SendMessageOptions.DontRequireReceiver}).ToArray());
  AssetDatabase.CreateAsset(clip,Phase6Dir+"/"+clip.name+".anim");return clip;
 }
 static AnimatorOverrideController Controller6(Spec s,AnimatorController source,bool tp){
  string path=Phase6Dir+"/"+s.Id+(tp?"_TP":"_FP")+".controller";if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path))throw new Exception(path);
  var ctl=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
  if(!tp){for(int i=ctl.layers.Length-1;i>=0;i--)if(new[]{"HandGunADD","NoAmmoADD"}.Contains(ctl.layers[i].name))ctl.RemoveLayer(i);}
  var c=tp?s.TP:s.FP;
  foreach(var layer in ctl.layers){foreach(var state in States6(layer.stateMachine)){
   if(tp && layer.name=="WeaponSwitch")continue;
   ReplaceMotion6(state.motion,clip=>Map6(clip,c,tp),motion=>state.motion=motion);
   if(s.Id=="Sniper01" && ((tp&&layer.name=="Actions")||(!tp&&(layer.name=="Reload"||layer.name=="Fire")))){
    string role=state.name;string suffix=role.Contains("Insert")?"Insert":role is "Bolt" or "Reload_Bolt"?"Bolt":role=="Reload_Open"?"Open":role=="Reload_Close"?"Close":null;
    if(suffix!=null){var clip=RequireClip((tp?"A_TP_CH_":"A_FP_PCH_")+"Sniper_01_Reload_"+suffix);
     if(!tp)clip=Events6(s,clip,layer.name+"_"+role.Replace(" ",""),suffix=="Insert"?new[]{("OnInsertShell",.55f)}:suffix=="Close"?new[]{("AnimationReloadClose",.1f),("OnAnimationEndedReload",.99f)}:suffix=="Bolt"?new[]{("AnimationBoltOpen",.1f),("OnEjectCasing",.30f),("AnimationBoltClose",.85f)}:new[]{("AnimationReloadOpen",.05f)});
     state.motion=clip;state.speed=1;state.speedParameterActive=false;
     if(role=="Reload_Close"){foreach(var tr in state.transitions)state.RemoveTransition(tr);AddReturnToIdle(state,States6(layer.stateMachine).First(st=>st.name=="Default"),.02f);}
    }
   }
  }}
  EditorUtility.SetDirty(ctl);
  var aoc=new AnimatorOverrideController(ctl){name=s.Id+(tp?"_TP":"_FP")};AssetDatabase.CreateAsset(aoc,Phase6Dir+"/"+aoc.name+".overrideController");return aoc;
 }
 static void ReplaceMotion6(Motion source,Func<AnimationClip,AnimationClip> map,Action<Motion> set){
  if(source is AnimationClip clip){var replacement=map(clip);if(replacement)set(replacement);}
  else if(source is BlendTree tree){var children=tree.children;for(int i=0;i<children.Length;i++){int index=i;ReplaceMotion6(children[i].motion,map,m=>children[index].motion=m);}tree.children=children;EditorUtility.SetDirty(tree);}
 }
 static AnimationClip Map6(AnimationClip clip,ClipSet c,bool tp){
  string n=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(clip)).ToLowerInvariant();if(string.IsNullOrEmpty(n))n=clip.name.ToLowerInvariant();if(!(n.Contains("handgun")||n.Contains("ar_")||n.Contains("smg_")||n.Contains("shotgun")||n.Contains("sniper")||n.Contains("run_02")))return null;
  if(n.Contains("reload_bolt")||n.Contains("reload_open")||n.Contains("reload_insert")||n.Contains("reload_close"))return null;
  if(n.Contains("aim_fire"))return c.AimFire??c.Fire;if(n.Contains("fire")&&!n.Contains("empty"))return c.Fire;
  if(n.Contains("reload_empty"))return c.ReloadEmpty;if(n.Contains("reload"))return c.Reload;
  if(n.Contains("crouch_aim"))return c.CrouchAimPose;if(n.Contains("crouch_pose"))return c.CrouchPose;
  if(n.Contains("aim_pose"))return c.AimPose;if(n.Contains("idle_pose"))return c.Idle;
  if(n.Contains("unholster"))return c.Unholster;if(n.Contains("holster"))return c.Holster;
  if(n.Contains("inspect"))return c.Inspect;if(n.Contains("sprint_impulse"))return c.SprintImpulse;if(n.Contains("sprint"))return c.Sprint;if(n.Contains("jog"))return c.Jog;if(n.Contains("run"))return c.Run;return null;
 }
 static AnimatorController BoltParts6(Spec s,bool tp){
  var ctl=AnimatorController.CreateAnimatorControllerAtPath(Phase6Dir+"/"+s.Id+(tp?"_TPParts":"_FPParts")+".controller");var sm=ctl.layers[0].stateMachine;
  var idle=sm.AddState("Idle");idle.motion=FindEmbeddedClip(AssetDatabase.GetAssetPath(s.ModelAsset),"_Ref_Pose");sm.defaultState=idle;
  foreach(string token in new[]{"Open","Insert","Close","Bolt"}){var state=sm.AddState("Reload_"+token);state.motion=FindEmbeddedClip(AssetDatabase.GetAssetPath(s.ModelAsset),(tp?"A_TP_WEP_":"A_FP_WEP_")+"Sniper_01_Reload_"+token);if(!state.motion)throw new Exception("Missing bolt parts "+token);}
  return ctl;
 }
}
#endif
