using System;
using System.Linq;
using UnityEngine;
namespace DeadQuarantine.PvPvE
{
 public sealed class PvPvEClassStore {
  const string Key="DQ.PvPvE.Classes.v1";
  [Serializable] sealed class Document {public int version=1;public PvPvEClassDefinition[] classes;}
  readonly PvPvELoadoutCatalog catalog;
  readonly string key;
  PvPvEClassDefinition[] classes;
  public int Count=>classes.Length;
  public event Action Changed;
  public PvPvEClassStore(PvPvELoadoutCatalog catalog,string profile="local"){this.catalog=catalog;key=Key+"."+profile;Load();}
  public PvPvEClassDefinition Get(int index){if(index<0||index>=5)throw new ArgumentOutOfRangeException(nameof(index));return classes[index].Copy();}
  public bool Save(int index,PvPvEClassDefinition draft,out string error){
   error="";if(index<0||index>=5){error="Clase inválida.";return false;}
   if(!catalog.Validate(draft,out error))return false;
   classes[index]=draft.Copy();Persist();Changed?.Invoke();return true;
  }
  public void Reset(int index){classes[index]=catalog.Default(index);Persist();Changed?.Invoke();}
  public void Load(){
   var data=Read(PlayerPrefs.GetString(key,""))??Read(PlayerPrefs.GetString(key+".backup",""));
   classes=Enumerable.Range(0,5).Select(i=>catalog.Repair(data!=null&&i<data.Length?data[i]:null,i)).ToArray();
  }
  static PvPvEClassDefinition[] Read(string json){
   if(string.IsNullOrEmpty(json))return null;
   try{var doc=JsonUtility.FromJson<Document>(json);return doc!=null&&doc.version==1&&doc.classes!=null?doc.classes:null;}catch(ArgumentException){return null;}
  }
  void Persist(){
   string previous=PlayerPrefs.GetString(key,"");if(Read(previous)!=null)PlayerPrefs.SetString(key+".backup",previous);
   PlayerPrefs.SetString(key,JsonUtility.ToJson(new Document{classes=classes.Select(c=>c.Copy()).ToArray()}));PlayerPrefs.Save();
  }
 }
}
