using NUnit.Framework;
using DeadQuarantine.PvPvE;
public class PvPvEZombieRulesTests {
 [TestCase(0,PvPvEZombieKind.Walker)] [TestCase(.799f,PvPvEZombieKind.Walker)] [TestCase(.8f,PvPvEZombieKind.Runner)] [TestCase(.949f,PvPvEZombieKind.Runner)] [TestCase(.95f,PvPvEZombieKind.Dog)] [TestCase(1,PvPvEZombieKind.Dog)] public void RecoveredWeightsKeepEightyFifteenFive(float sample,PvPvEZombieKind expected){Assert.AreEqual(expected,PvPvEZombieDirector.ChooseKind(sample,80,15,5));}
 [Test] public void ZeroAndInvalidWeightsCannotSpawn(){Assert.IsNull(PvPvEZombieDirector.ChooseKind(.5f,0,0,0));Assert.IsNull(PvPvEZombieDirector.ChooseKind(float.NaN,80,15,5));Assert.IsNull(PvPvEZombieDirector.ChooseKind(.5f,-1,float.NaN,float.PositiveInfinity));}
 [Test] public void ConfigurableRunnerOnlyUsesHumanoidVariant(){Assert.AreEqual(PvPvEZombieKind.Runner,PvPvEZombieDirector.ChooseKind(.7f,0,1,0));Assert.AreEqual(PvPvEZombieKind.Dog,PvPvEZombieDirector.ChooseKind(.1f,0,0,1));}
}