#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DeadQuarantine.PvPvE.Networking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
public static partial class Phase20MinimapBuild {
 [Serializable] sealed class MapReport {public string map,path;public int vertices,triangles,resolution;public Vector2 min,max;public float pixelsPerMeter;}
 [Serializable] sealed class Reports {public MapReport[] maps;}
 static void BakeMaps(PvPvENetworkSettings settings){
  const string folder="Assets/PvPvE/Art/Minimaps";Directory.CreateDirectory(folder);AssetDatabase.Refresh();var reports=new List<MapReport>();
  for(int i=0;i<settings.Maps.Length;i++){
   var scene=EditorSceneManager.OpenScene(MapPath,OpenSceneMode.Single);
   var configs=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ConfiguracionEscenarioJugable>(true)).ToArray();
   var selected=configs.Single(c=>c.TipoEscenario==settings.Maps[i].Id);var root=selected.transform.parent?selected.transform.parent:selected.transform;
   foreach(var config in configs){var r=config.transform.parent?config.transform.parent:config.transform;r.gameObject.SetActive(config==selected);}
   var allSurfaces=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NavMeshSurface>(true)).ToArray();
   var original=NavMesh.CalculateTriangulation();foreach(var surface in allSurfaces)surface.RemoveData();NavMesh.RemoveAllNavMeshData();
   foreach(var surface in root.GetComponentsInChildren<NavMeshSurface>(false))if(surface.enabled&&surface.navMeshData)NavMesh.AddNavMeshData(surface.navMeshData,surface.transform.position,surface.transform.rotation);
   var tri=NavMesh.CalculateTriangulation();
   if(tri.vertices.Length==0){
    // Legacy scene navigation has no per-scenario surface. Keep only its active physical floor.
    Physics.SyncTransforms();var indices=new List<int>();
    for(int k=0;k<original.indices.Length;k+=3){var centroid=(original.vertices[original.indices[k]]+original.vertices[original.indices[k+1]]+original.vertices[original.indices[k+2]])/3;
     if(Physics.RaycastAll(centroid+Vector3.up,Vector3.down,4,~0,QueryTriggerInteraction.Ignore).Any(h=>h.collider.transform.IsChildOf(root))){indices.Add(original.indices[k]);indices.Add(original.indices[k+1]);indices.Add(original.indices[k+2]);}
    }
    tri=original;tri.indices=indices.ToArray();
   }
   if(tri.indices.Length==0)throw new Exception("No active navigation for minimap "+settings.Maps[i].Name);
   // Ignore unused vertices retained by the legacy navigation fallback.
   var used=tri.indices.Distinct().Select(index=>tri.vertices[index]).ToArray();
   var bounds=new Bounds(used[0],Vector3.zero);foreach(var v in used)bounds.Encapsulate(v);
   float side=Mathf.Max(bounds.size.x,bounds.size.z)+8;Vector2 center=new(bounds.center.x,bounds.center.z),min=center-Vector2.one*side*.5f,max=center+Vector2.one*side*.5f;
   int n=Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(side*10)),1024,4096);var occupied=new bool[n*n];float scale=(n-1)/side;
   // Recover the former navigation silhouette, rasterized offline without a render camera.
   for(int k=0;k<tri.indices.Length;k+=3){
    Vector2 a=Pixel(tri.vertices[tri.indices[k]],min,scale),b=Pixel(tri.vertices[tri.indices[k+1]],min,scale),c=Pixel(tri.vertices[tri.indices[k+2]],min,scale);
    float area=Cross(b-a,c-a);if(Mathf.Abs(area)<.001f)continue;
    int x0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)),0,n-1);
    int y0=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y,b.y,c.y)),0,n-1),y1=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y,b.y,c.y)),0,n-1);float sign=Mathf.Sign(area);
    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){var point=new Vector2(x+.5f,y+.5f);if(Cross(b-a,point-a)*sign>=0&&Cross(c-b,point-b)*sign>=0&&Cross(a-c,point-c)*sign>=0)occupied[y*n+x]=true;}
   }
   var pixels=new Color32[n*n];
   for(int y=0;y<n;y++)for(int x=0;x<n;x++){
    int index=y*n+x;bool edge=occupied[index]&&(x==0||y==0||x==n-1||y==n-1||!occupied[index-1]||!occupied[index+1]||!occupied[index-n]||!occupied[index+n]);
    float wx=min.x+x/scale,wz=min.y+y/scale;bool grid=Mathf.Repeat(wx,10)<1/scale||Mathf.Repeat(wz,10)<1/scale;
    pixels[index]=occupied[index]?(edge?new Color32(136,175,177,255):grid?new Color32(52,76,85,255):new Color32(73,99,108,255)):(grid?new Color32(25,37,46,255):new Color32(17,26,34,255));
   }
   var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();string path=folder+"/Minimap_"+i+".png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
   string previous=Path.Combine(Environment.GetEnvironmentVariable("DQ_PVPVE_PREVIOUS_PROJECT")??"", "Assets/PvPvE/Art/Previews/Minimap_"+i+".png.meta");
   if(!File.Exists(path+".meta")&&File.Exists(previous)){string guid=File.ReadLines(previous).First(line=>line.StartsWith("guid: ")).Substring(6);string collision=AssetDatabase.GUIDToAssetPath(guid);if(!string.IsNullOrEmpty(collision)&&collision!=path)throw new Exception("Historical minimap GUID collision: "+collision);File.Copy(previous,path+".meta");}
   AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;importer.isReadable=false;importer.SaveAndReimport();
   var map=settings.Maps[i];map.Minimap=AssetDatabase.LoadAssetAtPath<Texture2D>(path);map.MinimapMin=min;map.MinimapMax=max;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
   reports.Add(new MapReport{map=map.Name,path=path,vertices=tri.vertices.Length,triangles=tri.indices.Length/3,resolution=n,min=min,max=max,pixelsPerMeter=n/side});
  }
  Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"Maps.json"),JsonUtility.ToJson(new Reports{maps=reports.ToArray()},true));
 }
 static Vector2 Pixel(Vector3 v,Vector2 min,float scale)=>new((v.x-min.x)*scale,(v.z-min.y)*scale);
 static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
}
#endif
