using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Presentation only. Weapon.cs retains the project's existing firing and damage path.
public sealed class NetworkWeaponPresentation : NetworkBehaviour
{
    [Serializable] public sealed class Definition
    {
        public string Id;
        public Weapon Source;
        public GameObject Model;
        public RuntimeAnimatorController Controller;
        [Tooltip("Familia visual TP: 1=Pistola, 2=Rifle/SMG, 3=Escopeta. 0=usar índice histórico.")]
        public int AnimatorWeaponId;
        public GameObject Casing;
        public Vector3 GripPosition, GripRotation;
    }
    public enum WeaponEvent : byte { Eject, ReloadOpen, Insert, ReloadClose, BoltOpen, BoltClose }
    public struct State : INetworkSerializable, IEquatable<State>
    {
        public int Weapon;
        public byte Flags;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref Weapon); serializer.SerializeValue(ref Flags); }
        public bool Equals(State other) => Weapon==other.Weapon && Flags==other.Flags;
    }
    [Header("Pruebas locales")]
    [Tooltip("Muestra el cuerpo y el arma propios, incluso en singleplayer, para revisar animaciones desde la vista de escena.")]
    public bool ShowLocalBodyForTesting;
    public Transform Body;
    public WeaponSwitcher Switcher;
    public Animator BodyAnimator;
    public Definition[] Weapons = Array.Empty<Definition>();
    readonly NetworkVariable<State> state = new(new State { Weapon=-1 });
    Transform hand, muzzle, eject;
    GameObject held;
    Muzzle flash;
    Animator parts;
    AudioSource audioOutput;
    int shown=-2;
    State sent;
    byte appliedFlags;
    uint shotSequence, acceptedSequence;
    double nextAcceptedShot;
    WeaponSupportPose.Binding supportArm;
    bool boltRifleInSupportHand;
    int lastSkin=-1;
    Renderer[] bodyRenderers=Array.Empty<Renderer>();
    StoryIntroController storyIntro;
    public int EquippedIndex => state.Value.Weapon;
    public int RemoteShotCount {get; private set;}
    public int EjectCount {get; private set;}
    public int RemoteSoundEvents {get;private set;}
    public int RemoteFlashEvents {get;private set;}
    public int RemoteTrailEvents {get;private set;}
    public Transform VisibleMuzzle => muzzle;
    public GameObject VisibleWeapon => held;
    public override void OnNetworkSpawn()
    {
        storyIntro=GetComponent<StoryIntroController>();
        hand=Array.Find(Body.GetComponentsInChildren<Transform>(true),t=>t.name=="Hand_R");
        audioOutput=gameObject.AddComponent<AudioSource>();audioOutput.playOnAwake=false;audioOutput.spatialBlend=1;
        audioOutput.minDistance=3;audioOutput.maxDistance=90;audioOutput.rolloffMode=AudioRolloffMode.Logarithmic;
        state.OnValueChanged+=StateChanged;sent=new State{Weapon=-2};Apply(state.Value);
    }
    public override void OnNetworkDespawn()
    {
        state.OnValueChanged-=StateChanged;
        if(held)Destroy(held);if(audioOutput)Destroy(audioOutput);
        base.OnNetworkDespawn();
    }
    void Update()=>PublishStateIfChanged();
    void PublishStateIfChanged()
    {
        if(!IsSpawned || !IsOwner || !Switcher || !Switcher.CurrentWeapon)return;
        var weapon=Switcher.CurrentWeapon;
        bool empty=weapon.gameObject.name.StartsWith("DQ_Weapon_",StringComparison.Ordinal)?weapon.PresentationEmpty:weapon.CurrentAmmo==0;
        var value=new State{Weapon=Switcher.CurrentWeaponIndex,Flags=(byte)((weapon.IsReloading?1:0)|(weapon.ShellLoading?2:0)|(weapon.IsChambering?4:0)|(weapon.IsAiming?8:0)|(empty?16:0)|(Switcher.IsHolstering?32:0)|(Switcher.IsUnholstering?64:0))};
        if(value.Equals(sent))return;
        sent=value;Apply(value);StateServerRpc(value);
    }
    void LateUpdate()
    {
        if(!IsSpawned || !Body)return;
        if(parts && BodyAnimator && shown>=0 && shown<Weapons.Length && (appliedFlags&1)!=0 && !Weapons[shown].Source.UsesMultiPartReload && Weapons[shown].Source.gameObject.name.StartsWith("DQ_Weapon_",StringComparison.Ordinal)) {
            int actions=BodyAnimator.GetLayerIndex("Actions");
            if(actions>=0) {
                var action=BodyAnimator.GetCurrentAnimatorStateInfo(actions);
                string clip=(action.IsName("Reload Empty")||action.IsName("Reload_Empty"))?"Reload Empty":action.IsName("Reload")?"Reload":null;
                if(clip!=null && parts.HasState(0,Animator.StringToHash(clip))){parts.Play(clip,0,Mathf.Clamp01(action.normalizedTime));parts.Update(0);}
            }
        }
        ApplySupportHand();
        int skin=GetComponent<PlayerCharacter>().AppliedIndex;
        if(skin!=lastSkin) {lastSkin=skin;bodyRenderers=Body.GetComponentsInChildren<Renderer>(true);}
        bool ocultoPorIntroduccion=IntroMultiplayerSinConfirmar;
        foreach(var renderer in bodyRenderers)if(renderer)renderer.forceRenderingOff=DeadPvPvEBody || IsOwner && !ShowLocalBodyForTesting || ocultoPorIntroduccion;
    }
    bool IntroMultiplayerSinConfirmar =>
        storyIntro != null && SceneManager.GetActiveScene().name.StartsWith("MP_") &&
        !storyIntro.IntroduccionInicialConfirmada;
    // Humanoid TP clips move the loading hand independently of the gun.
    // The bolt rifle stays in its supporting left hand while the right hand loads/cycles it.
    void AttachBoltRifleForAction(State value,Definition definition){
        if(definition.Id!="Sniper01"||!held||!BodyAnimator)return;
        bool support=(value.Flags&5)!=0;
        if(support==boltRifleInSupportHand)return;
        if(support){var left=BodyAnimator.GetBoneTransform(HumanBodyBones.LeftHand);if(!left)return;held.transform.SetParent(left,true);}
        else{held.transform.SetParent(hand,false);held.transform.localPosition=definition.GripPosition;held.transform.localRotation=Quaternion.Euler(definition.GripRotation);}
        boltRifleInSupportHand=support;
    }
    bool DeadPvPvEBody=>DeadQuarantine.PvPvE.Networking.PvPvEPreparationFlow.OwnsStagingPlayer(NetworkObject)&&GetComponent<PlayerHealth>() is {IsAlive:false};
    void ApplySupportHand(){
        if(!BodyAnimator||!held||shown<0||shown>=Weapons.Length||(appliedFlags&101)!=0)return;
        var health=GetComponent<PlayerHealth>();if(!health.IsAlive||health.IsDowned)return;
        var profile=Weapons[shown].Source.GetComponent<PackWeaponPresentation>();var attachments=held.GetComponent<WeaponAttachments>();var pose=profile?profile.SupportPose(attachments,true):null;
        if(pose!=null){supportArm??=new WeaponSupportPose.Binding(BodyAnimator);supportArm.Apply(profile.SupportFrame(attachments,Find(held.transform,"SOCKET_Grip")),pose);}
    }
    [ServerRpc] void StateServerRpc(State value)
    {
        if(value.Weapon<0 || value.Weapon>=Weapons.Length)return;
        if(!GetComponent<PlayerHealth>().ArmaPermitidaPorClase(Weapons[value.Weapon].Id))return;
        value.Flags&=127;
        var combat=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        if(combat&&!combat.ObserveStateServer(GetComponent<PlayerHealth>(),value.Weapon,value.Flags))return;
        state.Value=value;
    }
    void StateChanged(State old,State value) {if(!IsOwner)Apply(value);}
    void Apply(State value)
    {
        if(value.Weapon<0 || value.Weapon>=Weapons.Length || !hand)return;
        var definition=Weapons[value.Weapon];
        bool equipmentChanged=shown!=value.Weapon;bool finishedDraw=(appliedFlags&96)!=0&&(value.Flags&96)==0;
        if(equipmentChanged) {
            shown=value.Weapon;appliedFlags=0;boltRifleInSupportHand=false;
            if(audioOutput)audioOutput.outputAudioMixerGroup=definition.Source.PresentationMixer;
            if(BodyAnimator && definition.Controller && BodyAnimator.runtimeAnimatorController!=definition.Controller)BodyAnimator.runtimeAnimatorController=definition.Controller;
            if(BodyAnimator)BodyAnimator.SetFloat("TP_WeaponID", definition.AnimatorWeaponId>0?definition.AnimatorWeaponId:value.Weapon+1);
            if(held)Destroy(held);
            if(!definition.Model)return;
            held = Instantiate(definition.Model, hand);
            held.name = "SM_Wep_FP_" + definition.Id;

            held.transform.localPosition =
                definition.GripPosition;

            held.transform.localRotation =
                Quaternion.Euler(
                    definition.GripRotation
                );

            SetLayer(
                held.transform,
                Body.gameObject.layer
            );
            muzzle=Find(held.transform,"SOCKET_Muzzle");eject=Find(held.transform,"SOCKET_Eject_C") ?? Find(held.transform,"SOCKET_Eject");
            flash=muzzle?muzzle.GetComponent<Muzzle>():null;parts=held.GetComponent<Animator>();
            foreach(var renderer in held.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=DeadPvPvEBody || IsOwner && !ShowLocalBodyForTesting || IntroMultiplayerSinConfirmar;
            bodyRenderers=Body.GetComponentsInChildren<Renderer>(true);
        }
        if((!IsOwner || ShowLocalBodyForTesting) && (value.Flags&1)!=0 && (appliedFlags&1)==0) {
            PlayParts(definition.Source.UsesMultiPartReload?"Reload Open":(value.Flags&16)!=0?"Reload Empty":"Reload");
            var reloadSound=definition.Source.PresentationReloadSound((value.Flags&16)!=0);
            if(reloadSound && !IsOwner)audioOutput.PlayOneShot(reloadSound);
        }
        // Gloves must also affect models whose inactive FP source has not initialized its character reference.
        var perkHealth = GetComponent<PlayerHealth>();
        var perkCharacter = GetComponent<PlayerCharacter>();
        float perkReloadScale = perkHealth && perkHealth.TienePerk(DeadQuarantine.PvPvE.PassivePerk.Gloves) && perkCharacter && definition.Source
            ? perkCharacter.ReloadSpeedMultiplier / Mathf.Max(.01f, definition.Source.PresentationCharacterReloadSpeed) : 1f;
        if(parts)parts.speed=(value.Flags&1)!=0?definition.Source.PresentationThirdPersonPartsReloadSpeed((value.Flags&16)!=0)*perkReloadScale:1;
        if (BodyAnimator)
        {
            int switchLayer =
                BodyAnimator.GetLayerIndex("WeaponSwitch");

            if (switchLayer >= 0)
            {
                BodyAnimator.SetLayerWeight(
                    switchLayer,
                    (value.Flags & 96) != 0 ? 1f : 0f
                );
            }

            BodyAnimator.SetBool(
                "TP_Holstering",
                (value.Flags & 32) != 0
            );

            BodyAnimator.SetBool(
                "TP_Unholstering",
                (value.Flags & 64) != 0
            );

            /*
            * ThirdPersonAnimatorSync solamente maneja al OWNER.
            *
            * Para los jugadores remotos, NetworkWeaponPresentation
            * debe alimentar los parámetros del Animator.
            */
            if (!IsOwner)
            {
                bool reloading =
                    (value.Flags & 1) != 0;

                bool shellLoading =
                    (value.Flags & 2) != 0;

                bool chambering =
                    (value.Flags & 4) != 0;

                bool aiming =
                    (value.Flags & 8) != 0;

                bool empty =
                    (value.Flags & 16) != 0;

                BodyAnimator.SetBool(
                    "TP_IsAiming",
                    aiming
                );

                BodyAnimator.SetBool(
                    "TP_IsReloading",
                    reloading
                );

                BodyAnimator.SetBool(
                    "TP_MultiPartReload",
                    definition.Source != null &&
                    definition.Source.UsesMultiPartReload
                );

                BodyAnimator.SetBool(
                    "TP_ShellLoading",
                    shellLoading
                );

                BodyAnimator.SetBool(
                    "TP_IsChambering",
                    chambering
                );

                BodyAnimator.SetBool(
                    "TP_IsEmpty",
                    empty
                );

                PlayerCharacter character =
                    GetComponent<PlayerCharacter>();

                BodyAnimator.SetFloat(
                    "TP_ReloadSpeed",
                    definition.Source != null
                        ? definition.Source.PresentationThirdPersonReloadSpeed(empty) * perkReloadScale
                        : (character != null ? character.ReloadSpeedMultiplier : 1f)
                );
            }
        }
        GetComponent<NetworkWeaponAttachments>()?.RefreshVisibleWeapon();
        AttachBoltRifleForAction(value,definition);appliedFlags=value.Flags;
        var poseProfile=definition.Source.GetComponent<PackWeaponPresentation>();
        if((equipmentChanged||finishedDraw)&&poseProfile&&!poseProfile.preserveLegacyAnimation&&BodyAnimator&&BodyAnimator.isActiveAndEnabled){
            BodyAnimator.SetFloat("TP_AimWeight",poseProfile.bareSupportPoseEnabled||(value.Flags&8)!=0?1:0);
            // Evaluate the newly selected controller once before its first visible frame.
            // Incoming equipment updates can arrive after this frame's animation pass.
            BodyAnimator.Update(0);ApplySupportHand();
        }
    }
    public void PublishShot(Weapon source,Vector3[] targets)
    {
        if(!IsSpawned || !IsOwner || !Switcher || Switcher.CurrentWeapon!=source)return;
        // An animation can end after Update. Queue its reload-stop before the reliable shot RPC,
        // so the first valid round after a reload is not rejected against the previous frame's flag.
        var mode=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        if(mode&&mode.IsSpawned&&mode.NetworkManager==NetworkManager)PublishStateIfChanged();
        GetComponentInChildren<ThirdPersonAnimatorSync>(true)?.NotifyFire(source);
        Vector3 origin=source.AimOrigin;
        ShotServerRpc(Switcher.CurrentWeaponIndex,++shotSequence,origin,targets);
    }
    [ServerRpc] void ShotServerRpc(int index,uint sequence,Vector3 origin,Vector3[] targets)
    {
        if(!DeadQuarantine.PvPvE.Networking.PvPvETeamDeployment.AllowsOffense(GetComponent<PlayerHealth>()))return;
        if(index<0 || index>=Weapons.Length || sequence<=acceptedSequence || !Finite(origin) || targets==null || targets.Length<1 || targets.Length>16)return;
        if(!GetComponent<PlayerHealth>().ArmaPermitidaPorClase(Weapons[index].Id))return;
        var source=Weapons[index].Source;
        if(!source || Vector3.Distance(origin,transform.position+Vector3.up*1.5f)>4 || NetworkManager.ServerTime.Time+.025<nextAcceptedShot)return;
        for(int i=0;i<targets.Length;i++)if(!Finite(targets[i]) || Vector3.Distance(origin,targets[i])>source.PresentationRange+5)return;
        var combat=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        if(combat&&!combat.ResolveShotServer(GetComponent<PlayerHealth>(),index,sequence,origin,targets,out targets))return;
        GetComponent<NetworkWeaponAttachments>()?.PublishShotVisibility(index);
        acceptedSequence=sequence;nextAcceptedShot=NetworkManager.ServerTime.Time+Mathf.Max(.025f,source.PresentationFireInterval);
        ShotClientRpc(index,targets);
    }
    [ClientRpc] void ShotClientRpc(int index,Vector3[] targets)
    {
        if(IsOwner){if(ShowLocalBodyForTesting)PlayParts("Fire");return;}
        Apply(new State{Weapon=index,Flags=state.Value.Weapon==index?state.Value.Flags:(byte)0});
        if(!held || !muzzle)return;
        RemoteShotCount++;
        var source=Weapons[index].Source;
        if(source.PresentationShotSound){audioOutput.PlayOneShot(source.PresentationShotSound);RemoteSoundEvents++;}
        if(!source.ShotIsSuppressed && flash && flash.HasParticleEffect){flash.PlayEffect();RemoteFlashEvents++;}PlayParts("Fire");
        foreach(var target in targets)if(source.PresentationTrail) {
            var go=Instantiate(source.PresentationTrail,muzzle.position,Quaternion.identity);
            var trail=go.GetComponent<BulletTrail>();if(trail){trail.Init(target);RemoteTrailEvents++;}
        }
    }

    public void PublishEvent(Weapon source,WeaponEvent kind)
    {
        if(IsSpawned && IsOwner && Switcher && Switcher.CurrentWeapon==source)EventServerRpc(Switcher.CurrentWeaponIndex,kind);
    }
    [ServerRpc] void EventServerRpc(int index,WeaponEvent kind)
    {if(index>=0 && index<Weapons.Length && kind<=WeaponEvent.BoltClose)EventClientRpc(index,kind);}
    [ClientRpc] void EventClientRpc(int index,WeaponEvent kind)
    {
        if(IsOwner && !ShowLocalBodyForTesting)return;Apply(new State{Weapon=index,Flags=state.Value.Weapon==index?state.Value.Flags:(byte)0});if(!held)return;
        var source=Weapons[index].Source;var sound=source.PresentationEventSound(kind);
        if(sound && !IsOwner)audioOutput.PlayOneShot(sound);
        if((!IsOwner || ShowLocalBodyForTesting) && kind==WeaponEvent.Eject && eject && Weapons[index].Casing)
        {
            bool newWeapon = Array.Exists(new[] { "AutoPistol", "SemiRifle", "BurstRifle", "P90", "Vector", "MAC10" },
                                           id => id == Weapons[index].Id);
            Vector3 origin = eject.position + (newWeapon ? eject.right * 0.025f + eject.up * 0.015f : Vector3.zero);
            GameObject casing = Instantiate(Weapons[index].Casing, origin, eject.rotation);
            if(newWeapon && casing)
            {
                Rigidbody body = casing.GetComponent<Rigidbody>();
                if(body) body.AddForce((held.transform.right * 0.8f + Vector3.up * 0.4f) * 1.5f,
                                       ForceMode.VelocityChange);
            }
            EjectCount++;
        }
        string clip=kind switch {WeaponEvent.ReloadOpen=>null,WeaponEvent.Insert=>null,WeaponEvent.ReloadClose=>"Reload Close",WeaponEvent.BoltOpen=>null,_=>null};
        if(clip!=null)PlayParts(clip);
    }
    void PlayParts(string name) {if(parts && parts.HasState(0,Animator.StringToHash(name)))parts.Play(name,0,0);}
    static Transform Find(Transform root,string name) => Array.Find(root.GetComponentsInChildren<Transform>(true),t=>t.name==name);
    static void SetLayer(Transform root,int layer){root.gameObject.layer=layer;foreach(Transform child in root)SetLayer(child,layer);}
    static bool Finite(Vector3 p) => float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
}
