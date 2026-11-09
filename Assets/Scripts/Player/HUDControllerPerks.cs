using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class HUDController
{
    RectTransform perksRow;
    readonly List<Image> perkIcons = new();
    PlayerHealth perkHealthSubscription;
    uint shownPerks = uint.MaxValue;
    bool perkPvPvE; Vector2 perkParallax; Vector3 lastPerkView; PlayerLook perkView;
    void UpdatePerksHud()
    {
        if (!playerHealth || !playerHealth.PerksCatalog) return;
        if (!perkHealthSubscription) { perkHealthSubscription = playerHealth; playerHealth.OnPerksChanged += RefreshPerksHud; }
        if (!perksRow)
        {
            perksRow = new GameObject("Pasivos", typeof(RectTransform)).GetComponent<RectTransform>();
            perksRow.SetParent(transform, false); perksRow.anchorMin = perksRow.anchorMax = new Vector2(.055f, .19f); perksRow.pivot = Vector2.zero;
            perksRow.anchoredPosition = Vector2.zero; perksRow.sizeDelta = new Vector2(550, 96);
            foreach (var perk in playerHealth.PerksCatalog.entries)
            {
                var icon = new GameObject(perk.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(perksRow, false); var rect = icon.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.sizeDelta = new Vector2(96, 96);
                icon.sprite = perk.icon; icon.preserveAspect = true; icon.raycastTarget = false; perkIcons.Add(icon);
            }
        }
        bool mode=PvPvEPresentationActive;
        if(perkPvPvE!=mode){perkPvPvE=mode;shownPerks=uint.MaxValue;}
        for(int i=0;i<perkIcons.Count;i++){perkIcons[i].sprite=mode?playerHealth.PerksCatalog.entries[i].IconForPvPvE:playerHealth.PerksCatalog.entries[i].icon;perkIcons[i].rectTransform.sizeDelta=Vector2.one*(mode?72:96);}
        if(mode){
            if(!perkView){perkView=playerHealth.GetComponentInChildren<PlayerLook>(true);if(perkView)lastPerkView=perkView.transform.eulerAngles;}
            if(perkView){var current=perkView.transform.eulerAngles;var delta=new Vector2(-Mathf.DeltaAngle(lastPerkView.y,current.y),Mathf.DeltaAngle(lastPerkView.x,current.x));lastPerkView=current;perkParallax=Vector2.Lerp(perkParallax,Vector2.ClampMagnitude(delta*1.5f,5),1-Mathf.Exp(-12*Time.unscaledDeltaTime));}
        }else perkParallax=Vector2.zero;
        perksRow.anchoredPosition=perkParallax;
        if (shownPerks == playerHealth.Perks.Value) return;
        shownPerks = playerHealth.Perks.Value; int column = 0;
        for (int i = 0; i < perkIcons.Count; i++)
        {
            bool active = playerHealth.TienePerk(playerHealth.PerksCatalog.entries[i].perk);
            perkIcons[i].gameObject.SetActive(active);
            if (active) perkIcons[i].rectTransform.anchoredPosition = new Vector2(column++ * (mode?84:110), 0);
        }
        perksRow.gameObject.SetActive(column > 0);
    }
    void RefreshPerksHud() { shownPerks = uint.MaxValue; UpdatePerksHud(); }
    void CleanupPerksHud() { if (perkHealthSubscription) perkHealthSubscription.OnPerksChanged -= RefreshPerksHud; }
}
