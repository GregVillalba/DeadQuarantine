using System.Collections.Generic;
using UnityEngine;
using DeadQuarantine.PvPvE.Networking;
public partial class HUDController {
 bool pvpveStyled;
 readonly Dictionary<GameObject,bool> pvpveHidden=new();
 bool PvPvEPresentationActive=>playerHealth&&PvPvEPreparationFlow.OwnsStagingPlayer(playerHealth.NetworkObject);
 void StylePvPvEFrame(TMPro.TMP_Text text){
  if(!text)return;var frame=text.GetComponentInParent<UnityEngine.UI.Image>(true);if(!frame)return;
  frame.sprite=null;frame.color=DeadQuarantine.PvPvE.PvPvEUIFactory.PanelColor;
  DeadQuarantine.PvPvE.PvPvEUIFactory.Polish(frame.transform);
 }
 void HidePvPvE(GameObject go){if(!go)return;if(!pvpveHidden.ContainsKey(go))pvpveHidden.Add(go,go.activeSelf);go.SetActive(false);}
 void RefreshPvPvEPresentation(){
  if(!PvPvEPresentationActive){foreach(var entry in pvpveHidden)if(entry.Key)entry.Key.SetActive(entry.Value);pvpveHidden.Clear();return;}
  if(!pvpveStyled){pvpveStyled=true;StylePvPvEFrame(playerLabelText);StylePvPvEFrame(weaponNameText);}
  foreach(var resource in GetComponentsInChildren<RecursosTiendaUI>(true))if(resource.name=="Recursos_HUD")HidePvPvE(resource.gameObject);
  if(roundsText){var ancestor=roundsText.transform;while(ancestor&&ancestor!=transform&&ancestor.name!="info_ronda")ancestor=ancestor.parent;HidePvPvE(ancestor&&ancestor.name=="info_ronda"?ancestor.gameObject:roundsText.gameObject);}
  if(zombiesText)HidePvPvE(zombiesText.gameObject);HidePvPvE(vidas0);HidePvPvE(vidas1);
 }
}