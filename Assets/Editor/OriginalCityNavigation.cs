#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.SceneManagement;
public static partial class OriginalCityWeaponsTools
{
 const string CityPath="Assets/Prefabs/Escenarios/Escenario_2.prefab";
 const string NavPath="Assets/Prefabs/Escenarios/Escenario_2/NavMesh-Escenario_2.asset";
 static bool ObstacleName(string name) => name.StartsWith("SM_Veh_") || name.StartsWith("SM_Env_Tree") || name.StartsWith("SM_Env_Bush") || name.StartsWith("SM_Env_Rock") || name.StartsWith("SM_Generic_Mountains") ||
  (name.StartsWith("SM_Prop_") && !name.Contains("Scaffold") && !name.Contains("Stair") && !name.Contains("Ramp"));
 [Serializable] public sealed class NavReport {public int InitialTriangles,FinalTriangles,ExcludedIslands,ObstacleObjects,BuildingsRestored,DoorObstacles;public float AgentRadius,AgentHeight,AgentClimb;public List<Vector3> Vertices=new();public int[] Indices;public List<string> Routes=new();}
 [MenuItem("Tools/DeadQuarantine/Hornear navegación accesible de ciudad")]
 public static void RebuildCityNavigation() {
  if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  BakeCity();BakeSceneCities();EditorSceneManager.OpenScene("Assets/Scenes/MainSceneSinglePlayer.unity");
 }
 public static void BakeCity() {
  Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var root=PrefabUtility.LoadPrefabContents(CityPath);
  try {BakeCityRoot(root,NavPath,"city-navmesh.json",SaveCityPreservingMissingScripts);}
  finally {PrefabUtility.UnloadPrefabContents(root);}
 }
 public static void BakeSceneCities() {
  Directory.CreateDirectory(Output);Directory.CreateDirectory("Assets/Navigation/City");AssetDatabase.Refresh();
  foreach(string path in new[]{"Assets/Scenes/MainSceneSinglePlayer.unity","Assets/Scenes/MainSceneMultiPlayer.unity"}) {
   var scene=EditorSceneManager.OpenScene(path);
   var cities=UnityEngine.Object.FindObjectsByType<ConfiguracionEscenarioJugable>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.TipoEscenario==ConfiguracionPartidaSeleccionada.Escenario.Ciudad).ToArray();
   foreach(var config in cities) {
    foreach(var other in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include,FindObjectsSortMode.None))other.RemoveData();
    var root=config.transform.parent.gameObject;
    BakeCityRoot(root,"Assets/Navigation/City/NavMesh_"+Path.GetFileNameWithoutExtension(path)+".asset",Path.GetFileNameWithoutExtension(path)+"-navmesh.json",_=> {if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("City scene save failed");});
   }
  }
 }
 static void BakeCityRoot(GameObject root,string navPath,string reportFile,Action<GameObject> save) {
  var report=new NavReport();
   var surface=root.GetComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
   surface.overrideVoxelSize=true;surface.voxelSize=.06f;surface.overrideTileSize=true;surface.tileSize=128;surface.minRegionArea=.5f;
   // Replace the old blanket building exclusions with physical floors, walls and stairs.
   foreach(var modifier in root.GetComponentsInChildren<NavMeshModifier>(true)) {
    if(modifier.name.StartsWith("SM_Bld_") && modifier.overrideArea && modifier.area==1) {modifier.overrideArea=false;modifier.ignoreFromBuild=false;report.BuildingsRestored++;}
   }
   foreach(var t in root.GetComponentsInChildren<Transform>(true))if(ObstacleName(t.name)) {
    var modifier=t.GetComponent<NavMeshModifier>();if(!modifier)modifier=t.gameObject.AddComponent<NavMeshModifier>();
    modifier.enabled=true;modifier.overrideArea=true;modifier.area=1;modifier.ignoreFromBuild=false;modifier.applyToChildren=true;PrefabUtility.RecordPrefabInstancePropertyModifications(modifier);report.ObstacleObjects++;
   }
   // Door leaves are dynamic obstacles; their closed pose must not erase the doorway from the bake.
   foreach(var door in root.GetComponentsInChildren<Door>(true))foreach(var box in door.GetComponentsInChildren<BoxCollider>(true)) {
    if(box.isTrigger || box.size.y<.7f)continue;
    var modifier=box.GetComponent<NavMeshModifier>();if(!modifier)modifier=box.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
    var obstacle=box.GetComponent<NavMeshObstacle>();if(!obstacle)obstacle=box.gameObject.AddComponent<NavMeshObstacle>();
    obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=box.center;obstacle.size=box.size;obstacle.carving=true;obstacle.carveOnlyStationary=false;report.DoorObstacles++;
   }
   RestoreStairRamps(root);
   var previous=root.transform.Find("Navegacion_Ciudad");if(previous)UnityEngine.Object.DestroyImmediate(previous.gameObject);
   var exclusions=new GameObject("Navegacion_Ciudad");exclusions.transform.SetParent(root.transform,false);
   var settings=CitySettings(surface);report.AgentRadius=settings.agentRadius;report.AgentHeight=settings.agentHeight;report.AgentClimb=settings.agentClimb;
   BuildCityData(surface);surface.RemoveData();var initial=surface.navMeshData;
   if(!initial)throw new InvalidOperationException("City navigation bake returned no data");
   var instance=NavMesh.AddNavMeshData(initial,root.transform.position,root.transform.rotation);
   var retained=new List<Vector3>();
   try {
    var mesh=NavMesh.CalculateTriangulation();report.InitialTriangles=mesh.indices.Length/3;
    if(report.InitialTriangles==0)throw new InvalidOperationException("Empty city navigation");
    var config=root.GetComponentInChildren<ConfiguracionEscenarioJugable>(true);
    if(!NavMesh.SamplePosition(config.SpawnJugador.position,out var entrance,2,NavMesh.AllAreas))throw new InvalidOperationException("City player entrance has no navigation");
    var seeds=new List<Vector3>{entrance.position};
    foreach(var group in Components(mesh)) {
     int i=group[0]*3;Vector3 p=(mesh.vertices[mesh.indices[i]]+mesh.vertices[mesh.indices[i+1]]+mesh.vertices[mesh.indices[i+2]])/3;
     bool reachable=seeds.OrderBy(s=>(s-p).sqrMagnitude).Take(4).Any(s=>Reachable(s,p));
     if(reachable) {retained.Add(p);continue;}
     var bounds=new Bounds(p,Vector3.zero);foreach(int triangle in group)for(int k=0;k<3;k++)bounds.Encapsulate(mesh.vertices[mesh.indices[triangle*3+k]]);
     // A narrow vertical exclusion removes the inaccessible surface without cutting lower interiors.
     bounds.Expand(new Vector3(.04f,.18f,.04f));
     var go=new GameObject("Superficie_Sin_Acceso_"+report.ExcludedIslands++);go.transform.SetParent(exclusions.transform,false);go.transform.position=bounds.center;
     var volume=go.AddComponent<NavMeshModifierVolume>();volume.area=1;volume.size=bounds.size;volume.center=Vector3.zero;
    }
   }finally {instance.Remove();}
   BuildCityData(surface);surface.RemoveData();var data=surface.navMeshData;
   var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
   if(existing) {EditorUtility.CopySerialized(data,existing);UnityEngine.Object.DestroyImmediate(data);}else {existing=data;AssetDatabase.CreateAsset(existing,navPath);}
   surface.navMeshData=existing;EditorUtility.SetDirty(surface);PrefabUtility.RecordPrefabInstancePropertyModifications(surface);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssets();
   var finalInstance=NavMesh.AddNavMeshData(existing,root.transform.position,root.transform.rotation);
   try {
    var mesh=NavMesh.CalculateTriangulation();report.FinalTriangles=mesh.indices.Length/3;report.Vertices.AddRange(mesh.vertices.Select(root.transform.InverseTransformPoint));report.Indices=mesh.indices;
    var config=root.GetComponentInChildren<ConfiguracionEscenarioJugable>(true);
    if(!NavMesh.SamplePosition(config.SpawnJugador.position,out var start,2,NavMesh.AllAreas))throw new InvalidOperationException("Saved city entrance has no navigation");
    foreach(var spawn in config.SpawnsZombies.Where(t=>t)) {
     bool sampled=NavMesh.SamplePosition(spawn.position,out var hit,2,NavMesh.AllAreas);
     bool connected=sampled&&Reachable(start.position,hit.position);
     if(!connected) {
      var candidates=Enumerable.Range(1,80).SelectMany(n=>Enumerable.Range(0,24).Select(angle=>spawn.position+Quaternion.Euler(0,angle*15,0)*Vector3.forward*(n*.1f)));
      foreach(var point in candidates)if(NavMesh.SamplePosition(point,out var candidate,.4f,NavMesh.AllAreas)&&Mathf.Abs(candidate.position.y-spawn.position.y)<.6f&&Reachable(start.position,candidate.position)) {
       float moved=Vector3.Distance(spawn.position,candidate.position);spawn.position=candidate.position+Vector3.up*.06f;PrefabUtility.RecordPrefabInstancePropertyModifications(spawn);connected=true;report.Routes.Add("Spawn repaired "+spawn.name+": "+moved.ToString("F2")+" m");break;
      }
     }
     report.Routes.Add("Zombie spawn "+spawn.name+": "+connected);
    }
    foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Stair") || t.name.Contains("Escalera"))) {
     var bounds=BoundsOf(t);if(bounds.size==Vector3.zero)continue;var p=bounds.center;bool sampled=NavMesh.SamplePosition(p,out var hit,3,NavMesh.AllAreas);
     report.Routes.Add("Stair "+PathOf(t)+": "+(sampled&&Reachable(start.position,hit.position)));
    }
    foreach(var p in retained)if(!NavMesh.SamplePosition(p,out _,.3f,NavMesh.AllAreas))report.Routes.Add("Review retained surface "+p);
   }finally {finalInstance.Remove();}
   save(root);AssetDatabase.SaveAssets();
   File.WriteAllText(Path.Combine(Output,reportFile),JsonUtility.ToJson(report));
 }
 public static void PreviewAndAnalyze() {PreviewWeapons();AnalyzeCityRoutes();}
 public static void AnalyzeCityRoutes() {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=PrefabUtility.LoadPrefabContents(CityPath);var b=new StringBuilder();
  var surface=root.GetComponent<NavMeshSurface>();surface.RemoveData();var instance=NavMesh.AddNavMeshData(surface.navMeshData,root.transform.position,root.transform.rotation);
  try {
   Physics.SyncTransforms();var config=root.GetComponentInChildren<ConfiguracionEscenarioJugable>(true);NavMesh.SamplePosition(config.SpawnJugador.position,out var start,2,NavMesh.AllAreas);
   var sources=(List<NavMeshBuildSource>)typeof(NavMeshSurface).GetMethod("CollectSources",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(surface,null);
   foreach(var source in sources.Where(s=>s.component && PathOf(s.component.transform).Contains("Escalera")))b.AppendLine("SOURCE "+PathOf(source.component.transform)+" area="+source.area+" shape="+source.shape);
   foreach(var stair in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Escalera_Exterior_Tramo_"))) {
    var bounds=BoundsOf(stair);b.AppendLine("STAIR "+stair.name+" bounds="+bounds);
    foreach(var p in new[]{stair.position,bounds.center,new Vector3(bounds.center.x,-11.65f,bounds.center.z),new Vector3(bounds.center.x,bounds.max.y-1,bounds.center.z)}) {
     bool found=NavMesh.SamplePosition(p,out var hit,4,NavMesh.AllAreas);var path=new NavMeshPath();if(found)NavMesh.CalculatePath(start.position,hit.position,NavMesh.AllAreas,path);
     b.AppendLine("QUERY "+p+" -> "+found+" "+hit.position+" path="+path.status+" corners="+string.Join(";",path.corners.Select(c=>c.ToString())));
    }
   }
   for(int z=-514;z>=-526;z-=2)for(int x=-494;x<=-478;x+=4) {
    var point=new Vector3(x,-11.6f,z);var physics=root.scene.GetPhysicsScene();bool floor=physics.Raycast(point+Vector3.up*4,Vector3.down,out var floorHit,8,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
    bool nav=NavMesh.SamplePosition(point,out var navHit,.7f,NavMesh.AllAreas);b.AppendLine("GAP "+point+" floor="+(floor?PathOf(floorHit.transform)+":"+floorHit.point:"NONE")+" nav="+nav+":"+navHit.position);
    foreach(var v in root.GetComponentsInChildren<NavMeshModifierVolume>(true)) {var q=v.transform.InverseTransformPoint(point)-v.center;if(Mathf.Abs(q.x)<v.size.x*.5f&&Mathf.Abs(q.y)<v.size.y*.5f&&Mathf.Abs(q.z)<v.size.z*.5f)b.AppendLine(" GAP VOLUME "+PathOf(v.transform));}
   }
   File.WriteAllText(Path.Combine(Output,"city-route-analysis.txt"),b.ToString());
  }finally {instance.Remove();PrefabUtility.UnloadPrefabContents(root);}
 }
 static void RestoreStairRamps(GameObject root) {
  string directory="Assets/Navigation/City";Directory.CreateDirectory(directory);AssetDatabase.Refresh();var log=new StringBuilder();
  foreach(var stair in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Escalera_Exterior_Tramo_"))) {
   var child=stair.Find("Colision_Continua_De_Peldanos");if(!child)continue;
   var target=child.GetComponent<MeshCollider>();if(!target || target.sharedMesh)continue;
   var source=stair.GetComponent<MeshCollider>();if(!source || !source.sharedMesh)continue;
   var vertices=source.sharedMesh.vertices;var triangles=source.sharedMesh.triangles;
   var levels=new Dictionary<int,List<Vector3>>();
   for(int i=0;i<triangles.Length;i+=3) {
    Vector3 a=stair.TransformPoint(vertices[triangles[i]]),b=stair.TransformPoint(vertices[triangles[i+1]]),c=stair.TransformPoint(vertices[triangles[i+2]]);
    var normal=Vector3.Cross(b-a,c-a);if(normal.y<.00001f || Vector3.Dot(normal.normalized,Vector3.up)<.98f)continue;
    int level=Mathf.RoundToInt((a.y+b.y+c.y)/3*100);if(!levels.TryGetValue(level,out var points))levels[level]=points=new List<Vector3>();points.AddRange(new[]{a,b,c});
   }
   var steps=levels.Where(kv=>kv.Value.Count>=6).OrderBy(kv=>kv.Key).Select(kv=>new {Y=kv.Key*.01f,Points=kv.Value,Center=kv.Value.Aggregate(Vector3.zero,(sum,p)=>sum+p)/kv.Value.Count}).ToArray();
   log.AppendLine(stair.name+" levels "+string.Join(",",steps.Select(s=>s.Y+":"+s.Points.Count)));
   int bestStart=0,bestLength=0,start=0;
   for(int i=1;i<=steps.Length;i++) {
    if(i<steps.Length && steps[i].Y-steps[i-1].Y>.04f && steps[i].Y-steps[i-1].Y<.35f)continue;
    if(i-start>bestLength){bestLength=i-start;bestStart=start;}start=i;
   }
   if(bestLength<3)throw new InvalidOperationException("Cannot derive stair ramp from actual treads: "+stair.name);
   var run=steps.Skip(bestStart).Take(bestLength).ToArray();Vector3 forward=run.Last().Center-run[0].Center;forward.y=0;forward.Normalize();var side=Vector3.Cross(Vector3.up,forward);
   var first=run[0];var last=run.Last();float halfWidth=run.Min(step=>(step.Points.Max(p=>Vector3.Dot(p,side))-step.Points.Min(p=>Vector3.Dot(p,side)))*.5f)*.94f;
   float bottom=first.Points.Min(p=>Vector3.Dot(p,forward)),top=last.Points.Max(p=>Vector3.Dot(p,forward));
   Vector3 low=first.Center+forward*(bottom-Vector3.Dot(first.Center,forward));low.y=first.Y+.015f;
   Vector3 high=last.Center+forward*(top-Vector3.Dot(last.Center,forward));high.y=last.Y+.015f;
   var mesh=new Mesh{name=stair.name+"_CollisionRamp"};mesh.vertices=new[]{low-side*halfWidth,low+side*halfWidth,high-side*halfWidth,high+side*halfWidth}.Select(child.InverseTransformPoint).ToArray();mesh.triangles=new[]{0,2,1,1,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();
   string path=directory+"/"+mesh.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);}else{existing=mesh;AssetDatabase.CreateAsset(existing,path);}target.sharedMesh=existing;target.convex=false;
   log.AppendLine(" Restored width="+halfWidth*2+" rise="+(high.y-low.y)+" length="+Vector3.Distance(low,high));
  }File.WriteAllText(Path.Combine(Output,"stair-ramps.txt"),log.ToString());
 }
 static void SaveCityPreservingMissingScripts(GameObject root) {
  // Preserve pre-existing unresolved components verbatim; they are outside this navigation change.
  string original=File.ReadAllText(CityPath);
  var missing=new List<(string document,string component,string owner)>();
  foreach(string doc in System.Text.RegularExpressions.Regex.Split(original,@"(?=--- !u!)")) {
   if(!doc.StartsWith("--- !u!114 "))continue;
   var guid=System.Text.RegularExpressions.Regex.Match(doc,@"m_Script: \{fileID: \d+, guid: (\w+)");
   if(!guid.Success || !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value)))continue;
   string component=System.Text.RegularExpressions.Regex.Match(doc,@"--- !u!114 &(\d+)").Groups[1].Value;
   string owner=System.Text.RegularExpressions.Regex.Match(doc,@"m_GameObject: \{fileID: (\d+)").Groups[1].Value;
   missing.Add((doc,component,owner));
  }
  int count=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
  if(count!=missing.Count)throw new InvalidOperationException("Unresolved nested components require explicit preservation: "+count+" versus "+missing.Count);
  foreach(var t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
  PrefabUtility.SaveAsPrefabAsset(root,CityPath,out bool success);if(!success)throw new InvalidOperationException("City prefab was not saved");
  string saved=File.ReadAllText(CityPath);
  foreach(var item in missing) {
   var ownerPattern=new System.Text.RegularExpressions.Regex(@"--- !u!1 &"+item.owner+@"(?: stripped)?\r?\n[\s\S]*?(?=--- !u!|\z)");
   string ownerDoc=ownerPattern.Match(original).Value;
   if(ownerDoc.Contains(" stripped")) {
    string instance=System.Text.RegularExpressions.Regex.Match(ownerDoc,@"m_PrefabInstance: \{fileID: (\d+)").Groups[1].Value;
    var instancePattern=new System.Text.RegularExpressions.Regex(@"--- !u!1001 &"+instance+@"\r?\n[\s\S]*?(?=--- !u!|\z)");
    string originalInstance=instancePattern.Match(original).Value;
    string entry=System.Text.RegularExpressions.Regex.Match(originalInstance,@"    - targetCorrespondingSourceObject: [^\r\n]+\r?\n      insertIndex: [^\r\n]+\r?\n      addedObject: \{fileID: "+item.component+@"\}\r?\n").Value;
    string newInstance=instancePattern.Match(saved).Value;
    if(entry.Length==0 || newInstance.Length==0)throw new InvalidOperationException("Cannot preserve original nested component "+item.component);
    newInstance=newInstance.Contains("    m_AddedComponents: []") ? newInstance.Replace("    m_AddedComponents: []","    m_AddedComponents:\n"+entry.TrimEnd()) : newInstance.Replace("  m_SourcePrefab:",entry+"  m_SourcePrefab:");
    saved=instancePattern.Replace(saved,_=>newInstance,1);
    if(!ownerPattern.IsMatch(saved))saved+="\n"+ownerDoc;
   }else {
    var pattern=new System.Text.RegularExpressions.Regex(@"(--- !u!1 &"+item.owner+@"\r?\n[\s\S]*?  m_Component:\r?\n)");
    if(!pattern.IsMatch(saved))throw new InvalidOperationException("Original component owner did not survive saving: "+item.owner);
    saved=pattern.Replace(saved,"$1  - component: {fileID: "+item.component+"}\n",1);
   }
   saved+="\n"+item.document;
  }
  File.WriteAllText(CityPath,saved,new System.Text.UTF8Encoding(false));AssetDatabase.ImportAsset(CityPath,ImportAssetOptions.ForceUpdate);
 }
 public static void InspectCityAccess() {
  var root=PrefabUtility.LoadPrefabContents(CityPath);var b=new StringBuilder();
  try {
   var config=root.GetComponentInChildren<ConfiguracionEscenarioJugable>(true);
   var targets=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Escalera_Exterior") || t.name=="Spawn_Zombie_11").ToArray();
   foreach(var t in targets) {
    var bounds=BoundsOf(t);b.AppendLine("TARGET "+PathOf(t)+" p="+t.position+" bounds="+bounds);
    foreach(var c in t.GetComponentsInChildren<Collider>(true))b.AppendLine(" COLLIDER "+PathOf(c.transform)+" enabled="+c.enabled+" trigger="+c.isTrigger+" bounds="+c.bounds+" mesh="+(c is MeshCollider mc ? AssetDatabase.GetAssetPath(mc.sharedMesh)+" convex="+mc.convex : "primitive"));
    foreach(var m in t.GetComponentsInParent<NavMeshModifier>(true))b.AppendLine(" MOD "+PathOf(m.transform)+" area="+m.area+" override="+m.overrideArea+" ignored="+m.ignoreFromBuild);
    foreach(var v in root.GetComponentsInChildren<NavMeshModifierVolume>(true)) {
     var p=v.transform.InverseTransformPoint(bounds.size==Vector3.zero?t.position:bounds.center)-v.center;
     if(Mathf.Abs(p.x)<v.size.x*.5f && Mathf.Abs(p.y)<v.size.y*.5f && Mathf.Abs(p.z)<v.size.z*.5f)b.AppendLine(" EXCLUSION "+PathOf(v.transform)+" center="+v.transform.TransformPoint(v.center)+" size="+v.size);
    }
   }
   File.WriteAllText(Path.Combine(Output,"city-access.txt"),b.ToString());
  }finally {PrefabUtility.UnloadPrefabContents(root);}
 }
 static NavMeshBuildSettings CitySettings(NavMeshSurface surface) {
  var settings=surface.GetBuildSettings();settings.agentClimb=.35f;settings.agentSlope=45;return settings;
 }
 static void BuildCityData(NavMeshSurface surface) {
  var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var sources=(List<NavMeshBuildSource>)typeof(NavMeshSurface).GetMethod("CollectSources",flags).Invoke(surface,null);
  var bounds=(Bounds)typeof(NavMeshSurface).GetMethod("CalculateWorldBounds",flags).Invoke(surface,new object[]{sources});
  var data=NavMeshBuilder.BuildNavMeshData(CitySettings(surface),sources,bounds,surface.transform.position,surface.transform.rotation);
  surface.RemoveData();surface.navMeshData=data;
 }
 static Bounds BoundsOf(Transform root) {var r=root.GetComponentsInChildren<Renderer>(true);if(r.Length==0)return new Bounds();var b=r[0].bounds;foreach(var item in r.Skip(1))b.Encapsulate(item.bounds);return b;}
 static bool Reachable(Vector3 a,Vector3 b) {var path=new NavMeshPath();return NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;}
 static IEnumerable<List<int>> Components(NavMeshTriangulation mesh) {
  int count=mesh.indices.Length/3;var parent=Enumerable.Range(0,count).ToArray();var edges=new Dictionary<string,int>();
  int Find(int x) {while(parent[x]!=x){parent[x]=parent[parent[x]];x=parent[x];}return x;}
  string Key(Vector3 p)=>Mathf.RoundToInt(p.x*50)+","+Mathf.RoundToInt(p.y*50)+","+Mathf.RoundToInt(p.z*50);
  for(int i=0;i<count;i++)for(int j=0;j<3;j++) {string a=Key(mesh.vertices[mesh.indices[i*3+j]]),b=Key(mesh.vertices[mesh.indices[i*3+(j+1)%3]]);string edge=string.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a;if(edges.TryGetValue(edge,out int other))parent[Find(i)]=Find(other);else edges[edge]=i;}
  return Enumerable.Range(0,count).GroupBy(Find).Select(g=>g.ToList()).ToArray();
 }
}
#endif
