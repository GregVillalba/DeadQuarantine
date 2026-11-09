using System.Linq;
using DeadQuarantine.PvPvE.Networking;
using NUnit.Framework;
using Unity.Services.Multiplayer;
using UnityEngine;

public class PvPvENetworkRulesTests
{
    PvPvENetworkSettings settings;
    [SetUp] public void Setup(){settings=ScriptableObject.CreateInstance<PvPvENetworkSettings>();settings.Characters=UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/Scripts/Player/CharacterCatalog.asset");}
    [TearDown] public void Cleanup()=>Object.DestroyImmediate(settings);
    [Test] public void TenSequentialAssignmentsStayBalancedAndStopAtCapacity()
    {
        int a=0,b=0;
        for(int i=0;i<10;i++){var team=PvPvETeamRules.Choose(a,b,5);Assert.That(team,Is.Not.EqualTo(PvPvETeam.None));if(team==PvPvETeam.Alpha)a++;else b++;Assert.That(System.Math.Abs(a-b),Is.LessThanOrEqualTo(1));}
        Assert.That(a,Is.EqualTo(5));Assert.That(b,Is.EqualTo(5));Assert.That(PvPvETeamRules.Choose(a,b,5),Is.EqualTo(PvPvETeam.None));
    }
    [TestCase(0,1,PvPvETeam.Alpha)] [TestCase(3,2,PvPvETeam.Bravo)] [TestCase(5,4,PvPvETeam.Bravo)]
    public void AssignmentUsesTheSmallerAvailableTeam(int a,int b,PvPvETeam expected)=>Assert.That(PvPvETeamRules.Choose(a,b,5),Is.EqualTo(expected));
    [Test] public void SessionDiscoverySeparatesModeProtocolAndCapacity()
    {
        var options=PvPvEUnitySessions.Options(settings);var query=PvPvEUnitySessions.Query(settings);
        Assert.That(options.MaxPlayers,Is.EqualTo(10));Assert.That(options.IsPrivate,Is.False);
        Assert.That(options.SessionProperties["dq_mode"].Value,Is.EqualTo(settings.SessionPool));
        Assert.That(options.SessionProperties["dq_protocol"].Value,Is.EqualTo(settings.Compatibility));
        var converter=typeof(SessionOptions).Assembly.GetType("Unity.Services.Multiplayer.LobbyConverter").GetMethod("ToQueryFilters",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        Assert.DoesNotThrow(()=>converter.Invoke(null,new object[]{query.Filters}),"Filters must be accepted by the installed SDK converter");
        Assert.That(query.CreateSession,Is.True);Assert.That(query.Filters.Any(f=>f.Field==FilterField.StringIndex1),Is.True);
        Assert.That(query.Filters.Any(f=>f.Field==FilterField.StringIndex2),Is.True);Assert.That(query.Filters.Any(f=>f.Field==FilterField.AvailableSlots),Is.True);
        Assert.That(NetworkBootstrap.MaxPlayersPerRoom,Is.EqualTo(4),"Cooperative capacity stays unchanged");
    }
    [Test] public void HelloRejectsWrongModeVersionAndMalformedData()
    {
        var encoded=PvPvEHello.Encode(settings,"<b>Álvaro</b>\n");Assert.That(PvPvEHello.TryRead(encoded,settings,out var name),Is.True);Assert.That(name.Contains('<'),Is.False);
        settings.ProtocolVersion++;Assert.That(PvPvEHello.TryRead(encoded,settings,out _),Is.False);
        Assert.That(PvPvEHello.TryRead(new byte[]{255,255},settings,out _),Is.False);Assert.That(PvPvEHello.TryRead(new byte[513],settings,out _),Is.False);
    }
    [Test] public void HelloValidatesEveryOriginalVariantAndRejectsMissingModels()
    {
        for(int i=-1;i<=settings.Characters.VariantCount;i++){
            var payload=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new PvPvEHello{mode=settings.SessionPool,version=settings.Compatibility,name="Prueba",character=i}));
            Assert.That(PvPvEHello.TryRead(payload,settings,out _,out var selected),Is.EqualTo(i>=0&&i<settings.Characters.VariantCount));
            if(i>=0&&i<settings.Characters.VariantCount)Assert.That(selected,Is.EqualTo(i));
        }
        var encoded=PvPvEHello.Encode(settings,"Prueba");settings.Characters=null;
        Assert.That(PvPvEHello.TryRead(encoded,settings,out _),Is.False);
    }
    [Test] public void NamesFitTheNetworkStringEvenWithUnicode()
    {
        var clean=PvPvEHello.CleanName(new string('界',40));Assert.That(System.Text.Encoding.UTF8.GetByteCount(clean),Is.LessThanOrEqualTo(60));
    }
}
