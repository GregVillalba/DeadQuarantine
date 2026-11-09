using System;
using System.Text;
using DeadQuarantine.PvPvE.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DeadQuarantine.PvPvE.UI
{
    public sealed class PvPvEConnectionPanel : MonoBehaviour
    {
        [SerializeField] PvPvEMatchmaking matchmaking;
        [SerializeField] Button searchButton;
        [SerializeField] TMP_Text buttonLabel,status,availability,alpha,bravo;
        [SerializeField] GameObject rosterPanel;
        [SerializeField] GameObject[] rules;
        void OnEnable(){matchmaking.Changed+=Refresh;Refresh();}
        void OnDisable(){if(matchmaking)matchmaking.Changed-=Refresh;}
        public async void SearchOrLeave()
        {
            try{if(matchmaking.CanNavigateAway)await matchmaking.SearchAsync();else await matchmaking.CancelAndLeaveAsync();}
            catch(Exception e){Debug.LogException(e);}
        }
        void Refresh()
        {
            var state=matchmaking.State;
            searchButton.interactable=state!=PvPvEConnectionState.Leaving;
            bool waiting=state==PvPvEConnectionState.Waiting;
            rosterPanel.SetActive(waiting);var art=searchButton.transform.parent.Find("DQ_MapArt");if(art)art.gameObject.SetActive(!waiting);foreach(var rule in rules)rule.SetActive(!waiting);
            buttonLabel.text=state switch {PvPvEConnectionState.Searching=>"CANCELAR BÚSQUEDA",PvPvEConnectionState.Connecting=>"CANCELAR CONEXIÓN",PvPvEConnectionState.Waiting=>"SALIR DE LA SALA",PvPvEConnectionState.Leaving=>"CERRANDO CONEXIÓN…",_=>matchmaking.CanNavigateAway?"BUSCAR PARTIDA":"REINTENTAR SALIR"};
            status.text=state switch {PvPvEConnectionState.Searching=>"Buscando una sala compatible…",PvPvEConnectionState.Connecting=>"Conectando y sincronizando los equipos…",PvPvEConnectionState.Leaving=>"Cancelando y cerrando la conexión…",PvPvEConnectionState.Waiting=>matchmaking.Roster && matchmaking.Roster.CanStart?"Sala encontrada. Equipos preparados.":"Sala encontrada. Esperando un rival.",PvPvEConnectionState.Failed=>matchmaking.Error,_=>"Encontrá una sala automáticamente o esperá a otros jugadores."};
            var roster=matchmaking.Roster;
            availability.text=waiting&&roster?$"{roster.Members.Count}/{matchmaking.Settings.Capacity} JUGADORES":"RELAY · EQUIPOS AUTOMÁTICOS";
            if(waiting && roster){alpha.text=Rows(roster,PvPvETeam.Alpha);bravo.text=Rows(roster,PvPvETeam.Bravo);}
        }
        static string Rows(PvPvETeamRoster roster,PvPvETeam team)
        {
            var text=new StringBuilder();foreach(var member in roster.Members)if(member.Team==team)text.AppendLine(member.Name.ToString());return text.Length==0?"Esperando jugadores…":text.ToString();
        }
    }
}
