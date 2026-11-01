using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Bloquea TODAS las armas del jugador mientras la tienda está abierta.
///
/// Soluciona dos problemas:
/// 1. Un arma recién comprada podía disparar dentro de la tienda.
/// 2. Una pistola que estaba equipada al abrir la tienda podía quedar
///    permanentemente con InputLocked = true después de comprar/cambiar arma.
///
/// Este componente recuerda el estado que tenía CADA arma antes de abrir
/// la tienda y lo restaura al cerrarla.
///
/// IMPORTANTE:
/// - Colocarlo en el Player, en un objeto que permanezca activo.
/// - Asignar Shop Panel en el Inspector.
/// - NO colocarlo dentro del propio ShopPanel.
/// </summary>
[DefaultExecutionOrder(-500)]
public sealed class ShopWeaponInputGuard : MonoBehaviour
{
    [Header("Tienda")]

    [Tooltip(
        "Arrastrá acá el GameObject principal de la tienda, por ejemplo ShopPanel. " +
        "Se usa como comprobación adicional a NPCShopUI.LocalShopOpen."
    )]
    [SerializeField]
    private GameObject shopPanel;

    [Header("Armas")]

    [Tooltip(
        "Raíz donde están las armas. Si queda vacío se usa automáticamente " +
        "la raíz del Player."
    )]
    [SerializeField]
    private Transform weaponRoot;

    [Header("Diagnóstico")]

    [SerializeField]
    private bool debugLogs = false;

    private NetworkObject networkObject;

    /// <summary>
    /// Estado que tenía cada arma ANTES de abrir la tienda.
    /// </summary>
    private readonly Dictionary<Weapon, bool> savedLocks =
        new Dictionary<Weapon, bool>();

    /// <summary>
    /// Último estado conocido de las armas mientras la tienda estaba cerrada.
    ///
    /// Es importante porque NPCShopUI podría poner InputLocked=true
    /// antes de que detectemos que la tienda se abrió.
    /// </summary>
    private readonly Dictionary<Weapon, bool> normalLocks =
        new Dictionary<Weapon, bool>();

    private bool shopWasOpen;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        networkObject =
            GetComponentInParent<NetworkObject>();

        if (weaponRoot == null)
            weaponRoot = transform.root;

        CacheNormalWeaponStates();
    }

    private void Start()
    {
        CacheNormalWeaponStates();

        shopWasOpen =
            IsShopActuallyOpen();

        if (shopWasOpen)
            BeginShopLock();
    }

    private void Update()
    {
        // En multiplayer solamente maneja el jugador local.
        if (
            networkObject != null &&
            networkObject.IsSpawned &&
            !networkObject.IsOwner
        )
        {
            return;
        }

        bool shopOpen =
            IsShopActuallyOpen();

        // -----------------------------------------------------
        // TIENDA CERRADA
        // -----------------------------------------------------

        if (!shopOpen)
        {
            if (shopWasOpen)
            {
                EndShopLock();
                shopWasOpen = false;
            }

            /*
             * Mientras estamos jugando normalmente vamos
             * recordando el estado real de InputLocked.
             *
             * Así, si por ejemplo otro sistema tenía un arma
             * bloqueada legítimamente, no le pisamos ese estado.
             */
            CacheNormalWeaponStates();

            return;
        }

        // -----------------------------------------------------
        // ACABA DE ABRIR
        // -----------------------------------------------------

        if (!shopWasOpen)
        {
            BeginShopLock();
            shopWasOpen = true;
        }

        // -----------------------------------------------------
        // TIENDA ABIERTA
        // -----------------------------------------------------

        /*
         * Se hace TODOS los frames a propósito.
         *
         * Si el jugador compra un rifle o escopeta mientras
         * la tienda está abierta, esa nueva arma aparece después
         * del BeginShopLock().
         *
         * Acá la detectamos y la bloqueamos también.
         */
        LockEveryWeapon();
    }

    private void OnDisable()
    {
        /*
         * Nunca dejamos un arma bloqueada por este script si
         * el componente se desactiva inesperadamente.
         */
        if (shopWasOpen)
            RestoreWeapons();

        shopWasOpen = false;
    }

    private void OnDestroy()
    {
        if (shopWasOpen)
            RestoreWeapons();

        shopWasOpen = false;
    }

    // =========================================================
    // DETECTAR TIENDA
    // =========================================================

    private bool IsShopActuallyOpen()
    {
        /*
         * Usamos DOS comprobaciones:
         *
         * 1. La bandera oficial de NPCShopUI.
         * 2. El panel visible.
         *
         * Así no dependemos únicamente de una de las dos.
         */

        bool staticFlag =
            NPCShopUI.LocalShopOpen;

        bool panelVisible =
            shopPanel != null &&
            shopPanel.activeInHierarchy;

        return staticFlag || panelVisible;
    }

    // =========================================================
    // ABRIR
    // =========================================================

    private void BeginShopLock()
    {
        savedLocks.Clear();

        Weapon[] weapons =
            GetAllWeapons();

        foreach (Weapon weapon in weapons)
        {
            if (weapon == null)
                continue;

            /*
             * Preferimos el estado que conocíamos ANTES
             * de abrir la tienda.
             *
             * Esto evita guardar accidentalmente el true
             * que NPCShopUI pudo haber puesto al abrir.
             */
            bool previousLock = false;

            if (
                normalLocks.TryGetValue(
                    weapon,
                    out bool cachedLock
                )
            )
            {
                previousLock =
                    cachedLock;
            }

            savedLocks[weapon] =
                previousLock;

            weapon.ForceStopAiming();
            weapon.InputLocked = true;

            if (debugLogs)
            {
                Debug.Log(
                    "[ShopWeaponInputGuard] BLOQUEADA: " +
                    weapon.gameObject.name +
                    " | estado anterior=" +
                    previousLock,
                    weapon
                );
            }
        }
    }

    // =========================================================
    // MIENTRAS ESTÁ ABIERTA
    // =========================================================

    private void LockEveryWeapon()
    {
        Weapon[] weapons =
            GetAllWeapons();

        foreach (Weapon weapon in weapons)
        {
            if (weapon == null)
                continue;

            /*
             * ¿Es un arma que apareció DESPUÉS de abrir
             * la tienda? Por ejemplo un rifle comprado.
             */
            if (!savedLocks.ContainsKey(weapon))
            {
                bool previousLock = false;

                if (
                    normalLocks.TryGetValue(
                        weapon,
                        out bool cachedLock
                    )
                )
                {
                    previousLock =
                        cachedLock;
                }

                /*
                 * Si el arma es nueva durante la compra,
                 * normalmente su estado correcto previo es false.
                 */
                savedLocks.Add(
                    weapon,
                    previousLock
                );

                if (debugLogs)
                {
                    Debug.Log(
                        "[ShopWeaponInputGuard] Nueva arma detectada en tienda: " +
                        weapon.gameObject.name,
                        weapon
                    );
                }
            }

            weapon.ForceStopAiming();

            /*
             * Aunque WeaponSwitcher/NPCShopUI intente activarla
             * o desbloquearla después de comprarla, vuelve a
             * quedar bloqueada en este frame.
             */
            if (!weapon.InputLocked)
                weapon.InputLocked = true;
        }
    }

    // =========================================================
    // CERRAR
    // =========================================================

    private void EndShopLock()
    {
        RestoreWeapons();

        /*
         * Volvemos a reconstruir el cache con el estado limpio.
         */
        CacheNormalWeaponStates();

        if (debugLogs)
        {
            Debug.Log(
                "[ShopWeaponInputGuard] Tienda cerrada. " +
                "Estados de armas restaurados."
            );
        }
    }

    private void RestoreWeapons()
    {
        foreach (
            KeyValuePair<Weapon, bool> pair
            in savedLocks
        )
        {
            Weapon weapon =
                pair.Key;

            if (weapon == null)
                continue;

            weapon.InputLocked =
                pair.Value;

            if (debugLogs)
            {
                Debug.Log(
                    "[ShopWeaponInputGuard] RESTAURADA: " +
                    weapon.gameObject.name +
                    " -> InputLocked=" +
                    pair.Value,
                    weapon
                );
            }
        }

        savedLocks.Clear();
    }

    // =========================================================
    // ESTADO NORMAL
    // =========================================================

    private void CacheNormalWeaponStates()
    {
        Weapon[] weapons =
            GetAllWeapons();

        /*
         * Limpiamos referencias destruidas.
         */
        List<Weapon> deadWeapons =
            null;

        foreach (
            KeyValuePair<Weapon, bool> pair
            in normalLocks
        )
        {
            if (pair.Key != null)
                continue;

            if (deadWeapons == null)
                deadWeapons =
                    new List<Weapon>();

            deadWeapons.Add(pair.Key);
        }

        if (deadWeapons != null)
        {
            foreach (Weapon dead in deadWeapons)
                normalLocks.Remove(dead);
        }

        foreach (Weapon weapon in weapons)
        {
            if (weapon == null)
                continue;

            normalLocks[weapon] =
                weapon.InputLocked;
        }
    }

    // =========================================================
    // BUSCAR ARMAS
    // =========================================================

    private Weapon[] GetAllWeapons()
    {
        if (weaponRoot == null)
            weaponRoot = transform.root;

        return weaponRoot
            .GetComponentsInChildren<Weapon>(
                true
            );
    }
}