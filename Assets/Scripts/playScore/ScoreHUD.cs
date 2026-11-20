using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreHUD : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI textoPuntaje;

    private PlayerScore playerScore;
    CanvasGroup modePanel;
    private const float ScoreTop = 231f;
    DeadQuarantine.PvPvE.Networking.PvPvECombat matchScore;
    void ModeScoreChanged(Unity.Netcode.NetworkListEvent<DeadQuarantine.PvPvE.Networking.PvPvECombatStats> change)=>ActualizarTexto(0,0);

    private void Awake()
    {
        playerScore = GetComponentInParent<PlayerScore>();
        StyleScore();
        TMP_FontAsset hudFont = Resources.Load<TMP_FontAsset>("PvPvE/HeadingFont");
        if (hudFont != null && textoPuntaje != null)
        {
            textoPuntaje.font = hudFont;
            TMP_Text label = textoPuntaje.rectTransform.parent.Find("textoPuntaje")
                ?.GetComponent<TMP_Text>();
            if (label != null) label.font = hudFont;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (BindModeScore()) return;
        if (playerScore != null)
        {
            playerScore.ScoreNetwork.OnValueChanged += ActualizarTexto;
            ActualizarTexto(0, playerScore.ScoreNetwork.Value);
        }
    }

    public override void OnNetworkDespawn()
    {
        if(matchScore)matchScore.Stats.OnListChanged-=ModeScoreChanged;matchScore=null;
        if (playerScore != null)
            playerScore.ScoreNetwork.OnValueChanged -= ActualizarTexto;
    }

    private void StyleScore()
    {
        if (!textoPuntaje) return;
        var row = textoPuntaje.rectTransform.parent as RectTransform;
        var frame = row ? row.parent.GetComponent<UnityEngine.UI.Image>() : null;
        if (!frame || frame.transform.parent is not RectTransform panel || panel.name != "Puntaje") return;

        var source = HUDController.FindAmmoBackground(panel);
        if (source)
        {
            HUDController.PlaceHudPanel(panel, true, 460, 92, ScoreTop);
            UIEffects.EnsureHudSway(panel, transform);
            HUDController.ClearLegacyFrame(panel.GetComponent<UnityEngine.UI.Image>());
            HUDController.ClearLegacyFrame(frame);
            var backdrop = new GameObject("Fondo de estado", typeof(UnityEngine.UI.Image))
                .GetComponent<UnityEngine.UI.Image>();
            backdrop.transform.SetParent(panel, false);
            backdrop.transform.SetAsFirstSibling();
            HUDController.ApplyStatusFrame(backdrop, source, true);
            HUDController.FitHudRect(frame.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            HUDController.ArrangeHudMetric(textoPuntaje, "textoPuntaje", 13, 79, 48);
        }
        modePanel = panel.GetComponent<CanvasGroup>();
        if (!modePanel) modePanel = panel.gameObject.AddComponent<CanvasGroup>();
        modePanel.blocksRaycasts = false;
        modePanel.interactable = false;
    }
    // The player can spawn before the mode's NetworkBehaviour or before its scene move.
    // Bind when both are ready, not only in OnNetworkSpawn, and read the authoritative snapshot.
    bool BindModeScore() {
        var mode=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        bool belongs=IsSpawned && IsOwner && mode && mode.IsSpawned && mode.NetworkManager==NetworkManager;
        if(!belongs || matchScore==mode)return matchScore;
        if(matchScore)matchScore.Stats.OnListChanged-=ModeScoreChanged;
        if(playerScore)playerScore.ScoreNetwork.OnValueChanged-=ActualizarTexto;
        matchScore=mode;matchScore.Stats.OnListChanged+=ModeScoreChanged;ActualizarTexto(0,0);return true;
    }
    void Update() {
        if(!IsOwner||!IsSpawned)return;
        BindModeScore();
        if(matchScore && modePanel)modePanel.alpha=PauseController.LocalPlayerPaused||matchScore.ScoreLocked.Value?0:1;
    }
    public string DisplayedScore=>textoPuntaje?textoPuntaje.text:"";

    private void ActualizarTexto(int anterior, int nuevo)
    {
        if(matchScore){if(textoPuntaje)textoPuntaje.text=matchScore.TryStats(OwnerClientId,out var stats)?stats.Points.ToString("0.##"):"0";return;}
        if (textoPuntaje != null)
            textoPuntaje.text = nuevo.ToString();
    }
}
