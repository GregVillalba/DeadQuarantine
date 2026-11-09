using UnityEngine;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE.Networking;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadQuarantine.PvPvE.UI
{
    /// <summary>Navigation only. Session creation and matchmaking are separate from this menu.</summary>
    public sealed class PvPvEModeMenu : MonoBehaviour
    {
        [SerializeField] PvPvEMatchmaking matchmaking;
        [SerializeField] GameObject previousPanel;
        [SerializeField] GameObject modeSelectionPanel;
        [SerializeField] GameObject pvpvePanel;
        [SerializeField] Button cooperativeButton;
        [SerializeField] Button pvpveBackButton;
        [SerializeField] Button previousMultiplayerButton;

        public GameObject ModeSelectionPanel => modeSelectionPanel;
        public GameObject PvPvEPanel => pvpvePanel;

        readonly System.Collections.Generic.Dictionary<Graphic,bool> previousLabels=new();
        readonly System.Collections.Generic.Dictionary<Canvas,bool> previousCanvases=new();
        void Awake(){
            PvPvELoadingScreen.ConfigureOriginal(matchmaking.Settings);
            PvPvEUIFactory.Polish(modeSelectionPanel.transform);PvPvEUIFactory.Polish(pvpvePanel.transform);
            DecorateMode(modeSelectionPanel.transform);DecorateMode(pvpvePanel.transform);
        }
        void DecorateMode(Transform page){
            var background=page.GetComponent<Image>()??page.gameObject.AddComponent<Image>();background.sprite=null;background.color=new Color(.015f,.022f,.028f,.70f);background.raycastTarget=true;
            if(page==modeSelectionPanel.transform)return;
            Move(page,"Title",new(.06f,.70f),new(.55f,.84f));Move(page,"Description",new(.06f,.57f),new(.53f,.69f));
            var names=new[]{"Teams","Kills","Time"};for(int i=0;i<3;i++){var card=page.Find(names[i]);if(card){PvPvEUIFactory.Place((RectTransform)card,new(.06f+i*.16f,.40f),new(.21f+i*.16f,.55f));var text=card.Find("Title")?.GetComponent<TMPro.TMP_Text>();if(text){text.fontSize=24;text.fontSizeMax=24;text.color=PvPvEUIFactory.Accent;}}}
            Move(page,"SearchButton",new(.06f,.23f),new(.53f,.32f));Move(page,"Availability",new(.06f,.33f),new(.53f,.38f));Move(page,"SearchStatus",new(.06f,.14f),new(.53f,.22f));
        }
        static void Move(Transform root,string name,Vector2 min,Vector2 max){var t=root.Find(name) as RectTransform;if(t)PvPvEUIFactory.Place(t,min,max);}
        void MenuForeground(bool hide){
            if(hide){var canvas=GetComponentInParent<Canvas>();if(!canvas)return;
                foreach(var graphic in canvas.GetComponentsInChildren<Graphic>(true)){if(graphic.transform.IsChildOf(transform)||graphic is RawImage)continue;if(!previousLabels.ContainsKey(graphic))previousLabels.Add(graphic,graphic.enabled);graphic.enabled=false;}
                foreach(var other in FindObjectsByType<Canvas>(FindObjectsInactive.Include)){if(other==canvas||other.transform.IsChildOf(transform)||!other.isRootCanvas)continue;if(!previousCanvases.ContainsKey(other))previousCanvases.Add(other,other.enabled);other.enabled=false;}
            }else{foreach(var entry in previousLabels)if(entry.Key)entry.Key.enabled=entry.Value;previousLabels.Clear();foreach(var entry in previousCanvases)if(entry.Key)entry.Key.enabled=entry.Value;previousCanvases.Clear();}
        }
        void OnDestroy()=>MenuForeground(false);
        public void OpenModes()
        {
            // Restore the isolated lobby canvases/cameras before selecting the mode menu.
            GetComponent<PvPvELobbyView>()?.LeaveSearchLobby();
            MenuForeground(false);
            MenuForeground(true);previousPanel.SetActive(false);
            pvpvePanel.SetActive(false);
            modeSelectionPanel.SetActive(true);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Select(cooperativeButton);
        }

        public void OpenPvPvE()
        {
            MenuForeground(true);
            modeSelectionPanel.SetActive(false);
            pvpvePanel.SetActive(false);
            GetComponent<PvPvELobbyView>()?.BeginSearching();
        }

        public async void BackToModes() { if (await LeaveIfNeeded()) OpenModes(); }

        async Task<bool> LeaveIfNeeded()
        {
            if (!matchmaking || matchmaking.CanNavigateAway) return true;
            await matchmaking.CancelAndLeaveAsync();
            return matchmaking.CanNavigateAway;
        }

        public async void BackToPrevious()
        {
            if (!await LeaveIfNeeded()) return;
            GetComponent<PvPvELobbyView>()?.LeaveSearchLobby();
            modeSelectionPanel.SetActive(false);
            pvpvePanel.SetActive(false);
            MenuForeground(false);previousPanel.SetActive(true);
            Select(previousMultiplayerButton);
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (pvpvePanel.activeSelf) BackToModes();
            else if (modeSelectionPanel.activeSelf) BackToPrevious();
        }

        static void Select(Button button)
        {
            if (EventSystem.current && button && button.IsActive() && button.IsInteractable())
                EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
