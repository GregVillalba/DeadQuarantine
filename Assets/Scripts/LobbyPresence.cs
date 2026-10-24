using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Presencia en la sala de espera: cada jugador publica un "latido" (un contador que sube cada pocos segundos) como
/// propiedad suya en la sesión, y cada uno mira si los demás siguen latiendo. Un jugador que cerró el juego sin salir
/// deja de latir: pasado el tiempo límite se lo da por desconectado (no se muestra, no cuenta para empezar y el
/// anfitrión libera su lugar). No compara relojes entre PCs: solo mide cuánto hace que el valor dejó de cambiar.
/// </summary>
public class LobbyPresence
{
    public const string PulseProperty = "Pulse";

    private const float BeatEverySeconds = 5f;
    private const float TimeoutSeconds = 20f;

    private class Seen
    {
        public string value;
        public float changedAt;
    }

    private readonly Dictionary<string, Seen> seen = new Dictionary<string, Seen>();
    private float nextBeat;
    private int counter;
    private bool beating;

    /// <summary>Al entrar a una sala nueva.</summary>
    public void Reset()
    {
        seen.Clear();
        counter = 0;
        nextBeat = 0f;
    }

    /// <summary>Llamar en cada frame mientras se está en la sala.</summary>
    public void Tick(ISession session)
    {
        if (session == null)
            return;

        float now = Time.unscaledTime;

        // 1) Latido propio.
        if (now >= nextBeat && !beating)
        {
            nextBeat = now + BeatEverySeconds;
            _ = Beat(session);
        }

        // 2) Mirar si los demás siguen latiendo.
        IReadOnlyList<IReadOnlyPlayer> players = session.Players;
        string myId = session.CurrentPlayer.Id;

        for (int i = 0; i < players.Count; i++)
        {
            IReadOnlyPlayer p = players[i];

            if (p.Id == myId)
                continue;

            string value = ReadPulse(p);
            Seen s;

            if (!seen.TryGetValue(p.Id, out s))
            {
                // Recién visto: tiene todo el tiempo límite para empezar a latir.
                seen[p.Id] = new Seen { value = value, changedAt = now };
            }
            else if (s.value != value)
            {
                s.value = value;
                s.changedAt = now;
            }
        }
    }

    /// <summary>true = sigue en la sala. Uno mismo siempre está vivo.</summary>
    public bool IsAlive(IReadOnlyPlayer player, ISession session)
    {
        if (player.Id == session.CurrentPlayer.Id)
            return true;

        Seen s;

        if (!seen.TryGetValue(player.Id, out s))
            return true;

        return Time.unscaledTime - s.changedAt < TimeoutSeconds;
    }

    private async Task Beat(ISession session)
    {
        beating = true;

        try
        {
            counter++;

            session.CurrentPlayer.SetProperty(
                PulseProperty,
                new PlayerProperty(counter.ToString())
            );

            await session.SaveCurrentPlayerDataAsync();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Lobby] No pude publicar mi latido: " + e.Message);
        }
        finally
        {
            beating = false;
        }
    }

    private static string ReadPulse(IReadOnlyPlayer player)
    {
        PlayerProperty prop;

        if (player.Properties != null && player.Properties.TryGetValue(PulseProperty, out prop))
            return prop.Value;

        return "";
    }
}
