using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DeadQuarantine.PvPvE.Networking
{
    /// <summary>Temporarily configures the existing NGO transport, restoring cooperative settings on exit.</summary>
    public sealed class PvPvENetworkScope
    {
        public NetworkManager Network {get;private set;}
        readonly PvPvENetworkSettings settings;
        readonly NetworkConfig previousConfig;
        readonly Action<NetworkManager.ConnectionApprovalRequest,NetworkManager.ConnectionApprovalResponse> previousApproval;
        readonly UnityTransport transport;
        readonly UnityTransport.ConnectionAddressData previousAddress;
        readonly bool ownsManager;
        readonly string localName;
        readonly Dictionary<ulong,string> names=new();
        readonly Dictionary<ulong,int> characters=new();
        readonly Dictionary<GameObject,bool> dormantSceneObjects=new();
        readonly int localCharacter;
        readonly Dictionary<ulong,float> reservations=new();
        public event Action LocalDisconnected;
        public event Action TransportFailed;
        bool stopping;ulong? connectedLocalId;
        bool accepting=true;
        public void StopAccepting()=>accepting=false;
        public void StartAccepting(){if(!stopping)accepting=true;}
        public PvPvENetworkScope(PvPvENetworkSettings settings,string name)
        {
            this.settings=settings;localCharacter=CharacterSelection.SelectedIndex;
            if(!settings.IsValidCharacter(localCharacter))throw new InvalidOperationException("El personaje seleccionado no existe en el catálogo.");
            localName=PvPvEHello.CleanName(name);
            Network=NetworkManager.Singleton;
            if(Network && (Network.IsListening || Network.ShutdownInProgress))throw new InvalidOperationException("Ya hay una conexión en curso. Salí de la sala anterior.");
            if(!Network){var go=new GameObject("PvPvE NetworkManager");UnityEngine.Object.DontDestroyOnLoad(go);go.AddComponent<UnityTransport>();Network=go.AddComponent<NetworkManager>();ownsManager=true;}
            transport=Network.GetComponent<UnityTransport>();
            if(!transport)throw new InvalidOperationException("El NetworkManager requiere el UnityTransport existente.");
            previousAddress=transport.ConnectionData;previousConfig=Network.NetworkConfig;previousApproval=Network.ConnectionApprovalCallback;
            var config=new NetworkConfig{NetworkTransport=transport,ProtocolVersion=(ushort)settings.ProtocolVersion,ConnectionApproval=true,EnableSceneManagement=true,PlayerPrefab=null,TickRate=30,ClientConnectionBufferTimeout=(int)settings.ConnectionSeconds,ConnectionData=PvPvEHello.Encode(settings,localName)};
            config.LoadSceneTimeOut=(int)Math.Ceiling(settings.MapLoadSeconds);
            var registered=new HashSet<GameObject>();
            if(settings.GamePrefabs)foreach(var entry in settings.GamePrefabs.PrefabList)if(entry.Prefab&&registered.Add(entry.Prefab))config.Prefabs.Add(entry);
            if(previousConfig?.Prefabs?.Prefabs!=null)foreach(var entry in previousConfig.Prefabs.Prefabs)if(entry.Prefab&&registered.Add(entry.Prefab))config.Prefabs.Add(entry);
            if(registered.Add(settings.RosterPrefab))config.Prefabs.Add(new NetworkPrefab{Prefab=settings.RosterPrefab});
            if(settings.PlayerPrefab&&registered.Add(settings.PlayerPrefab))config.Prefabs.Add(new NetworkPrefab{Prefab=settings.PlayerPrefab});
            Network.NetworkConfig=config;Network.ConnectionApprovalCallback=Approve;
            Network.OnServerStarted+=ServerStarted;Network.OnClientConnectedCallback+=Connected;Network.OnClientDisconnectCallback+=Disconnected;Network.OnTransportFailure+=TransportFailure;
            // Menu scenery contains legacy gameplay NetworkObjects. Keep them out of NGO's
            // startup scene sweep; retain scene management for the future synchronized maps.
            foreach(var obj in UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include)){
                var go=obj.gameObject;if(go!=Network.gameObject && !dormantSceneObjects.ContainsKey(go))dormantSceneObjects.Add(go,go.activeSelf);
            }
            foreach(var pair in dormantSceneObjects)pair.Key.SetActive(false);
        }
        void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            foreach(var id in reservations.Where(p=>p.Value<Time.unscaledTime||Network.ConnectedClients.ContainsKey(p.Key)).Select(p=>p.Key).ToArray())reservations.Remove(id);
            bool compatible=PvPvEHello.TryRead(request.Payload,settings,out var name,out var character);
            bool room=accepting && Network.ConnectedClients.Count+reservations.Count<settings.Capacity;
            response.Approved=compatible&&room;response.CreatePlayerObject=false;response.Pending=false;
            if(!response.Approved){response.Reason=compatible?"La sala está llena.":"La versión o modalidad de la sala no es compatible.";return;}
            names[request.ClientNetworkId]=name;characters[request.ClientNetworkId]=character;reservations[request.ClientNetworkId]=Time.unscaledTime+settings.ConnectionSeconds;
        }
        void Connected(ulong id){reservations.Remove(id);if(id==Network.LocalClientId)connectedLocalId=id;}
        void Disconnected(ulong id)
        {
            reservations.Remove(id);names.Remove(id);characters.Remove(id);
            if(!stopping && (id==(connectedLocalId??Network.LocalClientId)||!Network.IsServer&&id==NetworkManager.ServerClientId))LocalDisconnected?.Invoke();
        }
        void TransportFailure(){if(!stopping)TransportFailed?.Invoke();}
        void ServerStarted()
        {
            var go=UnityEngine.Object.Instantiate(settings.RosterPrefab);go.GetComponent<PvPvETeamRoster>().Initialize(settings,id=>id==Network.LocalClientId?localName:names.GetValueOrDefault(id,"Superviviente"),id=>id==Network.LocalClientId?localCharacter:characters.GetValueOrDefault(id,0));
            go.GetComponent<NetworkObject>().Spawn(false);UnityEngine.Object.DontDestroyOnLoad(go);
        }
        void RestoreSceneObjects()
        {
            foreach(var pair in dormantSceneObjects)if(pair.Key)pair.Key.SetActive(pair.Value);
            dormantSceneObjects.Clear();
        }
        public async Task StopAndRestoreAsync()
        {
            if(!Network){RestoreSceneObjects();return;}stopping=true;
            // Normally Services has already stopped NGO. This is the bounded fallback for transport faults.
            if(Network.IsListening)Network.Shutdown();
            float end=Time.realtimeSinceStartup+settings.ShutdownSeconds;
            while(Network && (Network.IsListening||Network.ShutdownInProgress)) {if(Time.realtimeSinceStartup>end)throw new TimeoutException("La conexión anterior todavía se está cerrando.");await Task.Delay(50);}
            if(!Network){RestoreSceneObjects();return;}
            Network.OnServerStarted-=ServerStarted;Network.OnClientConnectedCallback-=Connected;Network.OnClientDisconnectCallback-=Disconnected;Network.OnTransportFailure-=TransportFailure;
            Network.ConnectionApprovalCallback=previousApproval;Network.NetworkConfig=previousConfig;
            transport.SetConnectionData(previousAddress.Address,previousAddress.Port,previousAddress.ServerListenAddress);transport.ConnectionData=previousAddress;
            if(ownsManager){UnityEngine.Object.Destroy(Network.gameObject);await Task.Delay(50);}
            Network=null;RestoreSceneObjects();
        }
    }
}
