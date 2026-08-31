using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class IntroScreenAnimatorTests
{
    private GameObject root;
    private IntroScreenAnimator animator;
    private CanvasGroup canvasGroup;
 
    private GameObject panelGO;
    private RectTransform panel;
 
    private const float FadeDurationDeTest = 0.15f;
    private const float SlideDistanceDeTest = 40f;
    private const float Tolerancia = 0.05f;
 
    [UnitySetUp]
    public IEnumerator UnitySetUp()
    {
        panelGO = new GameObject("Panel", typeof(RectTransform));
        panel = panelGO.GetComponent<RectTransform>();
        panel.anchoredPosition = Vector2.zero; // posición de reposo determinística
 
        // root arranca inactivo para inyectar 'panel' y las duraciones ANTES de que
        // corra Awake() (que captura panelRestingPosition a partir de panel.anchoredPosition).
        root = new GameObject("IntroScreenAnimatorTestObject");
        root.SetActive(false);
 
        animator = root.AddComponent<IntroScreenAnimator>(); // RequireComponent agrega CanvasGroup ya acá
        canvasGroup = root.GetComponent<CanvasGroup>();
 
        SetField("panel", panel);
        SetField("fadeDuration", FadeDurationDeTest); // acelera los tests
        SetField("panelSlideDistance", SlideDistanceDeTest);
 
        root.SetActive(true); // dispara Awake() con todo ya inyectado
 
        yield return null;
    }
 
    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(panelGO);
    }
 
    private void SetField(string fieldName, object value)
    {
        FieldInfo field = typeof(IntroScreenAnimator).GetField(
            fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
 
        Assert.IsNotNull(field, $"No se encontró el campo privado '{fieldName}' en IntroScreenAnimator.");
        field.SetValue(animator, value);
    }
 
    private object GetField(string fieldName)
    {
        FieldInfo field = typeof(IntroScreenAnimator).GetField(
            fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field.GetValue(animator);
    }
 
    // ---------------- Awake ----------------
 
    [Test]
    public void Awake_ObtieneElCanvasGroupDelMismoGameObject()
    {
        var canvasGroupInterno = GetField("canvasGroup") as CanvasGroup;
        Assert.AreSame(canvasGroup, canvasGroupInterno,
            "Awake() debería cachear el mismo CanvasGroup que RequireComponent agrega al GameObject.");
    }
 
    [Test]
    public void Awake_GuardaLaPosicionDeReposoDelPanel()
    {
        var restingPosition = (Vector2)GetField("panelRestingPosition");
        Assert.AreEqual(Vector2.zero, restingPosition,
            "panelRestingPosition debería capturarse desde panel.anchoredPosition al momento de Awake().");
    }
 
    // ---------------- PlayIn ----------------
 
    [Test]
    public void PlayIn_ActivaElGameObjectDeInmediato()
    {
        root.SetActive(false);
        animator.PlayIn();
 
        Assert.IsTrue(root.activeSelf,
            "PlayIn() debería activar el GameObject de forma síncrona, antes de que corra la corrutina.");
    }
 
    [UnityTest]
    public IEnumerator PlayIn_DuranteLaAnimacion_AlphaQuedaEntreCeroYUno()
    {
        animator.PlayIn();
        yield return new WaitForSecondsRealtime(FadeDurationDeTest / 2f);
 
        Assert.Greater(canvasGroup.alpha, 0f, "A mitad de la animación de entrada, el alpha ya debería haber empezado a subir.");
        Assert.Less(canvasGroup.alpha, 1f, "A mitad de la animación de entrada, el alpha todavía no debería estar en el valor final.");
    }
 
    [UnityTest]
    public IEnumerator PlayIn_AlFinalizar_DejaAlphaEnUno_YPanelEnPosicionDeReposo()
    {
        animator.PlayIn();
        yield return new WaitForSecondsRealtime(FadeDurationDeTest + Tolerancia);
 
        Assert.AreEqual(1f, canvasGroup.alpha, Tolerancia,
            "Al terminar PlayIn(), el CanvasGroup debería quedar totalmente visible (alpha = 1).");
        Assert.AreEqual(Vector2.zero, panel.anchoredPosition,
            "Al terminar PlayIn(), el panel debería quedar en su posición de reposo.");
    }
 
    [UnityTest]
    public IEnumerator PlayIn_AlFinalizar_DejaBlocksRaycastsActivo_YGameObjectActivo()
    {
        animator.PlayIn();
        yield return new WaitForSecondsRealtime(FadeDurationDeTest + Tolerancia);
 
        Assert.IsTrue(canvasGroup.blocksRaycasts,
            "Al terminar de entrar, el panel debería seguir bloqueando raycasts (es interactivo).");
        Assert.IsTrue(root.activeSelf,
            "Al terminar de entrar, el GameObject debe permanecer activo.");
    }
 
    // ---------------- PlayOut ----------------
 
    [UnityTest]
    public IEnumerator PlayOut_AlFinalizar_DejaAlphaEnCero_YDesactivaElGameObject()
    {
        animator.PlayOut(null);
        yield return new WaitForSecondsRealtime(FadeDurationDeTest + Tolerancia);
 
        Assert.AreEqual(0f, canvasGroup.alpha, Tolerancia,
            "Al terminar PlayOut(), el CanvasGroup debería quedar invisible (alpha = 0).");
        Assert.AreEqual(new Vector2(0f, -SlideDistanceDeTest), panel.anchoredPosition,
            "Al terminar PlayOut(), el panel debería deslizarse fuera de su posición de reposo.");
        Assert.IsFalse(canvasGroup.blocksRaycasts,
            "Al terminar de salir, el panel no debería seguir bloqueando raycasts.");
        Assert.IsFalse(root.activeSelf,
            "Al terminar PlayOut(), el GameObject debería desactivarse.");
    }
 
    [UnityTest]
    public IEnumerator PlayOut_InvocaElCallbackUnaSolaVez_AlFinalizar()
    {
        int vecesInvocado = 0;
        animator.PlayOut(() => vecesInvocado++);
 
        yield return new WaitForSecondsRealtime(FadeDurationDeTest + Tolerancia);
 
        Assert.AreEqual(1, vecesInvocado,
            "onComplete debería invocarse exactamente una vez al terminar PlayOut().");
    }
 
    [UnityTest]
    public IEnumerator PlayOut_SinCallback_NoLanzaExcepcion()
    {
        animator.PlayOut(null);
        yield return new WaitForSecondsRealtime(FadeDurationDeTest + Tolerancia);
        // Si onComplete?.Invoke() no manejara bien el null, esto ya habría
        // lanzado una excepción y el test hubiese fallado solo.
        Assert.Pass();
    }
 
    // ---------------- Interrupción de animación (hallazgo documentado) ----------------
 
    [UnityTest]
    public IEnumerator PlayOut_InterrumpeAPlayIn_ElAlphaSaltaEnVezDeContinuarSuave()
    {
        // Arranca PlayIn() y lo corta a mitad de camino con PlayOut(),
        // como podría pasar si el jugador cierra el popup muy rápido.
        animator.PlayIn();
        yield return new WaitForSecondsRealtime(FadeDurationDeTest / 2f);
 
        float alphaAntesDeInterrumpir = canvasGroup.alpha; // valor intermedio, ni 0 ni 1
 
        animator.PlayOut(null);
        yield return null; // primer frame de la corrutina de salida
 
        // FadeRoutine asume "from = 1" para la salida sin leer el alpha real,
        // así que el valor salta a 1 en vez de seguir desde alphaAntesDeInterrumpir.
        Assert.AreNotEqual(alphaAntesDeInterrumpir, canvasGroup.alpha,
            "Este test documenta el comportamiento actual: al interrumpir, el alpha " +
            "'pega un salto' en vez de continuar suavemente desde el valor intermedio. " +
            "Si esto no es lo deseado, hay que leer canvasGroup.alpha real como punto de partida " +
            "en FadeRoutine en vez de asumir 0/1 fijo, y este test debe actualizarse.");
    }
}
