using System;
using DeadQuarantine.PvPvE.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public class PvPvEMapVotingRulesTests
{
 [Test] public void UniqueHighestVoteAlwaysWins()=>Assert.That(PvPvEVoteRules.SelectWinner(new[]{1,3,2},n=>0),Is.EqualTo(1));
 [TestCase(0,1)] [TestCase(1,2)] public void TieOnlyChoosesAmongHighestMaps(int random,int expected)=>Assert.That(PvPvEVoteRules.SelectWinner(new[]{0,2,2},n=>random),Is.EqualTo(expected));
 [TestCase(0)] [TestCase(1)] [TestCase(2)] public void NoVotesAllowsEveryConfiguredMap(int index)=>Assert.That(PvPvEVoteRules.SelectWinner(new[]{0,0,0},n=>index),Is.EqualTo(index));
 [Test] public void EmptyCatalogCannotPickAFakeMap()=>Assert.Throws<ArgumentException>(()=>PvPvEVoteRules.SelectWinner(Array.Empty<int>(),n=>0));
 [Test] public void ProductionCatalogReusesExistingMapIDsAndHasRealPreviews()
 {
  var s=AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset");
  Assert.That(s.HasValidMaps,Is.True);Assert.That(s.MapVoteSeconds,Is.EqualTo(15));Assert.That(s.PostVoteSeconds,Is.EqualTo(10));
  Assert.That(s.Maps[0].Id,Is.EqualTo(ConfiguracionPartidaSeleccionada.Escenario.Laboratorio));Assert.That(s.Maps[1].Id,Is.EqualTo(ConfiguracionPartidaSeleccionada.Escenario.Ciudad));Assert.That(s.Maps[2].Id,Is.EqualTo(ConfiguracionPartidaSeleccionada.Escenario.Cabana));
  var copy=UnityEngine.Object.Instantiate(s);try{copy.Maps=new[]{s.Maps[0],s.Maps[0],s.Maps[2]};Assert.That(copy.HasValidMaps,Is.False);}finally{UnityEngine.Object.DestroyImmediate(copy);}
 }
}
