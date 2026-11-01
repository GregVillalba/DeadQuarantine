#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Reconstruye ZombieAnimator.controller usando solamente el subconjunto pedido:
/// - Hit reactions.
/// - Ataques.
/// - Locomoción 8 direcciones Walk/Run (InPlace).
/// - Variantes aleatorias Forward dentro de los Blend Trees direccionales.
/// - Idles.
/// - Sprint con dos variantes.
/// - OutOfGround -> CrawlIdle_To_Stand.
///
/// El controller se modifica IN PLACE, por lo que conserva su GUID y no rompe
/// referencias de prefabs. Antes de tocarlo crea una copia de seguridad.
/// </summary>
public static class ZombieAnimatorUpgradeBuilder
{
    private const string MenuPath = "Tools/Dead Quarantine/Zombies/Instalar Animator mejorado";
    private const float MoveThreshold = 0.05f;

    private static readonly string[] RequiredClipFiles =
    {
        // Idles
        "Zombie_Idle_01.FBX",
        "Zombie_Idle_02.FBX",
        "Zombie_Idle_03.FBX",
        "Zombie_Idle_Search.FBX",

        // Walk 8 direcciones (InPlace)
        "Zombie_Walk_01_Forward_InPlace.fbx",
        "Zombie_Walk_01_ForwardRight_InPlace.fbx",
        "Zombie_Walk_01_Right_InPlace.fbx",
        "Zombie_Walk_01_BackRight_InPlace.fbx",
        "Zombie_Walk_01_Back_InPlace.fbx",
        "Zombie_Walk_01_BackLeft_InPlace.fbx",
        "Zombie_Walk_01_Left_InPlace.fbx",
        "Zombie_Walk_01_ForwardLeft_InPlace.fbx",

        // Run 8 direcciones (InPlace)
        "Zombie_Run_01_Forward_InPlace.fbx",
        "Zombie_Run_01_ForwardRight_InPlace.fbx",
        "Zombie_Run_01_Right_InPlace.fbx",
        "Zombie_Run_01_BackRight_InPlace.fbx",
        "Zombie_Run_01_Back_InPlace.fbx",
        "Zombie_Run_01_BackLeft_InPlace.fbx",
        "Zombie_Run_01_Left_InPlace.fbx",
        "Zombie_Run_01_ForwardLeft_InPlace.fbx",

        // Variantes Forward + sprint (todas InPlace para no pelear con NavMeshAgent)
        "Zombie_Walk_02_Forward_InPlace.fbx",
        "Zombie_Walk_03_Forward_InPlace.fbx",
        "Zombie_Walk_04_Forward_InPlace.fbx",
        "Zombie_Walk_Fast01_Forward_InPlace.fbx",
        "Zombie_Run_02_Forward_InPlace.fbx",
        "Zombie_Sprint_01_Forward_InPlace.fbx",
        "Zombie_Sprint_02_Forward_InPlace.fbx",

        // Attacks
        "Zombie_Attack01.FBX",
        "Zombie_Attack02.FBX",
        "Zombie_Attack03.FBX",
        "Zombie_Attack_180Turn.FBX",
        "Zombie_Attack_90TurnLeft.FBX",
        "Zombie_Attack_90TurnRight.FBX",
        "Zombie_Attack_Forward01.fbx",
        "Zombie_Attack_ForwardJump.fbx",

        // Hits
        "Zombie_HitReact_Head.fbx",
        "Zombie_HitReact_LeftArm.fbx",
        "Zombie_HitReact_RightArm.fbx",

        // Spawn
        "Zombie_Crawl_OutOfGround_To_CrawlIdle.FBX",
        "Zombie_Crawl_CrawlIdle_To_Stand.FBX"
    };

    private sealed class Clips
    {
        public AnimationClip idle1;
        public AnimationClip idle2;
        public AnimationClip idle3;
        public AnimationClip idleSearch;

        public AnimationClip[] walk8;
        public AnimationClip[] run8;

        public AnimationClip[] walkForwardVariants;
        public AnimationClip[] runForwardVariants;
        public AnimationClip[] sprintForwardVariants;

        public AnimationClip[] attacks;
        public AnimationClip[] hits;

        public AnimationClip spawnOut;
        public AnimationClip spawnStand;
    }

    [MenuItem(MenuPath)]
    public static void Install()
    {
        AnimatorController controller = ResolveController();

        if (controller == null)
        {
            EditorUtility.DisplayDialog(
                "Zombie Animator",
                "No encontré ZombieAnimator.controller.\n\nSeleccioná el controller en Project y volvé a ejecutar:\n" + MenuPath,
                "OK"
            );
            return;
        }

        // Estos dos clips tienen desplazamiento de root. Como la locomoción real
        // la maneja NavMeshAgent, horneamos Y/XZ/rotación dentro de la pose para que
        // el cuerpo salga de la tierra sin mover físicamente el GameObject.
        ConfigureSpawnClipImportSettings();

        if (!TryLoadClips(out Clips clips, out List<string> missing))
        {
            EditorUtility.DisplayDialog(
                "Faltan animaciones",
                "No modifiqué el controller. Faltan estos FBX:\n\n- " +
                string.Join("\n- ", missing) +
                "\n\nImportá primero la carpeta Animations incluida en el paquete.",
                "OK"
            );
            return;
        }

        string controllerPath = AssetDatabase.GetAssetPath(controller);
        Motion oldDeathMotion = FindStateMotion(controller, "Death");

        CreateBackup(controllerPath);

        try
        {
            RebuildController(controller, clips, oldDeathMotion);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(controllerPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);

            EditorUtility.DisplayDialog(
                "Zombie Animator listo",
                "Se actualizó:\n" + controllerPath +
                "\n\nSe conservaron el GUID del controller y la animación Death anterior.\n" +
                "El controller ahora usa locomoción 8D con variantes aleatorias, hits localizados, ataques y salida de la tierra.",
                "OK"
            );
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog(
                "Error al crear el Animator",
                "No se pudo completar la instalación. Revisá Console.\n\n" + ex.Message,
                "OK"
            );
        }
    }

    private static AnimatorController ResolveController()
    {
        if (Selection.activeObject is AnimatorController selected)
            return selected;

        string[] guids = AssetDatabase.FindAssets("ZombieAnimator t:AnimatorController");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller != null &&
                string.Equals(controller.name, "ZombieAnimator", StringComparison.OrdinalIgnoreCase))
            {
                return controller;
            }
        }

        return null;
    }

    private static void CreateBackup(string controllerPath)
    {
        string directory = Path.GetDirectoryName(controllerPath)?.Replace("\\", "/") ?? "Assets";
        string backupPath = directory + "/ZombieAnimator_BACKUP_ANTES_FLUIDO.controller";

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(backupPath) != null)
            return;

        AssetDatabase.CopyAsset(controllerPath, backupPath);
        AssetDatabase.ImportAsset(backupPath);
    }

    private static void ConfigureSpawnClipImportSettings()
    {
        ConfigureSpawnClipImport(
            "Zombie_Crawl_OutOfGround_To_CrawlIdle.FBX"
        );

        ConfigureSpawnClipImport(
            "Zombie_Crawl_CrawlIdle_To_Stand.FBX"
        );
    }

    private static void ConfigureSpawnClipImport(string exactFileName)
    {
        string assetPath = FindExactAssetPath(exactFileName);
        if (string.IsNullOrEmpty(assetPath))
            return;

        ModelImporter importer =
            AssetImporter.GetAtPath(assetPath) as ModelImporter;

        if (importer == null)
            return;

        ModelImporterClipAnimation[] clipSettings =
            importer.clipAnimations;

        if (clipSettings == null || clipSettings.Length == 0)
            clipSettings = importer.defaultClipAnimations;

        if (clipSettings == null || clipSettings.Length == 0)
            return;

        bool changed = false;

        foreach (ModelImporterClipAnimation clip in clipSettings)
        {
            // Y queda horneado en la pose: el cuerpo emerge visualmente del suelo
            // sin levantar el GameObject fuera del NavMesh.
            if (!clip.lockRootHeightY)
            {
                clip.lockRootHeightY = true;
                changed = true;
            }

            // X/Z NO se hornea. ZombieAI consume animator.deltaPosition durante
            // el spawn y mueve el GameObject real. Así, cuando OutOfGround termina,
            // CrawlToStand empieza exactamente donde quedó el zombie y no vuelve
            // de golpe a la posición original.
            if (clip.lockRootPositionXZ)
            {
                clip.lockRootPositionXZ = false;
                changed = true;
            }

            if (!clip.lockRootRotation)
            {
                clip.lockRootRotation = true;
                changed = true;
            }

            if (!clip.keepOriginalPositionY)
            {
                clip.keepOriginalPositionY = true;
                changed = true;
            }

            if (!clip.keepOriginalPositionXZ)
            {
                clip.keepOriginalPositionXZ = true;
                changed = true;
            }

            if (!clip.keepOriginalOrientation)
            {
                clip.keepOriginalOrientation = true;
                changed = true;
            }

            if (clip.loopTime)
            {
                clip.loopTime = false;
                changed = true;
            }

            if (clip.loopPose)
            {
                clip.loopPose = false;
                changed = true;
            }
        }

        if (!changed && importer.clipAnimations != null &&
            importer.clipAnimations.Length > 0)
        {
            return;
        }

        importer.clipAnimations = clipSettings;
        importer.SaveAndReimport();
    }

    private static string FindExactAssetPath(string exactFileName)
    {
        string searchName = Path.GetFileNameWithoutExtension(exactFileName);
        string[] guids = AssetDatabase.FindAssets(searchName);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.Equals(
                Path.GetFileName(path),
                exactFileName,
                StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    private static bool TryLoadClips(out Clips clips, out List<string> missing)
    {
        clips = new Clips();
        missing = new List<string>();

        // Todos los FBX entregados copian su Avatar desde T_pose.fbx. Si se pierde ese
        // GUID, Unity puede importar los clips sin un Avatar válido.
        const string avatarGuid = "cf901b52c724af143b1a9f8c2add5b14";
        if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(avatarGuid)))
            missing.Add("T_pose.fbx (Avatar requerido por los FBX)");

        Dictionary<string, AnimationClip> map = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in RequiredClipFiles)
        {
            AnimationClip clip = FindAnimationClipByFileName(file);
            if (clip == null)
                missing.Add(file);
            else
                map[file] = clip;
        }

        if (missing.Count > 0)
            return false;

        clips.idle1 = map["Zombie_Idle_01.FBX"];
        clips.idle2 = map["Zombie_Idle_02.FBX"];
        clips.idle3 = map["Zombie_Idle_03.FBX"];
        clips.idleSearch = map["Zombie_Idle_Search.FBX"];

        clips.walk8 = new[]
        {
            map["Zombie_Walk_01_Forward_InPlace.fbx"],
            map["Zombie_Walk_01_ForwardRight_InPlace.fbx"],
            map["Zombie_Walk_01_Right_InPlace.fbx"],
            map["Zombie_Walk_01_BackRight_InPlace.fbx"],
            map["Zombie_Walk_01_Back_InPlace.fbx"],
            map["Zombie_Walk_01_BackLeft_InPlace.fbx"],
            map["Zombie_Walk_01_Left_InPlace.fbx"],
            map["Zombie_Walk_01_ForwardLeft_InPlace.fbx"]
        };

        clips.run8 = new[]
        {
            map["Zombie_Run_01_Forward_InPlace.fbx"],
            map["Zombie_Run_01_ForwardRight_InPlace.fbx"],
            map["Zombie_Run_01_Right_InPlace.fbx"],
            map["Zombie_Run_01_BackRight_InPlace.fbx"],
            map["Zombie_Run_01_Back_InPlace.fbx"],
            map["Zombie_Run_01_BackLeft_InPlace.fbx"],
            map["Zombie_Run_01_Left_InPlace.fbx"],
            map["Zombie_Run_01_ForwardLeft_InPlace.fbx"]
        };

        clips.walkForwardVariants = new[]
        {
            map["Zombie_Walk_01_Forward_InPlace.fbx"],
            map["Zombie_Walk_02_Forward_InPlace.fbx"],
            map["Zombie_Walk_03_Forward_InPlace.fbx"],
            map["Zombie_Walk_04_Forward_InPlace.fbx"],
            map["Zombie_Walk_Fast01_Forward_InPlace.fbx"]
        };

        clips.runForwardVariants = new[]
        {
            map["Zombie_Run_01_Forward_InPlace.fbx"],
            map["Zombie_Run_02_Forward_InPlace.fbx"]
        };

        clips.sprintForwardVariants = new[]
        {
            map["Zombie_Sprint_01_Forward_InPlace.fbx"],
            map["Zombie_Sprint_02_Forward_InPlace.fbx"]
        };

        clips.attacks = new[]
        {
            map["Zombie_Attack01.FBX"],
            map["Zombie_Attack02.FBX"],
            map["Zombie_Attack03.FBX"],
            map["Zombie_Attack_180Turn.FBX"],
            map["Zombie_Attack_90TurnLeft.FBX"],
            map["Zombie_Attack_90TurnRight.FBX"],
            map["Zombie_Attack_Forward01.fbx"],
            map["Zombie_Attack_ForwardJump.fbx"]
        };

        clips.hits = new[]
        {
            map["Zombie_HitReact_Head.fbx"],
            map["Zombie_HitReact_LeftArm.fbx"],
            map["Zombie_HitReact_RightArm.fbx"]
        };

        clips.spawnOut = map["Zombie_Crawl_OutOfGround_To_CrawlIdle.FBX"];
        clips.spawnStand = map["Zombie_Crawl_CrawlIdle_To_Stand.FBX"];

        return true;
    }

    private static AnimationClip FindAnimationClipByFileName(string exactFileName)
    {
        string searchName = Path.GetFileNameWithoutExtension(exactFileName);
        string[] guids = AssetDatabase.FindAssets(searchName);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!string.Equals(Path.GetFileName(path), exactFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            AnimationClip clip = assets
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));

            if (clip != null)
                return clip;
        }

        return null;
    }

    private static Motion FindStateMotion(AnimatorController controller, string stateName)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            Motion motion = FindStateMotionRecursive(layer.stateMachine, stateName);
            if (motion != null)
                return motion;
        }

        return null;
    }

    private static Motion FindStateMotionRecursive(AnimatorStateMachine machine, string stateName)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state != null &&
                string.Equals(child.state.name, stateName, StringComparison.OrdinalIgnoreCase))
            {
                return child.state.motion;
            }
        }

        foreach (ChildAnimatorStateMachine childMachine in machine.stateMachines)
        {
            Motion motion = FindStateMotionRecursive(childMachine.stateMachine, stateName);
            if (motion != null)
                return motion;
        }

        return null;
    }

    private static void RebuildController(
        AnimatorController controller,
        Clips clips,
        Motion oldDeathMotion)
    {
        controller.parameters = Array.Empty<AnimatorControllerParameter>();

        AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
        AddParameter(controller, "MoveY", AnimatorControllerParameterType.Float);
        AddParameter(controller, "IdleVariant", AnimatorControllerParameterType.Float);
        AddParameter(controller, "WalkVariant", AnimatorControllerParameterType.Float);
        AddParameter(controller, "RunVariant", AnimatorControllerParameterType.Float);
        AddParameter(controller, "SprintVariant", AnimatorControllerParameterType.Float);
        AddParameter(controller, "Running", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Sprint", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Spawning", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "AttackIndex", AnimatorControllerParameterType.Int);
        AddParameter(controller, "HitIndex", AnimatorControllerParameterType.Int);
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Death", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Spawn", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine root = new AnimatorStateMachine
        {
            name = "Base Layer",
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(root, controller);

        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "Base Layer",
            defaultWeight = 1f,
            stateMachine = root
        };
        controller.layers = new[] { layer };

        // ---------------- IDLE ----------------
        BlendTree idleTree = Create1DBlendTree(controller, "Idle Variants", "IdleVariant");
        idleTree.AddChild(clips.idle1, 0f);
        idleTree.AddChild(clips.idle2, 1f);
        idleTree.AddChild(clips.idle3, 2f);
        idleTree.AddChild(clips.idleSearch, 3f);

        AnimatorState idle = AddState(root, "Idle", idleTree, new Vector3(300f, 120f, 0f));
        idle.tag = "Idle";
        root.defaultState = idle;

        // ---------------- LOCOMOTION ----------------
        // Los 8 sentidos siguen disponibles. El hijo Forward del árbol direccional
        // contiene otro BlendTree 1D que elige una variante aleatoria.
        BlendTree walkTree = CreateDirectionalTreeWithForwardVariants(
            controller,
            "Walk 8 Directional + Variants",
            clips.walk8,
            clips.walkForwardVariants,
            "WalkVariant"
        );

        BlendTree runTree = CreateDirectionalTreeWithForwardVariants(
            controller,
            "Run 8 Directional + Variants",
            clips.run8,
            clips.runForwardVariants,
            "RunVariant"
        );

        BlendTree sprintTree = CreateVariantTree(
            controller,
            "Sprint Variants",
            "SprintVariant",
            clips.sprintForwardVariants
        );

        AnimatorState walk = AddState(root, "Walk 8D Variants", walkTree, new Vector3(600f, -40f, 0f));
        AnimatorState run = AddState(root, "Run 8D Variants", runTree, new Vector3(600f, 80f, 0f));
        AnimatorState sprint = AddState(root, "Sprint Variants", sprintTree, new Vector3(600f, 200f, 0f));

        foreach (AnimatorState movement in new[] { walk, run, sprint })
            movement.tag = "Locomotion";

        AddMovementTransitions(idle, idle, walk, run, sprint);
        AddMovementTransitions(walk, idle, walk, run, sprint);
        AddMovementTransitions(run, idle, walk, run, sprint);
        AddMovementTransitions(sprint, idle, walk, run, sprint);

        // ---------------- SPAWN ----------------
        AnimatorState spawnOut = AddState(root, "Spawn - Out Of Ground", clips.spawnOut, new Vector3(-250f, -160f, 0f));
        AnimatorState spawnStand = AddState(root, "Spawn - Crawl To Stand", clips.spawnStand, new Vector3(0f, -160f, 0f));
        spawnOut.tag = "Spawning";
        spawnStand.tag = "Spawning";
        spawnOut.speed = 1.25f;
        spawnStand.speed = 1.35f;

        AnimatorStateTransition toSpawn = root.AddAnyStateTransition(spawnOut);
        ConfigureImmediate(toSpawn, 0.06f);
        toSpawn.canTransitionToSelf = false;
        toSpawn.AddCondition(AnimatorConditionMode.If, 0f, "Spawn");

        AnimatorStateTransition outToStand = spawnOut.AddTransition(spawnStand);
        // Dejamos que OutOfGround alcance prácticamente su pose final de CrawlIdle.
        ConfigureExit(outToStand, 0.995f, 0.04f);

        AnimatorStateTransition standToIdle = spawnStand.AddTransition(idle);
        ConfigureExit(standToIdle, 0.985f, 0.08f);

        // ---------------- ATTACKS ----------------
        for (int i = 0; i < clips.attacks.Length; i++)
        {
            AnimatorState attack = AddState(
                root,
                "Attack " + i,
                clips.attacks[i],
                new Vector3(1150f, -220f + i * 70f, 0f)
            );
            attack.tag = "Attack";
            attack.speed = i <= 5 ? 1.75f : 1.55f;

            AnimatorStateTransition anyToAttack = root.AddAnyStateTransition(attack);
            ConfigureImmediate(anyToAttack, 0.07f);
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Spawning");
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            anyToAttack.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");

            AnimatorStateTransition attackToIdle = attack.AddTransition(idle);
            ConfigureExit(attackToIdle, 0.94f, 0.10f);
        }

        // ---------------- HIT REACTIONS ----------------
        for (int i = 0; i < clips.hits.Length; i++)
        {
            AnimatorState hit = AddState(
                root,
                "HitReaction " + i,
                clips.hits[i],
                new Vector3(-250f, 120f + i * 80f, 0f)
            );
            hit.tag = "HitReaction";
            hit.speed = 1.55f;

            AnimatorStateTransition anyToHit = root.AddAnyStateTransition(hit);
            ConfigureImmediate(anyToHit, 0.055f);
            anyToHit.canTransitionToSelf = false;
            anyToHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "Spawning");
            anyToHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            anyToHit.AddCondition(AnimatorConditionMode.Equals, i, "HitIndex");

            AnimatorStateTransition hitToIdle = hit.AddTransition(idle);
            ConfigureExit(hitToIdle, 0.97f, 0.09f);
        }

        // ---------------- DEATH EXISTENTE ----------------
        if (oldDeathMotion != null)
        {
            AnimatorState death = AddState(root, "Death", oldDeathMotion, new Vector3(300f, 520f, 0f));
            death.tag = "Death";

            AnimatorStateTransition anyToDeath = root.AddAnyStateTransition(death);
            ConfigureImmediate(anyToDeath, 0.06f);
            anyToDeath.canTransitionToSelf = false;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0f, "Death");
        }
    }

    private static void AddParameter(
        AnimatorController controller,
        string name,
        AnimatorControllerParameterType type)
    {
        controller.AddParameter(name, type);
    }

    private static AnimatorState AddState(
        AnimatorStateMachine machine,
        string name,
        Motion motion,
        Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static BlendTree Create1DBlendTree(
        AnimatorController controller,
        string name,
        string parameter)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.Simple1D,
            blendParameter = parameter,
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };

        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    private static BlendTree CreateVariantTree(
        AnimatorController controller,
        string name,
        string parameter,
        AnimationClip[] variants)
    {
        BlendTree tree = Create1DBlendTree(controller, name, parameter);

        for (int i = 0; i < variants.Length; i++)
            tree.AddChild(variants[i], i);

        return tree;
    }

    private static BlendTree CreateDirectionalTreeWithForwardVariants(
        AnimatorController controller,
        string name,
        AnimationClip[] directionalClips,
        AnimationClip[] forwardVariants,
        string variantParameter)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy
        };

        AssetDatabase.AddObjectToAsset(tree, controller);

        BlendTree forwardTree = CreateVariantTree(
            controller,
            name + " - Forward",
            variantParameter,
            forwardVariants
        );

        const float d = 0.70710678f;
        Vector2[] directions =
        {
            new Vector2(0f, 1f),
            new Vector2(d, d),
            new Vector2(1f, 0f),
            new Vector2(d, -d),
            new Vector2(0f, -1f),
            new Vector2(-d, -d),
            new Vector2(-1f, 0f),
            new Vector2(-d, d)
        };

        // Forward usa el subárbol aleatorio.
        tree.AddChild(forwardTree, directions[0]);

        // El resto mantiene las animaciones 8-directional reales del pack.
        for (int i = 1; i < 8; i++)
            tree.AddChild(directionalClips[i], directions[i]);

        return tree;
    }

    private static void AddMovementTransitions(
        AnimatorState from,
        AnimatorState idle,
        AnimatorState walk,
        AnimatorState run,
        AnimatorState sprint)
    {
        if (from != idle)
        {
            AnimatorStateTransition toIdle = from.AddTransition(idle);
            ConfigureImmediate(toIdle, 0.14f);
            toIdle.AddCondition(AnimatorConditionMode.Less, MoveThreshold, "Speed");
        }

        AddMovementTransition(from, walk,
            (AnimatorConditionMode.Greater, MoveThreshold, "Speed"),
            (AnimatorConditionMode.IfNot, 0f, "Running"));

        AddMovementTransition(from, run,
            (AnimatorConditionMode.Greater, MoveThreshold, "Speed"),
            (AnimatorConditionMode.If, 0f, "Running"),
            (AnimatorConditionMode.IfNot, 0f, "Sprint"));

        AddMovementTransition(from, sprint,
            (AnimatorConditionMode.Greater, MoveThreshold, "Speed"),
            (AnimatorConditionMode.If, 0f, "Running"),
            (AnimatorConditionMode.If, 0f, "Sprint"));
    }

    private static void AddMovementTransition(
        AnimatorState from,
        AnimatorState to,
        params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
    {
        if (from == to)
            return;

        AnimatorStateTransition transition = from.AddTransition(to);
        ConfigureImmediate(transition, 0.13f);

        foreach (var condition in conditions)
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
    }

    private static void ConfigureImmediate(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        transition.orderedInterruption = true;
    }

    private static void ConfigureExit(
        AnimatorStateTransition transition,
        float exitTime,
        float duration)
    {
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.None;
        transition.orderedInterruption = true;
    }
}
#endif
