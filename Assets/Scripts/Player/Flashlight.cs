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

    public override void OnNetworkSpawn()
    {
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
        isOn.OnValueChanged -= OnIsOnChanged;

        toggleAction.performed -= OnTogglePerformed;
        toggleAction.Disable();
    }

    private void OnTogglePerformed(InputAction.CallbackContext context)
    {
        if (!IsOwner)
            return;

        // En pausa no se prende ni se apaga.
        if (Time.timeScale <= 0f)
            return;

        if (!FlashlightSceneSettings.Allowed)
            return;

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
            flashlightLight.enabled = on && FlashlightSceneSettings.Allowed;
    }

    private void LateUpdate()
    {
        if (!sway || lightPivot == null || lightPivot.parent == null)
            return;

        if (flashlightLight == null || !flashlightLight.enabled)
            return;

        float t = 1f - Mathf.Exp(-swaySpeed * Time.deltaTime);
        lightPivot.rotation = Quaternion.Slerp(lightPivot.rotation, lightPivot.parent.rotation, t);
    }
}