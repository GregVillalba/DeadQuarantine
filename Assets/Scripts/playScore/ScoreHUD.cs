using UnityEngine;
using TMPro;
using Unity.Netcode;

public class ScoreHUD : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI textoPuntaje;

    private PlayerScore playerScore;
    CanvasGroup modePanel;
    DeadQuarantine.PvPvE.Networking.PvPvECombat matchScore;
    void ModeScoreChanged(Unity.Netcode.NetworkListEvent<DeadQuarantine.PvPvE.Networking.PvPvECombatStats> change)=>ActualizarTexto(0,0);

    private void Awake()
    {
        playerScore = GetComponentInParent<PlayerScore>();
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

    void StyleModeScore()
    {
        if (!textoPuntaje) return;
        var background = textoPuntaje.GetComponentInParent<UnityEngine.UI.Image>(true);
        var panel = background ? background.transform : textoPuntaje.transform.parent;
        if (background) { background.sprite=null; background.color=DeadQuarantine.PvPvE.PvPvEUIFactory.PanelColor; }
        foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true)) {
            text.font=DeadQuarantine.PvPvE.PvPvEUIFactory.BodyFont;
            text.color=text==textoPuntaje?DeadQuarantine.PvPvE.PvPvEUIFactory.Accent:Color.white;
        }
        modePanel=panel.GetComponent<CanvasGroup>();if(!modePanel)modePanel=panel.gameObject.AddComponent<CanvasGroup>();
        modePanel.blocksRaycasts=false;modePanel.interactable=false;
    }
    // The player can spawn before the mode's NetworkBehaviour or before its scene move.
    // Bind when both are ready, not only in OnNetworkSpawn, and read the authoritative snapshot.
    bool BindModeScore() {
        var mode=DeadQuarantine.PvPvE.Networking.PvPvECombat.Active;
        bool belongs=IsSpawned && IsOwner && mode && mode.IsSpawned && mode.NetworkManager==NetworkManager;
        if(!belongs || matchScore==mode)return matchScore;
        if(matchScore)matchScore.Stats.OnListChanged-=ModeScoreChanged;
        if(playerScore)playerScore.ScoreNetwork.OnValueChanged-=ActualizarTexto;
        matchScore=mode;StyleModeScore();matchScore.Stats.OnListChanged+=ModeScoreChanged;ActualizarTexto(0,0);return true;
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