#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace DeadQuarantine.Development
{
    public sealed class PvPvEPhase10HealingDriver : MonoBehaviour
    {
        public PvPvENetworkSettings Settings;
        public GameObject PlayerPrefab;
        string role, folder; bool finished; Keyboard keyboard;
        PvPvEMatchmaking service;
        readonly List<string> checks = new();
        NetworkManager Network => service.Network;
        PlayerHealth Local => Network && Network.LocalClient?.PlayerObject ? Network.LocalClient.PlayerObject.GetComponent<PlayerHealth>() : null;
        PlayerHealth[] Players => FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Where(p => p.IsSpawned).OrderBy(p => p.OwnerClientId).ToArray();
        [Serializable] sealed class Result { public bool ok; public string role, error; public string[] checks; }
        static string Arg(string key) { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        void Awake()
        {
            var selected = new GameObject("Phase10 fixture selection").AddComponent<ConfiguracionPartidaSeleccionada>();
            selected.escenarioSeleccionado = ConfiguracionPartidaSeleccionada.Escenario.Laboratorio;
            selected.modoSeleccionado = ConfiguracionPartidaSeleccionada.ModoJuego.Supervivencia;
        }
        void Start()
        {
            role = Arg("-dq-role"); if (role == null) { enabled = false; return; }
            folder = Arg("-dq-output"); Directory.CreateDirectory(folder); Application.runInBackground = true;
            StartCoroutine(Safe(Run()));
        }
        IEnumerator Safe(IEnumerator routine)
        {
            while (!finished)
            {
                object value;
                try { if (!routine.MoveNext()) yield break; value = routine.Current; }
                catch (Exception e) { Complete(false, e.ToString()); yield break; }
                if (value is IEnumerator child) yield return Safe(child); else yield return value;
            }
        }
        IEnumerator Until(Func<bool> predicate, string label, float seconds = 35)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!predicate()) { if (Time.realtimeSinceStartup > end) throw new TimeoutException(label + " " + service?.Error); yield return null; }
        }
        IEnumerator Wait(Task task) { yield return Until(() => task.IsCompleted, "Network operation"); if (task.IsFaulted) throw task.Exception; }
        void Write(string name, string value = "ok") => File.WriteAllText(Path.Combine(folder, name), value);
        bool Has(string name) => File.Exists(Path.Combine(folder, name));
        IEnumerator Barrier(string key) { Write(role + "-" + key); yield return Until(() => Has((role == "host" ? "client1" : "host") + "-" + key), key); }
        void Check(bool valid, string label)
        {
            if (!valid) throw new Exception(label); checks.Add(label);
            Write("progress-" + role + ".json", JsonUtility.ToJson(new Result { role = role, checks = checks.ToArray() }, true));
        }
        void Complete(bool ok, string error)
        {
            if (finished) return; finished = true;
            Write("result-" + role + ".json", JsonUtility.ToJson(new Result { ok = ok, role = role, error = error, checks = checks.ToArray() }, true));
            StartCoroutine(Quit(ok));
        }
        IEnumerator Quit(bool success) { if (service) yield return Wait(service.CancelAndLeaveAsync()); Application.Quit(success ? 0 : 1); }
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        PvPvEClassDefinition ClassFor(PlayerHealth player, int pass)
        {
            var c = Settings.Loadouts.Default(0);
            c.healing = Settings.Loadouts.healings[(Array.IndexOf(Players, player) + pass) % Settings.Loadouts.healings.Length].id;
            return c;
        }
        IEnumerator Run()
        {
            FindFirstObjectByType<GestorEscenariosPartida>().ActivarEscenario(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio);
            service = GetComponent<PvPvEMatchmaking>();
            var config = Instantiate(Settings); config.MapVoteSeconds = 300;
            service.ConfigureForTest(config, new DirectoryAdapter(service, PlayerPrefab, role == "host"));
            if (role != "host") yield return Until(() => Has("host-ready"), "Host listening");
            yield return Wait(service.SearchAsync()); if (role == "host") Write("host-ready");
            yield return Until(() => service.Roster && service.Roster.Members.Count == 2, "Actual PvPvE roster");
            Check(service.Roster.CanStart, "Two actual NGO/UTP peers keep the existing balanced teams");
            if (Network.IsServer)
                foreach (ulong id in Network.ConnectedClientsIds.ToArray()) Instantiate(PlayerPrefab).GetComponent<NetworkObject>().SpawnAsPlayerObject(id);
            yield return Until(() => Local && Players.Length == 2 && Players.All(p => p.SlotsCuras.Count > 0), "Both original players initialized");
            foreach (var p in Players)
            {
                if (Network.IsServer) { Set(p, "regenerationDelay", 100000f); Set(p, "nextRegenerationTime", Time.time + 100000f); }
                if (p.IsOwner) foreach (var b in p.GetComponentsInChildren<MonoBehaviour>(true))
                    if (new[] { "PlayerMovement", "PlayerLook", "CameraRecoil", "PlayerDownedEffects", "PlayerCameraBob" }.Contains(b.GetType().Name)) b.enabled = false;
            }
            keyboard = InputSystem.AddDevice<Keyboard>(); InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            ((InputAction)Field(Local, "accionSlot1")).ApplyBindingOverride(0, "<Keyboard>/q");
            // The isolated fixture has no loading curtain; use its normal HUD reveal hook.
            Local.GetComponentInChildren<PauseController>(true).MostrarHUD();
            yield return new WaitForSecondsRealtime(1);
            Check(FindObjectsByType<NPCWeaponVendor>(FindObjectsSortMode.None).Length == 0, "HUD validation has no merchant to supply healing icons");
            Check(Local.CapacidadCuras == 1 && !Local.CuracionPvPvEActiva, "Original inventory remains one slot until a class supply is applied");
            yield return Barrier("legacy-baseline");
            if (Network.IsServer)
            {
                foreach (var p in Players)
                {
                    var small = Settings.Loadouts.healings[0]; var large = Settings.Loadouts.healings[1];
                    Check(p.GuardarCura(small.id, small.healing), "Legacy merchant grant still works");
                    Check(!p.GuardarCura(large.id, large.healing), "Legacy full slot rejects another type");
                    Check(p.ComprarEspacioExtra(SlotInventarioExtra.Curas) && p.CapacidadCuras == 2, "Legacy extra-space upgrade still works");
                    Check(p.GuardarCura(small.id, small.healing) && !p.GuardarCura(small.id, small.healing), "Legacy capacity limit is unchanged");
                }
                Write("legacy-done");
            }
            yield return Until(() => Has("legacy-done") && Players.All(p => p.CapacidadCuras == 2), "Legacy capacities replicated");
            Check(!Local.AplicarCuracionDeClaseServidor(null), "Invalid class cannot replace inventory");
            if (!Network.IsServer) Check(!Local.AplicarCuracionDeClaseServidor(Settings.Loadouts.Default(0), true), "Client cannot grant or refill class supplies directly");

            for (int pass = 0; pass < 2; pass++)
            {
                if (Network.IsServer)
                {
                    foreach (var p in Players)
                    {
                        var c = ClassFor(p, pass); var entry = Settings.Loadouts.FindHealing(c.healing);
                        Check(p.AplicarCuracionDeClaseServidor(c, true), "Server applies canonical class healing " + pass);
                        Check(!p.GuardarCura(c.healing, 999) && !p.GuardarCura(c.healing, entry.healing), "Legacy purchase RPC cannot manufacture PvPvE refills or healing amounts");
                        Check(!p.ComprarEspacioExtra(SlotInventarioExtra.Curas) && p.CapacidadCuras == entry.carryCount, "Merchant upgrades cannot bypass the class capacity");
                    }
                    Write("supply-" + pass);
                }
                var expected = Settings.Loadouts.FindHealing(ClassFor(Local, pass).healing);
                yield return Until(() => Has("supply-" + pass) && Players.All(p => p.CuracionPvPvEActiva && p.GetSlotCura(0).cantidad == p.CapacidadCuras) && Local.TipoCuracionPvPvE == expected.id, "Class supplies synchronized");
                foreach (var p in Players)
                {
                    var entry = Settings.Loadouts.FindHealing(ClassFor(p, pass).healing); var slot = p.GetSlotCura(0);
                    Check(slot.itemId.ToString() == entry.id && slot.curacion == entry.healing && p.CapacidadCuras == entry.carryCount, "Both players see canonical type, amount and capacity " + pass + "/" + p.OwnerClientId);
                }
                CheckHud(expected.carryCount, expected); CaptureHud("healing-" + pass + "-full.png");
                yield return PressUse(); Check(Local.GetSlotCura(0).cantidad == expected.carryCount, "Full health never spends a healing charge " + pass);
                yield return Barrier("full-" + pass);
                if (Network.IsServer) { foreach (var p in Players) p.CurrentHealth.Value = 1; Write("damage-" + pass); }
                yield return Until(() => Has("damage-" + pass) && Local.CurrentHealth.Value == 1, "Server damage replicated");
                var invalid = typeof(PlayerHealth).GetMethod("UsarSlotCuraServerRpc", Private);
                invalid.Invoke(Local, new object[] { -1 }); invalid.Invoke(Local, new object[] { 999 });
                yield return new WaitForSecondsRealtime(.2f);
                Check(Local.CurrentHealth.Value == 1 && Local.GetSlotCura(0).cantidad == expected.carryCount, "Invalid inventory indices are rejected on the server " + pass);
                for (int used = 1; used <= expected.carryCount; used++)
                {
                    yield return PressUse(); int remaining = expected.carryCount - used;
                    yield return Until(() => (Local.GetSlotCura(0).Vacio ? 0 : Local.GetSlotCura(0).cantidad) == remaining, "Q consumption synchronized");
                    Check(Local.CurrentHealth.Value == Math.Min(Local.MaxHealth, 1 + used * expected.healing), "Q heals the server-approved amount and clamps to max health " + pass + "/" + used);
                    CheckHud(remaining, expected);
                    if (used == 1) CaptureHud("healing-" + pass + "-used-once.png");
                }
                CaptureHud("healing-" + pass + "-empty.png");
                yield return PressUse(); Check(Local.GetSlotCura(0).Vacio && Local.CurrentHealth.Value == Local.MaxHealth, "Empty supply cannot heal or become negative " + pass);
                yield return Barrier("empty-" + pass);
                yield return Until(() => Players.All(p => p.GetSlotCura(0).Vacio), "Remote empty inventory replicated");
                Check(Players.All(p => p.CuracionPvPvEActiva && p.CapacidadCuras > 0), "Selected type and capacity remain synchronized after depletion " + pass);
                if (Network.IsServer)
                {
                    foreach (var p in Players) { Check(p.AplicarCuracionDeClaseServidor(ClassFor(p, pass)), "Idempotent class application accepted"); Check(p.GetSlotCura(0).Vacio, "Repeated application without explicit refill cannot duplicate supplies"); }
                }
                yield return Barrier("idempotent-" + pass);
            }
            if (Network.IsServer)
            {
                foreach (var p in Players) { p.AplicarCuracionDeClaseServidor(ClassFor(p, 1), true); p.State.Value = PlayerHealth.PlayerState.Downed; p.CurrentHealth.Value = 40; }
                Write("downed");
            }
            yield return Until(() => Has("downed") && Local.IsDowned && !Local.GetSlotCura(0).Vacio, "Downed state");
            int before = Local.GetSlotCura(0).cantidad; yield return PressUse();
            Check(Local.GetSlotCura(0).cantidad == before && Local.CurrentHealth.Value == 40, "Downed players cannot consume class healing");
            yield return Barrier("done"); Complete(true, "");
        }
        IEnumerator PressUse()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return new WaitForSecondsRealtime(.18f);
        }
        void CheckHud(int count, PvPvELoadoutCatalog.Utility entry)
        {
            var hud = Local.GetComponentInChildren<HUDController>(true); hud.SendMessage("UpdateInventarioCuras");
            var slot = Field(hud, "hudCura"); var text = (TMP_Text)Field(slot, "cantidad"); var icon = (Image)Field(slot, "icono");
            Check(text.text == count + "/" + entry.carryCount, "Existing HUD shows " + count + "/" + entry.carryCount);
            Check(icon.enabled && icon.sprite == entry.icon && (count > 0 || icon.color.a < .5f), "HUD icon comes from the class catalog and remains dimmed when empty");
        }
        void CaptureHud(string file)
        {
            var canvas = Local.GetComponentInChildren<HUDController>(true).GetComponentInParent<Canvas>();
            var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; float plane = canvas.planeDistance;
            var camera = new GameObject("Validation HUD camera").AddComponent<Camera>(); camera.transform.position = new Vector3(50000, 0, -10); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 5;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f, .05f, .06f);
            var target = new RenderTexture(1280, 720, 24); var active = RenderTexture.active;
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target }); RenderTexture.active = target;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, role + "-" + file), image.EncodeToPNG()); Destroy(image);
            }
            finally { canvas.renderMode = mode; canvas.worldCamera = oldCamera; canvas.planeDistance = plane; RenderTexture.active = active; target.Release(); Destroy(target); Destroy(camera.gameObject); }
        }
        sealed class DirectoryAdapter : IPvPvESessions
        {
            readonly PvPvEMatchmaking service; readonly GameObject prefab; readonly bool host;
            public DirectoryAdapter(PvPvEMatchmaking service, GameObject prefab, bool host) { this.service = service; this.prefab = prefab; this.host = host; }
            public Task InitializeAsync() => Task.CompletedTask;
            public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings)
            {
                var n = service.Network; n.AddNetworkPrefab(prefab); n.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 7797, "0.0.0.0");
                if (!(host ? n.StartHost() : n.StartClient())) throw new Exception("Could not start NGO peer");
                return Task.FromResult<IPvPvESession>(new Lease(n, host));
            }
        }
        sealed class Lease : IPvPvESession
        {
            readonly NetworkManager network; public string Id => "phase10-fixture"; public bool IsHost { get; }
            public int ParticipantCount => network && network.IsServer ? network.ConnectedClients.Count : 1;
            public event Action Closed { add { } remove { } }
            public Lease(NetworkManager network, bool host) { this.network = network; IsHost = host; }
            public Task CloseAdmissionAsync() => Task.CompletedTask;
            public Task LeaveAsync() { if (network && network.IsListening) network.Shutdown(); return Task.CompletedTask; }
        }
    }
}
#endif
