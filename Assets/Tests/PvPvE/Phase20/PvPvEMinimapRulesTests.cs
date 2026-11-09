using NUnit.Framework;
using UnityEngine;
using DeadQuarantine.PvPvE;
public class PvPvEMinimapRulesTests {
 [Test] public void RecoveredProjectionKeepsNorthUpAndAspect(){var size=new Vector2(344,176);var p=PvPvEMinimap.Project(new(10,5),40,size,10,out var edge);Assert.IsFalse(edge);Assert.AreEqual(86,p.x,.01);Assert.AreEqual(43,p.y,.01);}
 [TestCase(100,0,162,0)] [TestCase(-100,0,-162,0)] [TestCase(0,100,0,78)] [TestCase(0,-100,0,-78)] public void EnemyOutsideRangeClampsToDirectionalBorder(float x,float y,float px,float py){var p=PvPvEMinimap.Project(new(x,y),40,new(344,176),10,out var edge);Assert.IsTrue(edge);Assert.AreEqual(px,p.x,.01);Assert.AreEqual(py,p.y,.01);}
 [Test] public void DiagonalArrowPreservesBearing(){var p=PvPvEMinimap.Project(new(100,100),40,new(344,176),10,out var edge);Assert.IsTrue(edge);Assert.AreEqual(p.x,p.y,.01);Assert.AreEqual(78,p.y,.01);}
 [Test] public void TextureCenterMatchesLocalPlayer(){var uv=PvPvEMinimap.MapUV(new(50,100),new(0,0),new(200,200),40,new(344,176));Assert.AreEqual(.25f,uv.center.x,.0001);Assert.AreEqual(.5f,uv.center.y,.0001);Assert.AreEqual(.2f,uv.width,.0001);Assert.AreEqual(.2f*176/344,uv.height,.0001);}
}