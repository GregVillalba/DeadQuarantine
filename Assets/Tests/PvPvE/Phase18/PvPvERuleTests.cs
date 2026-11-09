using NUnit.Framework;
using DeadQuarantine.PvPvE;
using DeadQuarantine.PvPvE.Networking;
public class PvPvERuleTests
{
    static PvPvETeam PvPvETeams(ulong id) => id == 9 ? PvPvETeam.Bravo : id == 8 ? PvPvETeam.None : PvPvETeam.Alpha;
    [Test] public void DamageAndUtilityGiveOnlyOneAssistPerLife()
    {
        var ledger = new ContributionLedger();
        ledger.Record(9, 1, 2, ContributionType.Damage, 25, 10, 10);
        ledger.Record(9, 1, 2, ContributionType.Flash, 0, 11, 3);
        ledger.Record(9, 1, 1, ContributionType.Damage, 100, 11, 10);
        CollectionAssert.AreEquivalent(new ulong[] { 2 }, ledger.Consume(9, 1, 1, PvPvETeam.Bravo, 12, 15, PvPvETeams));
        Assert.IsEmpty(ledger.Consume(9, 1, 1, PvPvETeam.Bravo, 12, 15, PvPvETeams));
        Assert.IsEmpty(ledger.Consume(9, 2, 1, PvPvETeam.Bravo, 12, 15, PvPvETeams));
    }
    [Test] public void UtilityWorksWithoutDamageButExpires()
    {
        var ledger = new ContributionLedger();
        ledger.Record(9, 1, 2, ContributionType.Flash, 0, 10, 2);
        CollectionAssert.Contains(ledger.Consume(9, 1, 1, PvPvETeam.Bravo, 11, 15, PvPvETeams), 2UL);
        ledger.Record(9, 2, 2, ContributionType.Stun, 0, 10, 2);
        Assert.IsEmpty(ledger.Consume(9, 2, 1, PvPvETeam.Bravo, 13, 15, PvPvETeams));
    }
    [Test] public void InsignificantExpiredAndEnemyContributionsDoNotAssist()
    {
        var ledger = new ContributionLedger();
        ledger.Record(9, 1, 2, ContributionType.Damage, 1, 10, 10);
        ledger.Record(9, 1, 3, ContributionType.Damage, 100, 0, 5);
        ledger.Record(9, 1, 8, ContributionType.Mark, 0, 10, 10);
        Assert.IsEmpty(ledger.Consume(9, 1, 1, PvPvETeam.Bravo, 11, 15, PvPvETeams));
    }
    [Test] public void FreshTrivialDamageCannotRenewExpiredDamage() {
        var ledger=new ContributionLedger();ledger.Record(9,1,2,ContributionType.Damage,20,0,1);ledger.Record(9,1,2,ContributionType.Damage,1,2,1);
        Assert.IsEmpty(ledger.Consume(9,1,1,PvPvETeam.Bravo,2.5,15,PvPvETeams));
    }
    [Test] public void DisconnectAndLifeCleanupRemoveContributors() {
        var ledger=new ContributionLedger();ledger.Record(9,1,2,ContributionType.Stun,0,0,10);ledger.ForgetPlayer(2);Assert.IsEmpty(ledger.Consume(9,1,1,PvPvETeam.Bravo,1,15,PvPvETeams));
        ledger.Record(9,1,2,ContributionType.Damage,20,0,10);ledger.Prune(1,(id,life)=>life==2);Assert.AreEqual(0,ledger.EntryCount);
    }
    [Test] public void InvalidContributionAndZeroWindowDoNotAccumulate() {
        var ledger=new ContributionLedger();ledger.Record(9,1,2,ContributionType.Damage,20,0,double.PositiveInfinity);ledger.Record(9,1,2,ContributionType.Flash,0,0,0);ledger.Record(9,1,2,(ContributionType)255,0,0,10);Assert.AreEqual(0,ledger.EntryCount);
    }
}
