using Unity.Netcode;
using UnityEngine;

/// <summary>Límite inferior invisible del escenario. El servidor resuelve la caída una sola vez.</summary>
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public sealed class MapLowerBoundary : MonoBehaviour
{
    BoxCollider volume;
    float nextCheck;
    void Awake()
    {
        volume = GetComponent<BoxCollider>(); volume.isTrigger = true;
        var body = GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
    }
    bool MapReady()
    {
        if (!volume || !volume.enabled || !isActiveAndEnabled) return false;
        var combat = DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        if (combat && combat.IsSpawned) return combat.IsPlaying;
        var maps = GestorEscenariosPartida.Instance;
        return !maps || !maps.UsaEscenasAditivasMultiplayer ||
            maps.MapaMultiplayerListo && maps.NombreMapaMultiplayerActivo == gameObject.scene.name;
    }
    public bool IsBelowLimit(Vector3 position)
    {
        if (!volume) volume = GetComponent<BoxCollider>();
        Vector3 local = transform.InverseTransformPoint(position) - volume.center;
        Vector3 half = volume.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z && local.y <= half.y;
    }
    void Check(PlayerHealth health)
    {
        if (!health || !health.IsSpawned || !health.IsServer || health.IsDead || health.IsSpectating || !MapReady()) return;
        if (!DeadQuarantine.PvPvE.Networking.PvPvECombat.Active &&
            health.TryGetComponent<MultiplayerPlayerSpawnAssigner>(out var spawn) && spawn.IsSpawned && !spawn.IsAtSpawn.Value) return;
        if (IsBelowLimit(health.transform.position)) health.KillOutsideMapServer();
    }
    void OnTriggerEnter(Collider other) => Check(other.GetComponentInParent<PlayerHealth>());
    void OnTriggerStay(Collider other) => Check(other.GetComponentInParent<PlayerHealth>());
    void FixedUpdate()
    {
        // También cubre atravesar todo el volumen en un frame o por una actualización de red.
        var network = NetworkManager.Singleton;
        if (!network || !network.IsServer || Time.unscaledTime < nextCheck || !MapReady()) return;
        nextCheck = Time.unscaledTime + .1f;
        foreach (var client in network.ConnectedClientsList)
            if (client.PlayerObject) Check(client.PlayerObject.GetComponent<PlayerHealth>());
    }
    void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>(); if (!box) return;
        Gizmos.color = new Color(1, .2f, .12f, .3f); Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
