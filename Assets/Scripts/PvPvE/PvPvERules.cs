using System;
using System.Collections.Generic;
using System.Linq;
using DeadQuarantine.PvPvE.Networking;

namespace DeadQuarantine.PvPvE
{
    // This ledger is owned by the server. A life revision prevents contributions crossing respawns.
    public sealed class ContributionLedger
    {
        readonly Dictionary<(ulong victim, uint life), List<Entry>> entries = new();
        sealed class Entry { public ulong Attacker; public ContributionType Type; public int Damage; public double RecordedAt, Expires; public float Duration, Intensity; }
        public void Record(ulong victim, uint life, ulong attacker, ContributionType type, int damage, double now, double window, float duration = 0, float intensity = 1)
        {
            if (attacker == victim || window <= 0 || double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(window) || double.IsInfinity(window) || !Enum.IsDefined(typeof(ContributionType), type) || !float.IsFinite(duration) || !float.IsFinite(intensity) || intensity <= 0) return;
            var key = (victim, life);
            if (!entries.TryGetValue(key, out var list)) entries[key] = list = new List<Entry>();
            list.RemoveAll(e => e.Expires < now);
            list.Add(new Entry { Attacker = attacker, Type = type, Damage = Math.Max(0, damage), RecordedAt = now, Expires = now + window, Duration = Math.Max(0,duration), Intensity = Math.Clamp(intensity,0,1) });
        }
        public ulong[] Consume(ulong victim, uint life, ulong killer, PvPvETeam victimTeam, double now, int minimumDamage, Func<ulong, PvPvETeam> teamOf)
        {
            var key = (victim, life);
            if (!entries.TryGetValue(key, out var list)) return Array.Empty<ulong>();
            entries.Remove(key);
            PvPvETeam killerTeam = teamOf(killer);
            if ((killerTeam == PvPvETeam.None || victimTeam == PvPvETeam.None || killerTeam == victimTeam)) return Array.Empty<ulong>();
            return list.Where(e => e.Expires >= now && e.Attacker != killer && e.Attacker != victim && teamOf(e.Attacker) == killerTeam)
                .GroupBy(e => e.Attacker)
                .Where(g => g.Any(e => e.Type != ContributionType.Damage) || g.Sum(e => (long)e.Damage) >= Math.Max(1,minimumDamage))
                .Select(g => g.Key).ToArray();
        }
        public void Forget(ulong victim, uint life) => entries.Remove((victim, life));
        public void ForgetPlayer(ulong id) {
            foreach (var key in entries.Keys.ToArray()) { if(key.victim==id)entries.Remove(key);else {entries[key].RemoveAll(e=>e.Attacker==id);if(entries[key].Count==0)entries.Remove(key);} }
        }
        public void Prune(double now, Func<ulong,uint,bool> currentLife) {
            foreach(var key in entries.Keys.ToArray()) {if(!currentLife(key.victim,key.life)){entries.Remove(key);continue;}entries[key].RemoveAll(e=>e.Expires<now);if(entries[key].Count==0)entries.Remove(key);}
        }
        public int EntryCount => entries.Values.Sum(list=>list.Count);
        public void Clear() => entries.Clear();
    }
}
