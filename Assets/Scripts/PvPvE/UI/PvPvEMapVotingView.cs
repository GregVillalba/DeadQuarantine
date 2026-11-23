using DeadQuarantine.PvPvE.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace DeadQuarantine.PvPvE.UI
{
    public sealed class PvPvEMapVotingView:MonoBehaviour
    {
        [SerializeField] PvPvEMatchmaking matchmaking;
        [SerializeField] TMP_Text headline, hint;
        [SerializeField] Button[] cards;
        [SerializeField] TMP_Text[] counts;
        PvPvEMapVoting voting;
        int lastSecond=-1;
        Button readyButton;
        TMP_Text readyLabel;
        bool? requestedReady;
        void Awake(){
            PvPvEUIFactory.Polish(transform, useMenuFont: true);PvPvEUIFactory.Place(headline.rectTransform,new(.035f,.30f),new(.66f,.38f));headline.fontSize=21;headline.fontSizeMax=21;headline.alignment=TextAlignmentOptions.MidlineLeft;headline.color=PvPvEUIFactory.Accent;
            PvPvEUIFactory.Place(hint.rectTransform,new(.035f,.255f),new(.66f,.30f));hint.alignment=TextAlignmentOptions.MidlineLeft;
            var readyPanel=PvPvEUIFactory.Panel(transform, "ConfirmReady", 0, 0, 1, 1);
            PvPvEUIFactory.Place(readyPanel, new(.035f,.394f), new(.30f,.474f));
            readyButton=readyPanel.gameObject.AddComponent<Button>();
            readyButton.targetGraphic=readyPanel.GetComponent<Image>();
            readyButton.targetGraphic.raycastTarget=true;
            PvPvEUIFactory.StyleButton(readyButton);
            readyLabel=PvPvEUIFactory.Text(readyPanel,"ReadyLabel","LISTO",0,0,1,1,null,21);
            PvPvEUIFactory.Place(readyLabel.rectTransform,Vector2.zero,Vector2.one);
            readyLabel.alignment=TextAlignmentOptions.Center;
            readyButton.onClick.AddListener(ToggleReady);
            for(int i=0;i<cards.Length;i++){var photograph=cards[i].GetComponentInChildren<RawImage>(true);if(photograph){photograph.texture=matchmaking.Settings.Maps[i].Preview;photograph.color=Color.white;}PvPvEUIFactory.Place((RectTransform)cards[i].transform,new(.035f+i*.21f,.025f),new(.235f+i*.21f,.245f));var title=cards[i].transform.Find("Name")?.GetComponent<TMP_Text>();if(title){title.fontSize=18;title.fontSizeMax=18;title.color=Color.white;}}
        }
        void OnEnable(){matchmaking.Changed+=Bind;Bind();}
        void OnDisable(){matchmaking.Changed-=Bind;if(voting)voting.Changed-=Refresh;voting=null;}
        void Bind(){var current=matchmaking.Roster?matchmaking.Roster.GetComponent<PvPvEMapVoting>():null;if(current!=voting){if(voting)voting.Changed-=Refresh;voting=current;if(voting)voting.Changed+=Refresh;}Refresh();}
        void Update(){if(voting && Mathf.CeilToInt((float)voting.Remaining)!=lastSecond)Refresh();}
        void ToggleReady()
        {
            if(!readyButton.interactable || !voting || !matchmaking.Network)return;
            requestedReady=!voting.IsReady(matchmaking.Network.LocalClientId);
            voting.SetReadyServerRpc(requestedReady.Value);
            Refresh();
        }
        public void Vote0()=>Vote(0);
        public void Vote1()=>Vote(1);
        public void Vote2()=>Vote(2);
        public void Vote(int index){if(voting && voting.IsSpawned && matchmaking.State==PvPvEConnectionState.Waiting)voting.CastVoteServerRpc(index);}
        void Refresh()
        {
            bool ready=voting && voting.IsSpawned;
            lastSecond=ready?Mathf.CeilToInt((float)voting.Remaining):0;
            var phase=ready?voting.Phase.Value:PvPvEVotePhase.WaitingForTeams;
            bool open=ready && phase==PvPvEVotePhase.Voting && matchmaking.State==PvPvEConnectionState.Waiting && voting.Remaining>0;
            ulong local=ready && matchmaking.Network?matchmaking.Network.LocalClientId:ulong.MaxValue;
            bool canConfirm=ready && matchmaking.State==PvPvEConnectionState.Waiting &&
                phase!=PvPvEVotePhase.MapSelected && matchmaking.Roster && matchmaking.Roster.Contains(local);
            bool confirmed=canConfirm && voting.IsReady(local);
            if(!canConfirm || requestedReady==confirmed)requestedReady=null;
            readyButton.gameObject.SetActive(canConfirm);
            readyButton.interactable=canConfirm && !requestedReady.HasValue;
            readyLabel.text=confirmed?"CANCELAR LISTO":"LISTO";
            readyLabel.color=confirmed?PvPvEUIFactory.Accent:Color.white;
            int mine=ready?voting.VoteOf(local):-1;
            for(int i=0;i<cards.Length;i++){
                cards[i].interactable=open;
                counts[i].text=(ready?voting.Count(i):0)+" VOTOS"+(mine==i?" · TU VOTO":"");
                cards[i].targetGraphic.color=ready && voting.Winner.Value==i?new Color(.35f,.26f,.10f,.98f):mine==i?new Color(.25f,.19f,.10f,.98f):PvPvEUIFactory.Surface;
            }
            string winner=ready && voting.SelectedMap!=null?voting.SelectedMap.Name:"";
            switch(phase){
                case PvPvEVotePhase.Voting:headline.text="VOTACIÓN TERMINA EN "+lastSecond;hint.text="Podés cambiar tu voto mientras esté abierta.";break;
                case PvPvEVotePhase.VoteResult:headline.text="MAPA GANADOR: "+winner+"\nLA PARTIDA COMIENZA EN "+lastSecond;hint.text="Votación cerrada.";break;
                case PvPvEVotePhase.WaitingForReady:headline.text="MAPA GANADOR: "+winner;hint.text="Esperando confirmación de todos los jugadores.";break;
                case PvPvEVotePhase.MapSelected:headline.text="MAPA CONFIRMADO: "+winner;hint.text="Votación finalizada.";break;
                default:headline.text="ESPERANDO UN JUGADOR POR EQUIPO";hint.text="La votación comienza al completar ambos equipos.";break;
            }
        }
    }
}
