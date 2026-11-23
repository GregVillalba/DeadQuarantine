using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SurvivalUpgrades))]
public sealed class SurvivalUpgradesEditor : Editor
{
    int testPoints = 1;
    static void Upgrade(string title, SurvivalUpgradeState state, int kind, float multiplier)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.IntField("Puntos invertidos", state.Level(kind));
        EditorGUILayout.FloatField("Mejora acumulada (%)", state.Percent(kind));
        EditorGUILayout.FloatField("Límite de mejora (%)", SurvivalUpgradeState.Step(kind) * SurvivalUpgradeState.Limit(kind));
        EditorGUILayout.FloatField("Multiplicador aplicado", multiplier);
    }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mejoras · comprobación en Play", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Durante Play, seleccioná tu jugador en la Hierarchy para comprobar las mejoras y agregar puntos de prueba.", MessageType.Info);
            return;
        }
        var upgrades = (SurvivalUpgrades)target;
        if (!upgrades.IsSpawned)
        {
            EditorGUILayout.HelpBox("Esperando a que aparezca este jugador en la partida.", MessageType.Info);
            return;
        }
        var state = upgrades.Progress.Value;
        var movement = upgrades.GetComponent<PlayerMovement>();
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("Mejoras activas", upgrades.Active);
            EditorGUILayout.IntField("Puntos disponibles", state.points);
            Upgrade("Vida máxima", state, 0, upgrades.HealthMultiplier);
            var health = upgrades.GetComponent<PlayerHealth>();
            if (health) EditorGUILayout.IntField("Vida máxima con mejora", health.MaxHealth);
            Upgrade("Daño general", state, 1, upgrades.DamageMultiplier);
            var switcher = upgrades.GetComponent<WeaponSwitcher>();
            var weapon = switcher ? switcher.CurrentWeapon : null;
            if (weapon)
            {
                EditorGUILayout.ObjectField("Arma equipada", weapon, typeof(Weapon), true);
                float damage = weapon.EffectiveStatistics.damage;
                EditorGUILayout.FloatField("Daño sin mejora roguelike", damage);
                EditorGUILayout.FloatField("Daño con mejora roguelike", damage * upgrades.DamageMultiplier);
            }
            Upgrade("Regeneración de estamina", state, 2, upgrades.RegenMultiplier);
            if (movement) EditorGUILayout.FloatField("Estamina recuperada / segundo", movement.StaminaRegenerationPerSecond);
            Upgrade("Velocidad de movimiento", state, 3, upgrades.MovementMultiplier);
            if (movement)
            {
                EditorGUILayout.FloatField("Caminar con mejora", movement.MoveSpeed);
                EditorGUILayout.FloatField("Correr con mejora", movement.SprintSpeed);
                EditorGUILayout.FloatField("Agachado con mejora", movement.CrouchSpeed);
            }
        }
        EditorGUILayout.HelpBox("El daño mostrado es el total base del disparo antes de distancia, cabeza y blindaje; en escopetas se reparte entre perdigones. Las velocidades no incluyen obstáculos ni penalizaciones del arma.", MessageType.Info);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Puntos de prueba · solo Editor", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!upgrades.IsServer || !upgrades.Active))
        {
            testPoints = EditorGUILayout.IntSlider("Puntos a agregar", testPoints, 1, 110);
            if (GUILayout.Button("Agregar puntos de prueba")) upgrades.AddEditorTestPoints(testPoints);
            if (GUILayout.Button("Completar puntos para probar todos los máximos")) upgrades.AddEditorTestPoints(110);
        }
        EditorGUILayout.HelpBox("En cooperativo, agregalos desde el Host seleccionando el jugador que quieras probar. Se conservan durante esta partida y respetan los límites de las mejoras. Para gastarlos, abrí las tarjetas con U durante la preparación.", MessageType.Info);
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
