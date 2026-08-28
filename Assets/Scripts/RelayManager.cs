using System.Threading.Tasks;
using TMPro;
using UnityEngine;

// DESCOMENTAR CUANDO USES MULTIPLAYER REAL:
// using System;
// using Unity.Netcode;
// using Unity.Netcode.Transports.UTP;
// using Unity.Services.Core;
// using Unity.Services.Authentication;
// using Unity.Services.Relay;
// using Unity.Services.Relay.Models;
// using Unity.Networking.Transport.Relay;

public class RelayManager : MonoBehaviour
{
    [Header("Paneles Principales")]
    [SerializeField] private GameObject navGenerarCod;
    [SerializeField] private GameObject navIngresarCod;

    [Header("Componentes de nav_generar_cod")]
    [SerializeField] private TMP_Text txtCodigoGenerado; // Arrastrar 'Text (TMP) (1)'

    [Header("Componentes de nav_ingresar_cod")]
    [SerializeField] private TMP_InputField inputField; // Arrastrar 'InputField'

    private string joinCodeGenerado = "";

    // DESCOMENTAR PARA MULTIPLAYER:
    // private Allocation hostAllocation;

    private async void Start()
    {
        /*
        // DESCOMENTAR PARA MULTIPLAYER:
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"Autenticado en Unity Services. ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error de inicialización: {e.Message}");
        }
        */
        await Task.CompletedTask;
    }

    // --- ABRIR Y CERRAR NAVEGADORES ---

    public async void AbrirNavGenerarCod()
    {
        if (navGenerarCod != null) navGenerarCod.SetActive(true);
        if (navIngresarCod != null) navIngresarCod.SetActive(false);

        // Genera código simulado para pruebas
        joinCodeGenerado = "DQ-" + Random.Range(1000, 9999);
        if (txtCodigoGenerado != null)
            txtCodigoGenerado.text = $"CÓDIGO: {joinCodeGenerado}";

        /*
        // DESCOMENTAR PARA MULTIPLAYER:
        try
        {
            if (txtCodigoGenerado != null) txtCodigoGenerado.text = "GENERANDO...";
            hostAllocation = await RelayService.Instance.CreateAllocationAsync(1);
            joinCodeGenerado = await RelayService.Instance.GetJoinCodeAsync(hostAllocation.AllocationId);
            if (txtCodigoGenerado != null) txtCodigoGenerado.text = $"CÓDIGO: {joinCodeGenerado}";
        }
        catch (Exception e)
        {
            Debug.LogError($"Error al crear Relay: {e.Message}");
            if (txtCodigoGenerado != null) txtCodigoGenerado.text = "ERROR AL CREAR";
        }
        */
        await Task.CompletedTask;
    }

    public void AbrirNavIngresarCod()
    {
        if (navIngresarCod != null) navIngresarCod.SetActive(true);
        if (navGenerarCod != null) navGenerarCod.SetActive(false);
    }

    public void ButtonCerrarNav()
    {
        if (navGenerarCod != null) navGenerarCod.SetActive(false);
        if (navIngresarCod != null) navIngresarCod.SetActive(false);
    }

    // --- ACCIONES DE LOS BOTONES ---

    public void ButtonCopiarCod()
    {
        if (!string.IsNullOrEmpty(joinCodeGenerado))
        {
            GUIUtility.systemCopyBuffer = joinCodeGenerado;
            Debug.Log($"Código copiado: {joinCodeGenerado}");
        }
    }

    // Botón 'Button_ir_a_sala' dentro de nav_generar_cod (Host)
    public void ButtonIrASalaHost()
    {
        Debug.Log("Iniciando sala como Host...");
        ButtonCerrarNav();

        /*
        // DESCOMENTAR PARA MULTIPLAYER:
        if (hostAllocation == null) return;
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(AllocationUtils.ToRelayServerData(hostAllocation, "dtls"));
        NetworkManager.Singleton.StartHost();
        */
    }

    // Botón 'Button_ir_a_sala' dentro de nav_ingresar_cod (Cliente)
    public async void ButtonIrASalaClient()
    {
        string codigoIngresado = inputField != null ? inputField.text.Trim().ToUpper() : "";

        if (string.IsNullOrEmpty(codigoIngresado))
        {
            Debug.LogWarning("Por favor ingresa un código.");
            return;
        }

        Debug.Log($"Conectando a la sala con código: {codigoIngresado}");
        ButtonCerrarNav();

        /*
        // DESCOMENTAR PARA MULTIPLAYER:
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(codigoIngresado);
            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));
            NetworkManager.Singleton.StartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error al unirse al Relay: {e.Message}");
        }
        */
        await Task.CompletedTask;
    }
}