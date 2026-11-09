using System.Collections;
using System.Linq;
using DeadQuarantine.PvPvE.UI;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class PvPvEModeMenuTests
{
    const string EntryPath="Canvas/menuPrincipalPanel/Botones/PartidaMultijugador";
    PvPvEModeMenu menu;
    Button entry;
    int originalCharacter;

    static T[] SceneComponents<T>() where T:Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static void DisableOnlineInitialization(Scene scene,LoadSceneMode mode)
    {
        // These are UI navigation tests, independent of online service availability.
        foreach(var bootstrap in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkBootstrap>(true)))bootstrap.enabled=false;
        foreach(var rename in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CharacterRenameManager>(true)))rename.enabled=false;
    }
    [UnitySetUp] public IEnumerator LoadMenu()
    {
        originalCharacter=CharacterSelection.SelectedIndex;
        SceneManager.sceneLoaded+=DisableOnlineInitialization;
        yield return SceneManager.LoadSceneAsync("PantallasUI",LoadSceneMode.Single);
        yield return null;
        menu=SceneComponents<PvPvEModeMenu>().Single();
        var canvas=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Canvas" && g.transform.Find("menuPrincipalPanel"));
        entry=canvas.transform.Find(EntryPath.Substring("Canvas/".Length)).GetComponent<Button>();
        Assert.That(entry.gameObject.activeInHierarchy,Is.True,"The main menu exposes Multiplayer directly");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        CharacterSelection.SelectedIndex=originalCharacter;
        SceneManager.sceneLoaded-=DisableOnlineInitialization;
        foreach(var bootstrap in Object.FindObjectsByType<NetworkBootstrap>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.Destroy(bootstrap.gameObject);
        foreach(var network in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include,FindObjectsSortMode.None))Object.Destroy(network.gameObject);
        yield return null;
    }
    [UnityTest] public IEnumerator SoloOptionOpensTheLobbyAndBackRestoresTheMainMenu()
    {
        var mainMenu = entry.transform.parent.parent;
        var solo = mainMenu.Find("Botones/PartidaEnSolitario").GetComponent<Button>();
        solo.onClick.Invoke();
        yield return null;

        Assert.That(mainMenu.gameObject.activeSelf, Is.False);
        Assert.That(GameObject.Find("salaEsperaSinglePlayerPanel"), Is.Not.Null);

        SceneComponents<SinglePlayerLobbyController>().Single().OnClick_Volver();
        yield return null;
        Assert.That(mainMenu.gameObject.activeInHierarchy, Is.True);
        Assert.That(GameObject.Find("salaEsperaSinglePlayerPanel"), Is.Null);
    }


    [UnityTest] public IEnumerator EntryPvPvEAndBothBackButtonsUseRealSceneBindings()
    {
        Assert.That(menu.ModeSelectionPanel.activeSelf,Is.False);
        Assert.That(menu.PvPvEPanel.activeSelf,Is.False);
        entry.onClick.Invoke();yield return null;
        Assert.That(menu.ModeSelectionPanel.activeInHierarchy,Is.True);
        Assert.That(entry.gameObject.activeInHierarchy,Is.False);
        menu.ModeSelectionPanel.transform.Find("PvPvEButton").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.That(menu.PvPvEPanel.activeInHierarchy,Is.True);
        Assert.That(menu.ModeSelectionPanel.activeSelf,Is.False);
        var search=menu.PvPvEPanel.transform.Find("SearchButton").GetComponent<Button>();
        Assert.That(search.interactable,Is.True);
        Assert.That(search.onClick.GetPersistentEventCount(),Is.EqualTo(1));
        Assert.That(search.onClick.GetPersistentTarget(0),Is.InstanceOf<PvPvEConnectionPanel>());
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(menu.PvPvEPanel.transform.Find("BackButton").gameObject));
        Assert.That(NetworkManager.Singleton==null || !NetworkManager.Singleton.IsListening,Is.True,"Menu navigation must not create a network session");
        menu.PvPvEPanel.transform.Find("BackButton").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.That(menu.ModeSelectionPanel.activeInHierarchy,Is.True);
        menu.ModeSelectionPanel.transform.Find("BackButton").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.That(entry.gameObject.activeInHierarchy,Is.True);
        Assert.That(menu.ModeSelectionPanel.activeSelf||menu.PvPvEPanel.activeSelf,Is.False);
        Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(entry.gameObject));
    }
    [UnityTest] public IEnumerator ReopeningDoesNotAccumulateCallbacksOrPanels()
    {
        for(int i=0;i<4;i++) {
            entry.onClick.Invoke();
            menu.ModeSelectionPanel.transform.Find("PvPvEButton").GetComponent<Button>().onClick.Invoke();
            menu.BackToModes();menu.BackToPrevious();yield return null;
        }
        Assert.That(SceneComponents<PvPvEModeMenu>().Length,Is.EqualTo(1));
        Assert.That(entry.onClick.GetPersistentEventCount(),Is.EqualTo(1));
        Assert.That(menu.ModeSelectionPanel.transform.Find("PvPvEButton").GetComponent<Button>().onClick.GetPersistentEventCount(),Is.EqualTo(1));
        Assert.That(entry.gameObject.activeInHierarchy,Is.True);
    }
    [UnityTest] public IEnumerator CooperativeOptionStillLoadsTheExistingNetworkLobby()
    {
        entry.onClick.Invoke();
        var coop=menu.ModeSelectionPanel.transform.Find("CooperativeButton").GetComponent<Button>();
        Assert.That(coop.onClick.GetPersistentTarget(0),Is.InstanceOf<SceneLoader>());
        Assert.That(coop.onClick.GetPersistentMethodName(0),Is.EqualTo("LoadNetworkLobby"));
        coop.onClick.Invoke();yield return null;yield return null;
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("NetworkLobby"));
        Assert.That(SceneComponents<MultiplayerLobbyController>().Length,Is.GreaterThan(0));
        var room = GameObject.Find("salaEsperaPanel");
        Assert.That(room, Is.Not.Null);
        Assert.That(GameObject.Find("MODO_MULTIPLAYER_PANEL"), Is.Null);
        Assert.That(room.transform.Find("AccionesSala/Btn_CrearSala").gameObject.activeInHierarchy, Is.True);
        Assert.That(room.transform.Find("AccionesSala/Btn_UnirseSala").gameObject.activeInHierarchy, Is.True);
        Assert.That(room.transform.Find("CodigoSala").gameObject.activeSelf, Is.False);
        Assert.That(room.transform.Find("Btn_ConfigurarPartida").gameObject.activeSelf, Is.False);
        Assert.That(room.transform.Find("PLAY_MULTIPLAYER").GetComponent<Button>().interactable, Is.False);
    }

    [UnityTest] public IEnumerator UtilityPickersDoNotShowWeaponModelOrStats()
    {
        menu.OpenModes();menu.OpenPvPvE();yield return null;
        var editor=menu.GetComponent<PvPvEClassEditor>();editor.Open();yield return null;
        var root=editor.Panel.transform;
        root.Find("Primary").GetComponent<Button>().onClick.Invoke();yield return null;
        var picker=root.Find("ItemPicker");var model=picker.Find("PickerModelFrame/PickerModel");var stats=picker.Find("SelectionDetails/WeaponStatBars");
        Assert.IsTrue(model.gameObject.activeInHierarchy,"Weapon picker still shows its model");Assert.IsTrue(stats.gameObject.activeInHierarchy);
        foreach(string slot in new[]{"Grenade","Healing"}){
            picker.Find("ClosePicker").GetComponent<Button>().onClick.Invoke();root.Find(slot).GetComponent<Button>().onClick.Invoke();yield return null;
            Assert.IsTrue(picker.gameObject.activeInHierarchy);
            Assert.IsFalse(model.gameObject.activeInHierarchy,slot+" must not show the previously selected weapon");
            Assert.IsFalse(stats.gameObject.activeInHierarchy,slot+" must not show weapon statistics");
            Assert.IsTrue(picker.Find("PickerPerkIcon").GetComponent<Image>().enabled,slot+" shows its own item icon");
        }
        picker.Find("ClosePicker").GetComponent<Button>().onClick.Invoke();root.Find("Primary").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.IsTrue(model.gameObject.activeInHierarchy);Assert.IsTrue(stats.gameObject.activeInHierarchy,"Returning to weapons restores weapon statistics");
        editor.Close();menu.GetComponent<PvPvELobbyView>().Leave();yield return null;
    }
    [UnityTest] public IEnumerator LeavingUnconnectedLobbyRestoresVisibleModeMenu()
    {
        for(int i=0;i<2;i++){
            menu.OpenModes();menu.OpenPvPvE();yield return null;
            var lobby=menu.GetComponent<PvPvELobbyView>();Assert.IsTrue(lobby.IsShowing);
            lobby.Leave();yield return null;yield return null;
            Assert.IsFalse(lobby.IsShowing);Assert.IsNull(lobby.Stage);
            Assert.IsFalse(lobby.SearchLobbyRequested);Assert.IsFalse(menu.PvPvEPanel.activeSelf);Assert.IsTrue(menu.ModeSelectionPanel.activeInHierarchy);
            var canvas=menu.ModeSelectionPanel.GetComponentInParent<Canvas>();Assert.IsTrue(canvas.isActiveAndEnabled,"Restored mode menu canvas must render");
            var button=menu.ModeSelectionPanel.transform.Find("CooperativeButton").GetComponent<Button>();Assert.IsTrue(button.IsActive());Assert.IsTrue(button.image.enabled,"Mode buttons must remain visible");
            Assert.IsTrue(button.GetComponentInChildren<TMPro.TMP_Text>().enabled);Assert.IsTrue(canvas.GetComponent<GraphicRaycaster>().isActiveAndEnabled);
        }
        menu.BackToPrevious();yield return null;
    }

    [UnityTest] public IEnumerator LeavingConnectedLobbyRestoresVisibleModeMenu()
    {
        var service=menu.GetComponent<DeadQuarantine.PvPvE.Networking.PvPvEMatchmaking>();
        var config=Object.Instantiate(service.Settings);service.ConfigureForTest(config,new LocalSessions(service));
        menu.OpenModes();menu.OpenPvPvE();var task=service.SearchAsync();float end=Time.realtimeSinceStartup+20;
        while(!task.IsCompleted){Assert.Less(Time.realtimeSinceStartup,end,service.Error);yield return null;}
        Assert.IsFalse(task.IsFaulted);Assert.AreEqual(DeadQuarantine.PvPvE.Networking.PvPvEConnectionState.Waiting,service.State,service.Error);
        var lobby=menu.GetComponent<PvPvELobbyView>();Assert.IsTrue(lobby.IsShowing);
        lobby.LobbyCanvas.GetComponentsInChildren<Button>(true).Single(b=>b.name=="LeaveButton").onClick.Invoke();
        end=Time.realtimeSinceStartup+20;while(!service.CanNavigateAway){Assert.Less(Time.realtimeSinceStartup,end,service.Error);yield return null;}yield return null;yield return null;
        Assert.IsFalse(lobby.IsShowing);Assert.IsNull(lobby.Stage);Assert.IsFalse(menu.PvPvEPanel.activeSelf);
        Assert.IsTrue(menu.ModeSelectionPanel.activeInHierarchy);Assert.IsTrue(menu.ModeSelectionPanel.GetComponentInParent<Canvas>().isActiveAndEnabled);
        var button=menu.ModeSelectionPanel.transform.Find("CooperativeButton").GetComponent<Button>();Assert.IsTrue(button.IsActive());Assert.IsTrue(button.image.enabled);
        Assert.IsTrue(Cursor.visible);Assert.AreEqual(CursorLockMode.None,Cursor.lockState);Object.Destroy(config);
    }

    sealed class LocalSessions : DeadQuarantine.PvPvE.Networking.IPvPvESessions
    {
        readonly DeadQuarantine.PvPvE.Networking.PvPvEMatchmaking owner;
        public LocalSessions(DeadQuarantine.PvPvE.Networking.PvPvEMatchmaking owner)=>this.owner=owner;
        public System.Threading.Tasks.Task InitializeAsync()=>System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<DeadQuarantine.PvPvE.Networking.IPvPvESession> FindOrCreateAsync(DeadQuarantine.PvPvE.Networking.PvPvENetworkSettings settings)
        { Assert.That(owner.Network.StartHost(),Is.True);return System.Threading.Tasks.Task.FromResult<DeadQuarantine.PvPvE.Networking.IPvPvESession>(new LocalSession(owner.Network)); }
    }
    sealed class LocalSession : DeadQuarantine.PvPvE.Networking.IPvPvESession
    {
        readonly NetworkManager network;public string Id=>"menu-transport-test";public bool IsHost=>true;public int ParticipantCount=>network.ConnectedClients.Count;public System.Threading.Tasks.Task CloseAdmissionAsync()=>System.Threading.Tasks.Task.CompletedTask;public event System.Action Closed;
        public LocalSession(NetworkManager network)=>this.network=network;
        public System.Threading.Tasks.Task LeaveAsync(){network.Shutdown();return System.Threading.Tasks.Task.CompletedTask;}
    }
    [UnityTest] public IEnumerator SearchingInTheRealMenuAndLeavingRestoresCooperativeNetworkConfiguration()
    {
        var service=menu.GetComponent<DeadQuarantine.PvPvE.Networking.PvPvEMatchmaking>();
        var config=Object.Instantiate(service.Settings);service.ConfigureForTest(config,new LocalSessions(service));
        var legacy=new GameObject("Existing cooperative network manager");legacy.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();var network=legacy.AddComponent<NetworkManager>();var originalConfig=network.NetworkConfig;
        entry.onClick.Invoke();menu.ModeSelectionPanel.transform.Find("PvPvEButton").GetComponent<Button>().onClick.Invoke();
        var canvasEnabled=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).ToDictionary(c=>c,c=>c.enabled);
        var originalUI=Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include).ToDictionary(c=>c.gameObject,c=>c.gameObject.activeSelf);
        var legacyObjects=Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include).ToDictionary(o=>o.gameObject,o=>o.gameObject.activeSelf);
        var task=service.SearchAsync();float end=Time.realtimeSinceStartup+10;while(!task.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}
        Assert.That(service.State,Is.EqualTo(DeadQuarantine.PvPvE.Networking.PvPvEConnectionState.Waiting),service.Error);
        Assert.That(service.Roster.Members.Count,Is.EqualTo(1));Assert.That(service.Roster.CanStart,Is.False);
        Assert.That(network.SpawnManager.SpawnedObjects.Count,Is.EqualTo(1),"Only the PvPvE roster is spawned in the menu");
        foreach(var pair in legacyObjects)Assert.That(pair.Key.activeSelf,Is.False);
        Assert.That(network.NetworkConfig.PlayerPrefab,Is.Null,"Menu connection must not create a gameplay Player");
        var lobby=menu.GetComponent<PvPvELobbyView>();
        Assert.That(lobby.IsShowing,Is.True);Assert.That(menu.PvPvEPanel.activeSelf,Is.False);
        var actors=lobby.Stage.GetComponents<LobbyCinematicPlayerDisplay>();
        Assert.That(actors.Length,Is.EqualTo(10));
        int saved=CharacterSelection.SelectedIndex;
        for(int index=0;index<config.Characters.VariantCount;index++) {
            lobby.OpenCharacters();var selector=lobby.CharacterPopup.GetComponentInChildren<CharacterSelectMenu>(true);var variant=config.Characters.GetVariant(index);
            selector.SelectClass(variant.type);selector.SelectGender(variant.gender);lobby.CharacterPopup.GetComponentInChildren<CharacterSelectScreen>(true).Confirm();
            Assert.That(lobby.CharacterPopup.activeSelf,Is.False);
            end=Time.realtimeSinceStartup+10;
            while(!service.Roster.TryGetMember(network.LocalClientId,out var member) || member.CharacterIndex!=index || actors[0].BuiltCharacterIndex!=index || !actors[0].TryGetHeadWorldPosition(out _)){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end),"Variant "+index);yield return null;}
            Assert.That(actors.Count(a=>a.IsVisible),Is.EqualTo(1),"No disconnected placeholder actors");
        }
        CharacterSelection.SelectedIndex=saved;
        lobby.Leave();end=Time.realtimeSinceStartup+10;while(!service.CanNavigateAway){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}yield return null;
        foreach(var pair in legacyObjects)Assert.That(pair.Key.activeSelf,Is.EqualTo(pair.Value),"Original scene object state is restored");
        foreach(var pair in canvasEnabled)if(pair.Key)Assert.That(pair.Key.enabled,Is.EqualTo(pair.Value),"Original canvas enable state is restored");
        foreach(var pair in originalUI)if(pair.Key)Assert.That(pair.Key.activeSelf,Is.EqualTo(pair.Value),"Original menu canvas state is restored");
        Assert.That(!lobby.IsShowing,Is.True);
        Assert.That(menu.ModeSelectionPanel.activeInHierarchy,Is.True);Assert.That(network.IsListening,Is.False);Assert.That(network.NetworkConfig,Is.SameAs(originalConfig));
        Assert.That(menu.ModeSelectionPanel.transform.Find("CooperativeButton").GetComponent<Button>().interactable,Is.True);
        Object.Destroy(legacy);Object.Destroy(config);yield return null;
    }
}
