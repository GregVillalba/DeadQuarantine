using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialDifficultyVisibility : MonoBehaviour
{
    [Serializable]
    private struct Difficulty
    {
        public Button button;
        public GameObject panel;
    }

    private static readonly Color VisibleColor = new Color32(39, 51, 32, 255);
    private static readonly Color HiddenColor = new Color32(17, 25, 30, 255);

    [SerializeField] private Difficulty[] difficulties;

    public void MostrarTodas()
    {
        foreach (Difficulty difficulty in difficulties)
            Mostrar(difficulty, true);
    }

    public void Alternar(int indice)
    {
        Difficulty difficulty = difficulties[indice];
        Mostrar(difficulty, !difficulty.panel.activeSelf);
    }

    private static void Mostrar(Difficulty difficulty, bool visible)
    {
        difficulty.panel.SetActive(visible);
        difficulty.button.targetGraphic.color = visible ? VisibleColor : HiddenColor;
    }
}
