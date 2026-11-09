using UnityEngine;

/// <summary>Holds the authored empty-slide pose on the three non-protected handgun models.</summary>
[DisallowMultipleComponent]
public sealed class WeaponEmptySlideSync:MonoBehaviour
{
 Animator parts;Weapon source;NetworkWeaponPresentation presentation;
 void OnEnable(){parts=GetComponent<Animator>();source=GetComponent<Weapon>();presentation=GetComponentInParent<NetworkWeaponPresentation>();}
 void Update(){
  if(!parts)return;
  bool empty=source?source.CurrentAmmo==0&&!source.IsReloading:presentation&&presentation.BodyAnimator&&presentation.BodyAnimator.GetBool("TP_IsEmpty")&&!presentation.BodyAnimator.GetBool("TP_IsReloading");
  parts.SetBool("Empty",empty);
 }
}
