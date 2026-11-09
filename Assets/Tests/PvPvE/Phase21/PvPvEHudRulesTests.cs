using System.Linq;
using NUnit.Framework;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
public class PvPvEHudRulesTests {
 [Test] public void RecoveredScoreOrderingSeparatesTeamsAndBreaksTies(){var rows=new[]{new PvPvECombatStats{ClientId=8,Team=PvPvETeam.Bravo,Points=900},new PvPvECombatStats{ClientId=3,Team=PvPvETeam.Alpha,Points=100,Kills=1,Assists=2},new PvPvECombatStats{ClientId=2,Team=PvPvETeam.Alpha,Points=100,Kills=1,Assists=2},new PvPvECombatStats{ClientId=4,Team=PvPvETeam.Alpha,Points=100,Kills=2},new PvPvECombatStats{ClientId=5,Team=PvPvETeam.Alpha,Points=150}};CollectionAssert.AreEqual(new ulong[]{5,4,2,3},PvPvEView.Sorted(rows,PvPvETeam.Alpha).Select(p=>p.ClientId));}
 [Test] public void TenPlayerStatsFitFiveRowsPerTeam(){var rows=Enumerable.Range(0,10).Select(i=>new PvPvECombatStats{ClientId=(ulong)i,Team=i%2==0?PvPvETeam.Alpha:PvPvETeam.Bravo,Points=i*100}).ToArray();Assert.AreEqual(5,PvPvEView.Sorted(rows,PvPvETeam.Alpha).Length);Assert.AreEqual(5,PvPvEView.Sorted(rows,PvPvETeam.Bravo).Length);}
 [TestCase(600,"10:00")] [TestCase(59.1,"01:00")] [TestCase(59,"00:59")] [TestCase(0,"00:00")] [TestCase(-1,"00:00")] public void ClockUsesServerRemainingTimeAndClampsZero(double value,string expected){Assert.AreEqual(expected,PvPvEView.Clock(value));}
}