using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeadQuarantine.PvPvE.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadQuarantine.PvPvE.UI
{
    /// <summary>Presents the existing cinematic lobby and character selector over the current session.</summary>
    public sealed class PvPvELobbyView : MonoBehaviour
    {
        [SerializeField] PvPvEMatchmaking matchmaking;
        [SerializeField] PvPvEModeMenu menu;
        [SerializeField] GameObject panel, characterPopup, stagePrefab;
        [SerializeField] TMP_Text alpha, bravo, status;
        [SerializeField] Button characterButton, leaveButton;
        GameObject stage;
        Camera stageCamera;
        LobbyCinematicPlayerDisplay[] actors;
        Vector3 cameraOrigin;
        float cameraFov;
        readonly Dictionary<Camera,bool> cameras=new();
        readonly Dictionary<Canvas,bool> pausedCanvases=new();
        readonly Dictionary<Canvas,(bool enabled, bool overrideSorting, int sortingOrder)> popupCanvases=new();
        readonly Dictionary<GraphicRaycaster,bool> pausedRaycasters=new();
        Canvas lobbyCanvas;
        RectTransform soloCard;
        TMP_Text soloName;
        Image soloIcon;
        GameObject[] teamPanels;
        Transform uiRoot, originalUIParent, popupParent;
        int popupSibling;
        public Canvas LobbyCanvas=>lobbyCanvas;
        readonly List<PvPvEMember> members=new();
        PvPvETeam provisionalTeam;
        readonly Image[,] rosterIcons=new Image[2,5];readonly TMP_Text[,] rosterNames=new TMP_Text[2,5];
        int pendingCharacter=-1, sentCharacter=-1;
        float sendAt,nextSearchingText;bool lobbyRequested;Button retry;
        public bool SearchLobbyRequested=>lobbyRequested;
        public void BeginSearching(){lobbyRequested=true;Refresh();}
        public void LeaveSearchLobby(){lobbyRequested=false;Hide();}
        public bool IsShowing=>stage && panel.activeInHierarchy;
        public GameObject Stage=>stage;
        public LobbyCinematicPlayerDisplay LocalActor {
            get {
                if((!matchmaking.Network||!matchmaking.Network.IsConnectedClient)&&actors!=null&&actors.Length>0)return actors[0];
                int index=-1;
                foreach(var member in members)
                    if(matchmaking.Network&&member.ClientId==matchmaking.Network.LocalClientId){index=ActorIndex(member);break;}
                return actors!=null&&index>=0&&index<actors.Length?actors[index]:null;
            }
        }
        public GameObject CharacterPopup=>characterPopup;

        void Awake(){
            PvPvEUIFactory.Polish(panel.transform, useMenuFont: true);
            PvPvEUIFactory.Polish(characterPopup.transform, useMenuFont: true);
            var selector=characterPopup.GetComponentInChildren<Canvas>(true);if(selector){var backdrop=selector.GetComponent<Image>();if(!backdrop)backdrop=selector.gameObject.AddComponent<Image>();backdrop.color=PvPvEUIFactory.PanelColor;backdrop.raycastTarget=true;}
            var title=panel.transform.Find("Title")?.GetComponent<TMP_Text>();if(title){title.fontSize=42;title.fontSizeMax=42;}
            var a=panel.transform.Find("AlphaPanel") as RectTransform;var b=panel.transform.Find("BravoPanel") as RectTransform;
            teamPanels=new[]{a?.gameObject,b?.gameObject};
            ConfigureSoloCard();
            if(a)PvPvEUIFactory.Place(a,new(.70f,.50f),new(.98f,.88f));if(b)PvPvEUIFactory.Place(b,new(.70f,.10f),new(.98f,.48f));
            foreach(var frame in new[]{a,b})if(frame){var bg=frame.GetComponent<Image>();if(!bg)bg=frame.gameObject.AddComponent<Image>();bg.color=PvPvEUIFactory.PanelColor;bg.raycastTarget=false;}
            var entry=panel.transform.Find("CreateClassButton") as RectTransform;if(entry)PvPvEUIFactory.Place(entry,new(.035f,.70f),new(.30f,.78f));
            PvPvEUIFactory.Place((RectTransform)characterButton.transform,new(.035f,.60f),new(.30f,.68f));PvPvEUIFactory.Place((RectTransform)leaveButton.transform,new(.035f,.50f),new(.30f,.58f));
            for(int team=0;team<2;team++){var frame=team==0?a:b;var heading=team==0?alpha:bravo;PvPvEUIFactory.Place(heading.rectTransform,new(.04f,.86f),new(.96f,.99f));heading.fontSize=25;
             for(int slot=0;slot<5;slot++){float top=.84f-slot*.164f;var icon=new GameObject("CharacterIcon_"+slot,typeof(RectTransform),typeof(Image)).GetComponent<Image>();icon.transform.SetParent(frame,false);PvPvEUIFactory.Place(icon.rectTransform,new(.035f,top-.13f),new(.18f,top));icon.preserveAspect=true;icon.raycastTarget=false;rosterIcons[team,slot]=icon;
              var text=new GameObject("PlayerName_"+slot,typeof(RectTransform)).AddComponent<TextMeshProUGUI>();text.transform.SetParent(frame,false);PvPvEUIFactory.Place(text.rectTransform,new(.21f,top-.13f),new(.97f,top));text.font=PvPvEUIFactory.HeadingFont;text.fontSize=20;text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=20;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.richText=false;rosterNames[team,slot]=text;}}
        }
        void ConfigureSoloCard()
        {
            var source = gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(card => card.name == "PartyCard" && card.Find("Row_1/Text"));
            if (!source) return;
            soloCard = Instantiate(source, panel.transform, false);
            soloCard.name = "SoloPartyCard";
            soloCard.sizeDelta = new Vector2(source.sizeDelta.x, 106);
            soloCard.Find("Header").GetComponent<TMP_Text>().text = "JUGADORES 1/1";
            var first = soloCard.Find("Row_1");
            first.gameObject.SetActive(true);
            for (int i = 2; i <= 4; i++)
                soloCard.Find("Row_" + i)?.gameObject.SetActive(false);
            soloName = first.Find("Text").GetComponent<TMP_Text>();
            soloName.richText = false;
            foreach (var label in soloCard.GetComponentsInChildren<TMP_Text>(true))
                label.font = PvPvEUIFactory.HeadingFont;
            soloIcon = first.Find("Icon").GetComponent<Image>();
            foreach (var graphic in soloCard.GetComponentsInChildren<Graphic>(true))
                graphic.enabled = true;
        }

        void LateUpdate(){if(IsShowing){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
        void OnEnable(){matchmaking.Changed+=Refresh;CharacterSelection.Changed+=CharacterChanged;Refresh();}
        void OnDisable()
        {
            matchmaking.Changed -= Refresh;
            CharacterSelection.Changed -= CharacterChanged;
            HideAfterDisable();
        }

        async void HideAfterDisable()
        {
            // A parent may still be deactivating its children during OnDisable.
            await System.Threading.Tasks.Task.Yield();
            if (this && !isActiveAndEnabled) Hide();
        }

        void OnDestroy()
        {
            if (stage) Destroy(stage);
            RestoreCameras();
            RestoreUI(false);
        }
        void CharacterChanged(int index){pendingCharacter=index;sendAt=Time.unscaledTime+.12f;if(IsShowing&&(!matchmaking.Network||!matchmaking.Network.IsConnectedClient))Refresh();}
        void Update()
        {
            if(!IsShowing)return;
            if (Time.unscaledTime >= nextSearchingText) RefreshTeamLabels();
            var classes=GetComponent<PvPvEClassEditor>();if(classes&&(classes.IsOpen||classes.ConsumedEscapeThisFrame))return;
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame){if(characterPopup.activeSelf)CloseCharacters();else Leave();}
            var roster=matchmaking.Roster;
            if(pendingCharacter>=0 && pendingCharacter!=sentCharacter && Time.unscaledTime>=sendAt && roster && roster.IsSpawned && !roster.Closing.Value){sentCharacter=pendingCharacter;roster.SetCharacterServerRpc(pendingCharacter);}
        }
        public void OpenCharacters(){if(!IsShowing || matchmaking.State==PvPvEConnectionState.Leaving)return;characterPopup.SetActive(true);EventSystem.current?.SetSelectedGameObject(characterPopup.GetComponentInChildren<Button>().gameObject);}
        public void CloseCharacters(){characterPopup.SetActive(false);if(characterButton.IsActive())EventSystem.current?.SetSelectedGameObject(characterButton.gameObject);}
        public void Leave(){lobbyRequested=false;CloseCharacters();menu.BackToModes();}

        void IsolateUI()
        {
            var source=GetComponentInParent<Canvas>().GetComponent<CanvasScaler>();
            var go=new GameObject("PvPvE Lobby Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            lobbyCanvas=go.GetComponent<Canvas>();lobbyCanvas.renderMode=RenderMode.ScreenSpaceOverlay;lobbyCanvas.sortingOrder=50;
            var scaler=go.GetComponent<CanvasScaler>();
            if(source){scaler.uiScaleMode=source.uiScaleMode;scaler.referenceResolution=source.referenceResolution;scaler.screenMatchMode=source.screenMatchMode;scaler.matchWidthOrHeight=source.matchWidthOrHeight;scaler.referencePixelsPerUnit=source.referencePixelsPerUnit;scaler.scaleFactor=source.scaleFactor;}
            uiRoot=panel.transform.parent;originalUIParent=uiRoot.parent;uiRoot.SetParent(lobbyCanvas.transform,false);
            // The reused selector owns its own root Canvas/CanvasScaler. Keeping it
            // nested under the lobby leaves its authored zero-sized Canvas undriven.
            popupParent=characterPopup.transform.parent;popupSibling=characterPopup.transform.GetSiblingIndex();
            characterPopup.transform.SetParent(null,false);
            foreach(var canvas in characterPopup.GetComponentsInChildren<Canvas>(true))
            {
                popupCanvases[canvas]=(canvas.enabled,canvas.overrideSorting,canvas.sortingOrder);
                canvas.enabled=true;
                canvas.overrideSorting=true;
                canvas.sortingOrder=100;
            }
            foreach(var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include)){
                if(canvas.transform.IsChildOf(lobbyCanvas.transform)||canvas.transform.IsChildOf(characterPopup.transform))continue;
                pausedCanvases[canvas]=canvas.enabled;canvas.enabled=false;
            }
            foreach(var raycaster in FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include)){
                if(raycaster.transform.IsChildOf(lobbyCanvas.transform)||raycaster.transform.IsChildOf(characterPopup.transform))continue;
                pausedRaycasters[raycaster]=raycaster.enabled;raycaster.enabled=false;
            }
        }
        void RestoreUI(bool reparent = true)
        {
            var previousPopupParent = popupParent;
            popupParent = null;
            if (previousPopupParent && characterPopup)
            {
                if (reparent)
                {
                    characterPopup.transform.SetParent(previousPopupParent, false);
                    characterPopup.transform.SetSiblingIndex(popupSibling);
                }
                else Destroy(characterPopup);
            }

            foreach (var pair in popupCanvases)
            {
                if (!pair.Key) continue;
                pair.Key.enabled=pair.Value.enabled;
                pair.Key.overrideSorting=pair.Value.overrideSorting;
                pair.Key.sortingOrder=pair.Value.sortingOrder;
            }
            popupCanvases.Clear();

            var previousCanvas = lobbyCanvas;
            lobbyCanvas = null;
            if (previousCanvas)
            {
                if (reparent && uiRoot && originalUIParent)
                    uiRoot.SetParent(originalUIParent, false);
                Destroy(previousCanvas.gameObject);
            }
            foreach(var pair in pausedCanvases)if(pair.Key)pair.Key.enabled=pair.Value;
            foreach(var pair in pausedRaycasters)if(pair.Key)pair.Key.enabled=pair.Value;
            pausedCanvases.Clear();pausedRaycasters.Clear();
        }
        void Show()
        {
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None)){cameras[camera]=camera.enabled;camera.enabled=false;}
            stage=Instantiate(stagePrefab,new Vector3(10000,0,0),Quaternion.identity);
            stage.name="PvPvE Cinematic Lobby";stage.SetActive(true);
            stageCamera=stage.GetComponentsInChildren<Camera>(true).Single(c=>c.name=="LobbyCinematicCamera");
            int previewLayer=LayerMask.NameToLayer("CharacterPreview");if(previewLayer>=0)stageCamera.cullingMask&=~(1<<previewLayer);
            stageCamera.enabled=true;
            cameraOrigin=stageCamera.transform.localPosition;cameraFov=stageCamera.fieldOfView;
            actors=stage.GetComponents<LobbyCinematicPlayerDisplay>();
            IsolateUI();
            panel.SetActive(true);menu.PvPvEPanel.SetActive(false);
            pendingCharacter=CharacterSelection.SelectedIndex;sentCharacter=-1;sendAt=Time.unscaledTime;
            EventSystem.current?.SetSelectedGameObject(characterButton.gameObject);
        }
        void Hide()
        {
            GetComponent<PvPvEClassEditor>()?.Close();
            if(characterPopup)characterPopup.SetActive(false);if(panel)panel.SetActive(false);
            if(stage){stage.SetActive(false);Destroy(stage);stage=null;}
            actors=null;members.Clear();pendingCharacter=sentCharacter=-1;
            RestoreCameras();
            RestoreUI();
        }

        void RestoreCameras()
        {
            foreach (var pair in cameras)
                if (pair.Key) pair.Key.enabled = pair.Value;
            cameras.Clear();
        }

        void Refresh()
        {
            var flow=matchmaking.Roster?matchmaking.Roster.GetComponent<PvPvEPreparationFlow>():null;
            bool searching=lobbyRequested&&(matchmaking.State is PvPvEConnectionState.Searching or PvPvEConnectionState.Connecting or PvPvEConnectionState.Idle or PvPvEConnectionState.Failed);
            bool waiting=(matchmaking.State==PvPvEConnectionState.Waiting&&(!flow||flow.Stage.Value==PvPvEPrepareStage.Lobby))||searching;
            if(!waiting && !(stage && matchmaking.State==PvPvEConnectionState.Leaving)){
                bool wasShowing=stage;Hide();
                if(wasShowing && (matchmaking.State==PvPvEConnectionState.Failed || matchmaking.State==PvPvEConnectionState.Idle))menu.PvPvEPanel.SetActive(true);
                return;
            }
            if(!stage)Show();
            characterButton.interactable=waiting;leaveButton.interactable=waiting;
            if(!waiting){status.text="SALIENDO DE LA SALA…";return;}
            var roster=matchmaking.Roster;
            if(!retry){retry=Instantiate(leaveButton,leaveButton.transform.parent);retry.name="RetrySearch";PvPvEUIFactory.Place((RectTransform)retry.transform,new(.035f,.80f),new(.30f,.88f));retry.onClick=new Button.ButtonClickedEvent();retry.onClick.AddListener(()=>{if(matchmaking.State is PvPvEConnectionState.Idle or PvPvEConnectionState.Failed){_ = matchmaking.SearchAsync();}else{_ = matchmaking.CancelAndLeaveAsync();}});retry.GetComponentInChildren<TMP_Text>().text="BUSCAR PARTIDA";}
            retry.gameObject.SetActive(waiting);retry.interactable=matchmaking.State!=PvPvEConnectionState.Leaving;retry.GetComponentInChildren<TMP_Text>().text=matchmaking.State is PvPvEConnectionState.Idle or PvPvEConnectionState.Failed?"BUSCAR PARTIDA":"CANCELAR BÚSQUEDA";
            leaveButton.GetComponentInChildren<TMP_Text>().text="SALIR DE LA SALA";
            bool searchingForPlayers = matchmaking.State is PvPvEConnectionState.Searching
                or PvPvEConnectionState.Connecting or PvPvEConnectionState.Waiting;
            foreach (var teamPanel in teamPanels) if (teamPanel) teamPanel.SetActive(searchingForPlayers);
            if (soloCard)
            {
                soloCard.gameObject.SetActive(!searchingForPlayers);
                int index = CharacterSelection.SelectedIndex;
                var catalog = matchmaking.Settings.Characters;
                var variant = catalog.GetVariant(index);
                soloName.text = PvPvEHello.CleanName(PlayerIdentity.GetUsername()) + " - " +
                    catalog.GetCharacterNameForVariant(index).ToUpperInvariant();
                soloIcon.sprite = variant == null ? null : catalog.GetIcon(variant.type, variant.gender);
                soloIcon.enabled = soloIcon.sprite;
            }
            members.Clear();
            if (!searchingForPlayers || !roster || !roster.IsSpawned)
            {
                status.text=matchmaking.State==PvPvEConnectionState.Failed?matchmaking.Error:
                    searchingForPlayers?"BUSCANDO SALA COMPATIBLE"+Dots():"LOBBY PvPvE · PREPARÁ TU CLASE";
                for(int i=0;i<actors.Length;i++)
                {
                    if(i==0)actors[i].SetPose(stage.transform.TransformPoint(new Vector3(1.6f,-1.26f,-65.6f)),180);
                    actors[i].SetPlayer(i==0,CharacterSelection.SelectedIndex);
                }
                stageCamera.transform.localPosition=cameraOrigin;
                stageCamera.fieldOfView=cameraFov;
                provisionalTeam=searchingForPlayers ? PvPvETeam.Alpha : PvPvETeam.None;
                RefreshTeamLabels();
                return;
            }

            foreach(var member in roster.Members)members.Add(member);
            bool localPending = !matchmaking.Network || !matchmaking.Network.IsConnectedClient ||
                !roster.Contains(matchmaking.Network.LocalClientId);
            provisionalTeam = localPending ? PvPvETeamRules.Choose(roster.Count(PvPvETeam.Alpha),
                roster.Count(PvPvETeam.Bravo), matchmaking.Settings.TeamCapacity) : PvPvETeam.None;
            RefreshTeamLabels();
            status.text=localPending ? "BUSCANDO SALA COMPATIBLE"+Dots() :
                $"SALA PvPvE  ·  {members.Count}/{matchmaking.Settings.Capacity} JUGADORES";
            var activeSlots = new bool[actors.Length];
            foreach(var member in members)
            {
                int index=ActorIndex(member);
                if(index<0||index>=actors.Length)continue;
                activeSlots[index]=true;
                bool back=member.LobbySlot>=3;
                int column=back?member.LobbySlot-3:member.LobbySlot;
                float center=member.Team==PvPvETeam.Alpha?-1.6f:1.6f;
                float x=center+(column-(back?.5f:1f))*.9f;
                float z=back?-63.9f:-65.6f;
                if(back)x*=(-63.9f-cameraOrigin.z)/(-65.6f-cameraOrigin.z);
                actors[index].SetPose(stage.transform.TransformPoint(new Vector3(x,-1.26f,z)),180f);
                actors[index].SetPlayer(true,member.CharacterIndex);
            }
            for(int i=0;i<actors.Length;i++)if(!activeSlots[i])actors[i].SetPlayer(false,0);
            stageCamera.transform.localPosition=cameraOrigin;
            stageCamera.fieldOfView=members.Count>6?Mathf.Max(cameraFov,56):cameraFov;
        }
        static int ActorIndex(PvPvEMember member) => member.LobbySlot+(member.Team==PvPvETeam.Bravo?5:0);
        static string Dots()=>new string('.',1+(int)(Time.unscaledTime/.35f)%3);
        void RefreshTeamLabels()
        {
            nextSearchingText=Time.unscaledTime+.35f;
            alpha.text=TeamText(PvPvETeam.Alpha, provisionalTeam == PvPvETeam.Alpha);
            bravo.text=TeamText(PvPvETeam.Bravo, provisionalTeam == PvPvETeam.Bravo);
        }
        string TeamText(PvPvETeam team, bool showLocalWhileConnecting)
        {
            int teamIndex = team == PvPvETeam.Alpha ? 0 : 1;
            var selected = members.Where(m => m.Team == team).OrderBy(m => m.LobbySlot).ToArray();
            var catalog = matchmaking.Settings.Characters;
            var voting=matchmaking.Roster?matchmaking.Roster.GetComponent<PvPvEMapVoting>():null;
            for (int i = 0; i < 5; i++)
            {
                var icon = rosterIcons[teamIndex, i];
                var label = rosterNames[teamIndex, i];
                if (!icon || !label) continue;

                bool provisional = showLocalWhileConnecting && i == selected.Length;
                if (i >= selected.Length && !provisional)
                {
                    icon.gameObject.SetActive(false);
                    label.text = "BUSCAR JUGADOR" + Dots();
                    label.color = PvPvEUIFactory.Muted;
                    continue;
                }

                var member = provisional ? default : selected[i];
                int index = provisional ? CharacterSelection.SelectedIndex : member.CharacterIndex;
                string name = provisional ? PvPvEHello.CleanName(PlayerIdentity.GetUsername()) : member.Name.ToString();
                bool local = provisional || (matchmaking.Network && matchmaking.Network.IsConnectedClient && member.ClientId == matchmaking.Network.LocalClientId);
                var variant = catalog.GetVariant(index);
                icon.sprite = variant != null ? catalog.GetIcon(variant.type, variant.gender) : null;
                icon.gameObject.SetActive(icon.sprite != null);
                label.text = name + " - " + catalog.GetCharacterNameForVariant(index).ToUpperInvariant() +
                    (voting && voting.IsSpawned && !provisional ? (voting.IsReady(member.ClientId) ? " · LISTO" : " · NO LISTO") : "") +
                    (local ? " (VOS)" : "");
                label.color = local ? Color.white : PvPvEUIFactory.Muted;
            }
            return $"{team.ToString().ToUpperInvariant()}  {selected.Length + (showLocalWhileConnecting ? 1 : 0)}/{matchmaking.Settings.TeamCapacity}";
        }
    }
}
