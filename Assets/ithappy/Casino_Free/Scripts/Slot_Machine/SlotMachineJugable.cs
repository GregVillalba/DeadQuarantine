using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace ithappy.Casino
{
    /// <summary>
    /// Va en la máquina, junto a PresetUVSlotMachine (que queda SIN modificar).
    /// Maneja las chances, decide si el giro gana o pierde y, al ganar, suelta agua y gasolina.
    /// La alarma y las virutas las dispara PresetUVSlotMachine solo, porque el preset elegido tiene isWin.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PresetUVSlotMachine))]
    public sealed class SlotMachineJugable : MonoBehaviour
    {
        private const string JackpotMessage = "DeadQuarantine.SlotMachineJackpot";
        private static bool jackpotMessageRegistered;

        [Header("Chances")]
        [SerializeField, Min(1)] private int chancesMaximas = 5;
        [Header("Probabilidad de ganar según dificultad")]
        [SerializeField, Range(0f, 100f)] private float probabilidadNormal = 50f;
        [SerializeField, Range(0f, 100f)] private float probabilidadDificil = 40f;
        [SerializeField, Range(0f, 100f)] private float probabilidadPesadilla = 30f;

        private enum NivelDificultad { Normal, Dificil, Pesadilla }

        [Header("Presets (posición en la lista 'Presets' de PresetUVSlotMachine, empieza en 0)")]
        [Tooltip("Índices de los presets con 4 sietes (isWin = true).")]
        [SerializeField] private int[] presetsGanadores = { 0, 1, 2 };
        [Tooltip("Índices de los presets que pierden (isWin = false).")]
        [SerializeField] private int[] presetsPerdedores = { 3, 4, 5, 6, 7 };

        [Header("Preset y recurso de cada jackpot (índices desde 0)")]
        [SerializeField, Min(0)] private int presetSietes = 0;
        [SerializeField, Min(0)] private int presetDiamantes = 1;
        [SerializeField, Min(0)] private int presetCerezas = 2;

        [Header("Recursos al ganar")]
        [Tooltip("Prefab del recurso en el piso (RecursoDrop). Tiene que estar en DefaultNetworkPrefabs.")]
        [SerializeField] private RecursoDrop prefabRecurso;
        [SerializeField, Min(0.1f)] private float separacionDrops = 0.5f;

        [Header("Ubicación del drop")]
        [Tooltip("Adelante de la máquina, a la altura del piso. Si queda vacío se usa la máquina.")]
        [SerializeField] private Transform puntoDeSalida;
        [SerializeField] private float alturaSobreElPiso = 0.35f;
        [SerializeField] private float separacion = 0.5f;
        [SerializeField, Min(0f)] private float retrasoPremio = 0.3f;

        private PresetUVSlotMachine machine;
        private int chancesRestantes;
        private TipoRecurso recursoPremioActual;

        public int ChancesLeft => chancesRestantes;
        public bool IsSpinning => machine != null && machine.IsSpinning;
        public bool CanPlay => !IsSpinning && chancesRestantes > 0;

        private void Awake()
        {
            machine = GetComponent<PresetUVSlotMachine>();
            chancesRestantes = chancesMaximas;
            if (presetsGanadores == null || presetsGanadores.Length == 0)
                presetsGanadores = new[] { presetSietes, presetDiamantes, presetCerezas };
            if (presetsPerdedores == null || presetsPerdedores.Length == 0)
                presetsPerdedores = new[] { 3, 4, 5, 6, 7 };
            RegistrarMensajePremio();
        }

        private static void RegistrarMensajePremio()
        {
            NetworkManager red = NetworkManager.Singleton;
            if (jackpotMessageRegistered || red == null || red.CustomMessagingManager == null)
                return;

            red.CustomMessagingManager.RegisterNamedMessageHandler(JackpotMessage, RecibirSolicitudPremio);
            jackpotMessageRegistered = true;
        }

        /// <summary>Gasta una chance y gira. Devuelve false si no se pudo.</summary>
        public bool TryPlay()
        {
            if (!isActiveAndEnabled || !CanPlay)
                return false;

            bool gana = Random.Range(0f, 100f) < ProbabilidadActual();
            int indice = ElegirPreset(gana);

            if (indice < 0)
            {
                Debug.LogError($"[SlotMachineJugable] No hay índices de preset configurados en {name}.", this);
                return false;
            }

            machine.SpinPresetByIndex(indice);

            // Si el preset no existe la máquina no gira: no se gasta la chance.
            if (!machine.IsSpinning)
                return false;

            if (!TryGetRewardType(indice, out recursoPremioActual))
                recursoPremioActual = TipoRecurso.PilaDeLatas;

            chancesRestantes--;
            StartCoroutine(EsperarResultado(gana));
            return true;
        }

        private float ProbabilidadActual()
        {
            switch (DificultadActual())
            {
                case NivelDificultad.Dificil: return probabilidadDificil;
                case NivelDificultad.Pesadilla: return probabilidadPesadilla;
                default: return probabilidadNormal;
            }
        }

        // PENDIENTE: conectar con el lugar donde tu juego guarda la dificultad de la partida.
        // Mientras tanto devuelve Normal.
        private NivelDificultad DificultadActual()
        {
            return NivelDificultad.Normal;
        }

        private int ElegirPreset(bool gana)
        {
            int[] configurados = gana ? presetsGanadores : presetsPerdedores;
            var candidatos = new System.Collections.Generic.List<int>();

            if (configurados != null)
                foreach (int indice in configurados)
                    if (machine.IsPresetWin(indice) == gana)
                        candidatos.Add(indice);

            // Si el prefab tenía índices antiguos, usar automáticamente todos los presets
            // cuyo isWin coincide con el resultado solicitado.
            if (candidatos.Count == 0)
                for (int i = 0; i < machine.PresetCount; i++)
                    if (machine.IsPresetWin(i) == gana)
                        candidatos.Add(i);

            return candidatos.Count == 0 ? -1 : candidatos[Random.Range(0, candidatos.Count)];
        }

        private IEnumerator EsperarResultado(bool gano)
        {
            while (machine.IsSpinning)
                yield return null;

            if (!gano)
                yield break;

            if (retrasoPremio > 0f)
                yield return new WaitForSeconds(retrasoPremio);

            NetworkManager red = NetworkManager.Singleton;
            if (red == null || !red.IsListening || red.IsServer)
            {
                SoltarSuministros(recursoPremioActual);
                yield break;
            }

            // El giro se resuelve localmente en el cliente. El servidor debe crear el drop
            // para que RecursoDrop lo replique al resto de la partida.
            RegistrarMensajePremio();
            if (red.CustomMessagingManager != null)
            {
                Vector3 posicion = (puntoDeSalida != null ? puntoDeSalida : transform).position;
                using (var writer = new FastBufferWriter(sizeof(float) * 3 + sizeof(byte), Allocator.Temp))
                {
                    writer.WriteValueSafe(posicion.x);
                    writer.WriteValueSafe(posicion.y);
                    writer.WriteValueSafe(posicion.z);
                    writer.WriteValueSafe((byte)recursoPremioActual);
                    red.CustomMessagingManager.SendNamedMessage(JackpotMessage, NetworkManager.ServerClientId, writer);
                }
            }
        }

        private static void RecibirSolicitudPremio(ulong remitente, FastBufferReader reader)
        {
            NetworkManager red = NetworkManager.Singleton;
            if (red == null || !red.IsServer ||
                !red.ConnectedClients.TryGetValue(remitente, out NetworkClient cliente) ||
                cliente.PlayerObject == null)
                return;

            reader.ReadValueSafe(out float x);
            reader.ReadValueSafe(out float y);
            reader.ReadValueSafe(out float z);
            reader.ReadValueSafe(out byte tipoByte);
            if (!System.Enum.IsDefined(typeof(TipoRecurso), (int)tipoByte))
                return;
            TipoRecurso tipoPremio = (TipoRecurso)tipoByte;
            Vector3 posicionReportada = new Vector3(x, y, z);

            SlotMachineJugable maquinaCercana = null;
            float distanciaMinima = float.MaxValue;
            foreach (SlotMachineJugable candidata in FindObjectsOfType<SlotMachineJugable>())
            {
                Transform origen = candidata.puntoDeSalida != null ? candidata.puntoDeSalida : candidata.transform;
                float distancia = Vector3.Distance(origen.position, posicionReportada);
                if (distancia < distanciaMinima)
                {
                    distanciaMinima = distancia;
                    maquinaCercana = candidata;
                }
            }

            if (maquinaCercana == null || distanciaMinima > 2f)
                return;

            Transform salida = maquinaCercana.puntoDeSalida != null
                ? maquinaCercana.puntoDeSalida
                : maquinaCercana.transform;
            if (Vector3.Distance(cliente.PlayerObject.transform.position, salida.position) > 6f)
                return;

            maquinaCercana.SoltarSuministros(tipoPremio);
        }

        /// <summary>Solo servidor/host, igual que DropRecursosZombie.Soltar().</summary>
        private void SoltarSuministros(TipoRecurso tipo)
        {
            NetworkManager red = NetworkManager.Singleton;

            if (red == null || !red.IsServer || prefabRecurso == null)
                return;

            Transform origen = puntoDeSalida != null ? puntoDeSalida : transform;
            Vector3 lado = origen.right * (separacionDrops * 0.5f);
            Vector3 centro = origen.position + Vector3.up * alturaSobreElPiso;

            SoltarUno(tipo, centro - lado);
            SoltarUno(tipo, centro + lado);
        }

        private void SoltarUno(TipoRecurso tipo, Vector3 posicion)
        {
            RecursoDrop recurso = Instantiate(prefabRecurso, posicion, Quaternion.identity);
            recurso.Configurar(tipo, 1);
            recurso.NetworkObject.Spawn(true);
        }

        private bool TryGetRewardType(int presetIndex, out TipoRecurso tipo)
        {
            if (presetIndex == presetCerezas) { tipo = TipoRecurso.PilaDeLatas; return true; }
            if (presetIndex == presetDiamantes) { tipo = TipoRecurso.Cantimplora; return true; }
            if (presetIndex == presetSietes) { tipo = TipoRecurso.Bidon; return true; }
            tipo = default;
            return false;
        }
    }
}
