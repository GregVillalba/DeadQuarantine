using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Linterna del jugador. Va en la raíz del prefab Player (el que tiene el NetworkObject).
///
/// Montaje:
///  1) Dentro de PlayerCamera creá un hijo vacío "FlashlightPivot" y, dentro de él, un Spot Light "Flashlight"
///     (ver valores sugeridos en la guía). Arrastrá el pivote a 'Light Pivot' y la luz a 'Flashlight Light'.
///  2) Asigná este script al Player y dejá la luz apagada (Light.enabled = false) en el prefab.
///  3) En el escenario donde se permita la linterna (el laboratorio), agregá un FlashlightSceneSettings.
///
/// Se enciende y apaga con la tecla de 'Toggle Action' (F por defecto). El estado se sincroniza por red
/// (lo escribe el dueño del jugador) para que tu compañero también vea el haz de luz.
/// </summary>
[DefaultExecutionOrder(1200)]
public class Flashlight : NetworkBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Light flashlightLight;
    [Tooltip("Objeto que sostiene la luz (hijo de la cámara). Se usa para el balanceo suave.")]
    [SerializeField] private Transform lightPivot;

    [Header("Control")]
    [SerializeField]
    private InputAction toggleAction = new InputAction("Linterna", InputActionType.Button, "<Keyboard>/f");

    [Header("Balanceo")]
    [Tooltip("La luz se retrasa un poco al girar la cámara, como si la linterna se moviera en la mano.")]
    [SerializeField] private bool sway = true;
    [Tooltip("Más alto = sigue a la cámara más rápido.")]
    [SerializeField] private float swaySpeed = 12f;

    [Header("Sonido (opcional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip toggleClip;

    private readonly NetworkVariable<bool> isOn = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<bool> purchased=new(false,NetworkVariableReadPermission.Everyone,NetworkVariableWritePermission.Server);
    PlayerHealth life;
    public event System.Action AvailabilityChanged;bool observedPurchase,observedAvailability;
    public bool Purchased=>IsSpawned&&purchased.Value;
    bool ClassUtility {get{var mode=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;return mode&&mode.IsSpawned&&mode.NetworkManager==NetworkManager;}}
    public void GrantPurchaseServer(){if(IsServer)purchased.Value=true;}
    void LifeChanged(PlayerHealth.PlayerState before,PlayerHealth.PlayerState after){if(after is PlayerHealth.PlayerState.Dead or PlayerHealth.PlayerState.Spectating){if(IsServer)purchased.Value=false;if(IsOwner)isOn.Value=false;}}
    NetworkWeaponPresentation presentation;Transform originalLightParent;Vector3 originalLightPosition;Quaternion originalLightRotation;
    WeaponAttachments EquippedAttachment {
        get {
            if(!presentation)presentation=GetComponent<NetworkWeaponPresentation>();
            if(!presentation)return null;
            if(IsOwner)return presentation.Switcher&&presentation.Switcher.CurrentWeapon?presentation.Switcher.CurrentWeapon.GetComponent<WeaponAttachments>():null;
            return presentation.VisibleWeapon?presentation.VisibleWeapon.GetComponent<WeaponAttachments>():null;
        }
    }
    public bool UtilityEnabled=>IsSpawned&&isOn.Value;
    public bool FlashlightAvailable=>EquippedAttachment&&EquippedAttachment.HasFlashlight&&(ClassUtility||Purchased);
    public bool CompatibleWithEquippedWeapon=>EquippedAttachment&&EquippedAttachment.HasRuntimeCatalog&&EquippedAttachment.Catalog.Allows(EquippedAttachment.StableWeaponId,WeaponAttachmentKind.Utility,"Flashlight_01");
    public bool LaserAvailable=>EquippedAttachment&&EquippedAttachment.HasLaser;
    public bool BeamVisible=>flashlightLight&&flashlightLight.enabled&&flashlightLight.gameObject.activeInHierarchy;

    public override void OnNetworkSpawn()
    {
        if(flashlightLight){originalLightParent=flashlightLight.transform.parent;originalLightPosition=flashlightLight.transform.localPosition;originalLightRotation=flashlightLight.transform.localRotation;
            // Remote first-person cameras are inactive; move the same light out of that hidden hierarchy.
            if(!IsOwner)flashlightLight.transform.SetParent(transform,true);
        }
        life=GetComponent<PlayerHealth>();if(life)life.State.OnValueChanged+=LifeChanged;
        isOn.OnValueChanged += OnIsOnChanged;
        Apply(isOn.Value);

        if (IsOwner)
        {
            toggleAction.performed += OnTogglePerformed;
            toggleAction.Enable();
        }
    }

    public override void OnNetworkDespawn()
    {
        if(life)life.State.OnValueChanged-=LifeChanged;
        isOn.OnValueChanged -= OnIsOnChanged;
        if(flashlightLight){flashlightLight.enabled=false;if(originalLightParent){flashlightLight.transform.SetParent(originalLightParent,false);flashlightLight.transform.localPosition=originalLightPosition;flashlightLight.transform.localRotation=originalLightRotation;}}

        toggleAction.performed -= OnTogglePerformed;
        toggleAction.Disable();
    }

    private void OnTogglePerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        // Reuse F for the utility fitted to the currently equipped gun.
        if (Time.timeScale <= 0f || PlayerInputGate.ActionsBlocked || PauseController.LocalPlayerPaused)
            return;

        if (!FlashlightAvailable && !LaserAvailable) return;
        var health=GetComponent<PlayerHealth>();if(health&&!health.IsAlive)return;

        isOn.Value = !isOn.Value;
    }

    private void OnIsOnChanged(bool previous, bool current)
    {
        Apply(current);

        if (audioSource != null && toggleClip != null)
            audioSource.PlayOneShot(toggleClip);
    }

    private void Apply(bool on)
    {
        if (flashlightLight != null)
            flashlightLight.enabled = on && FlashlightAvailable;
    }

    private void LateUpdate()
    {
        var attachment=EquippedAttachment;
        bool available=FlashlightAvailable||LaserAvailable;
        bool owned=Purchased,beam=FlashlightAvailable;
        if(owned!=observedPurchase||beam!=observedAvailability){observedPurchase=owned;observedAvailability=beam;AvailabilityChanged?.Invoke();}
        if(IsSpawned&&IsOwner&&isOn.Value&&!available)isOn.Value=false;
        Apply(IsSpawned&&isOn.Value);
        if(!IsOwner && attachment && flashlightLight && flashlightLight.enabled){
            flashlightLight.transform.SetPositionAndRotation(attachment.UtilityOrigin,Quaternion.LookRotation(attachment.UtilityDirection));
            return;
        }
        if (!sway || lightPivot == null || lightPivot.parent == null)
            return;

        if (flashlightLight == null || !flashlightLight.enabled)
            return;

        float t = 1f - Mathf.Exp(-swaySpeed * Time.deltaTime);
        lightPivot.rotation = Quaternion.Slerp(lightPivot.rotation, lightPivot.parent.rotation, t);
    }
}