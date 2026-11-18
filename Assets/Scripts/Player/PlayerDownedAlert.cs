using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections.Generic;

// Aviso en pantalla cuando un compañero está abatido, con la cuenta regresiva
// del tiempo que queda para reanimarlo. Va en el prefab del jugador,
// junto a PlayerHealth. Solo actúa en el dueño.
public class PlayerDownedAlert : NetworkBehaviour
{
    
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("UI")]
    [SerializeField] private GameObject panelAviso;
    [Tooltip("{0} se reemplaza por los segundos restantes.")]

    [SerializeField] private TextMeshProUGUI[] textosAviso;   // 3 líneas (máx. 3 compañeros)
    [Tooltip("{0} = nombre del jugador.")]
    [SerializeField] private string formatoTexto = "{0} fue abatido";
    [Tooltip("Segundos que se muestra el aviso de cada compañero abatido.")]
    [SerializeField] private float duracionAviso = 5f;

private readonly Dictionary<PlayerHealth, float> vencimientos = new Dictionary<PlayerHealth, float>();
private readonly List<PlayerHealth> paraQuitar = new List<PlayerHealth>();
private readonly List<(string nombre, float vence)> abatidos = new List<(string nombre, float vence)>();

    [Header("Urgencia")]
    [Tooltip("Por debajo de estos segundos el texto cambia de color.")]
    [SerializeField] private float umbralUrgente = 3f;
    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorUrgente = Color.red;

    [Header("Búsqueda")]
    [SerializeField] private float intervaloBusqueda = 0.25f;

    private PlayerHealth[] jugadores = new PlayerHealth[0];
    private float proximaBusqueda;
    

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void OnDisable()
    {
        Ocultar();
    }

    public override void OnNetworkDespawn()
    {
        Ocultar();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        // Si yo no estoy de pie, el aviso no sirve.
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            Ocultar();
            return;
        }

        if (Time.time >= proximaBusqueda)
        {
            proximaBusqueda = Time.time + intervaloBusqueda;

            jugadores =
                FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        }

        // Si hay varios abatidos, se muestra al que menos tiempo le queda.
     /*   bool hayAbatido = false;
        float menorTiempo = float.MaxValue;

        foreach (PlayerHealth jugador in jugadores)
        {
            if (jugador == null || jugador == playerHealth)
                continue;

            if (!jugador.IsSpawned || !jugador.IsDowned)
                continue;

            float tiempo = jugador.DownedTimeRemaining.Value;

            if (tiempo < menorTiempo)
            {
                menorTiempo = tiempo;
                hayAbatido = true;
            }
        }

        if (!hayAbatido)
        {
            Ocultar();
            return;
        }*/

    abatidos.Clear();
paraQuitar.Clear();

// Quitar a los que ya no están abatidos, para que una nueva caída vuelva a avisar.
foreach (var par in vencimientos)
{
    if (par.Key == null || !par.Key.IsSpawned || !par.Key.IsDowned)
        paraQuitar.Add(par.Key);
}
foreach (PlayerHealth quitar in paraQuitar)
    vencimientos.Remove(quitar);

foreach (PlayerHealth jugador in jugadores)
{
    if (jugador == null || jugador == playerHealth)
        continue;

    if (!jugador.IsSpawned || !jugador.IsDowned)
        continue;

    // Primera vez que se lo ve abatido: arranca su cuenta de 5 s.
    if (!vencimientos.TryGetValue(jugador, out float vence))
    {
        vence = Time.time + duracionAviso;
        vencimientos[jugador] = vence;
    }

    if (Time.time >= vence)
        continue;

    PlayerNameplate rotulo = jugador.GetComponentInChildren<PlayerNameplate>(true);
    string nombre = rotulo != null ? rotulo.UsernameOnly : "JUGADOR";

    abatidos.Add((nombre, vence));
}

if (abatidos.Count == 0)
{
    Ocultar();
    return;
}

abatidos.Sort((a, b) => a.vence.CompareTo(b.vence));
Mostrar();
    }
    private void Mostrar()
{
    if (panelAviso != null && !panelAviso.activeSelf)
        panelAviso.SetActive(true);

    if (textosAviso == null)
        return;

    for (int i = 0; i < textosAviso.Length; i++)
    {
        TextMeshProUGUI texto = textosAviso[i];
        if (texto == null)
            continue;

        if (i >= abatidos.Count)
        {
            if (texto.gameObject.activeSelf)
                texto.gameObject.SetActive(false);
            continue;
        }

        if (!texto.gameObject.activeSelf)
            texto.gameObject.SetActive(true);

        texto.text = string.Format(formatoTexto, abatidos[i].nombre);
        texto.color = colorNormal;
    }
}

    private void Ocultar()
    {
        if (panelAviso != null && panelAviso.activeSelf)
            panelAviso.SetActive(false);
    }
}
