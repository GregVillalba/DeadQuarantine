using System;
using UnityEngine;

/// <summary>
/// Qué recoil usa un arma. AR y SMG son las curvas del original (SO_WEP_Recoil_AR / _SMG).
/// Handgun NO existe en el original: usa las curvas SMG pero con muy poca intensidad.
/// (Se agrega al final para no cambiar los valores ya guardados en tus prefabs.)
/// </summary>
public enum RecoilPreset { AR, SMG, Handgun }

/// <summary>
/// Equivale a SpringSettings del original: damping / stiffness / mass / speed.
/// "speed" multiplica el tiempo del resorte (2 = el resorte corre al doble de velocidad).
/// </summary>
[Serializable]
public struct SpringSettings
{
    public float damping;
    public float stiffness;
    public float mass;
    public float speed;

    public SpringSettings(float damping, float stiffness, float mass, float speed)
    {
        this.damping = damping;
        this.stiffness = stiffness;
        this.mass = mass;
        this.speed = speed;
    }

    public static SpringSettings Default => new SpringSettings(15f, 150f, 1f, 1f);
}

/// <summary>
/// Equivale a ACurves del original: 3 curvas de posición (X, Y, Z), 3 de rotación,
/// un multiplicador para cada grupo y los resortes que las persiguen.
/// </summary>
[Serializable]
public class MotionCurves
{
    public float locationMultiplier = 1f;
    public SpringSettings locationSpring = SpringSettings.Default;
    public AnimationCurve[] locationCurves = NewCurves();

    public float rotationMultiplier = 1f;
    public SpringSettings rotationSpring = SpringSettings.Default;
    public AnimationCurve[] rotationCurves = NewCurves();

    public MotionCurves() { }

    public MotionCurves(float locationMultiplier, SpringSettings locationSpring, AnimationCurve[] locationCurves,
                        float rotationMultiplier, SpringSettings rotationSpring, AnimationCurve[] rotationCurves)
    {
        this.locationMultiplier = locationMultiplier;
        this.locationSpring = locationSpring;
        this.locationCurves = locationCurves;
        this.rotationMultiplier = rotationMultiplier;
        this.rotationSpring = rotationSpring;
        this.rotationCurves = rotationCurves;
    }

    private static AnimationCurve[] NewCurves()
    {
        return new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
    }

    /// <summary>Evalúa las 3 curvas en "time" (sin multiplicador, igual que EvaluateCurves del original).</summary>
    public static Vector3 Evaluate(AnimationCurve[] curves, float time)
    {
        if (curves == null)
            return Vector3.zero;

        Vector3 result = Vector3.zero;

        if (curves.Length > 0 && curves[0] != null) result.x = curves[0].Evaluate(time);
        if (curves.Length > 1 && curves[1] != null) result.y = curves[1].Evaluate(time);
        if (curves.Length > 2 && curves[2] != null) result.z = curves[2].Evaluate(time);

        return result;
    }

    public Vector3 EvaluateLocation(float time) => Evaluate(locationCurves, time);

    public Vector3 EvaluateRotation(float time) => Evaluate(rotationCurves, time);

    /// <summary>
    /// Mayor cantidad de KEYS entre las 6 curvas. El JumpMotion original usa "curve.length"
    /// (que en Unity es la cantidad de keys, no segundos) como duración del salto; se
    /// replica igual para que la pose en el aire sea idéntica a la del original.
    /// </summary>
    public int MaxKeyCount
    {
        get
        {
            int max = 0;

            if (locationCurves != null)
                foreach (AnimationCurve c in locationCurves)
                    if (c != null && c.length > max) max = c.length;

            if (rotationCurves != null)
                foreach (AnimationCurve c in rotationCurves)
                    if (c != null && c.length > max) max = c.length;

            return max;
        }
    }
}

/// <summary>Sway de un tipo (mirar o moverse): un eje horizontal y uno vertical. Equivale a SwayType.</summary>
[Serializable]
public class SwayType
{
    public MotionCurves horizontal = new MotionCurves();
    public MotionCurves vertical = new MotionCurves();

    public SwayType() { }

    public SwayType(MotionCurves horizontal, MotionCurves vertical)
    {
        this.horizontal = horizontal;
        this.vertical = vertical;
    }
}

/// <summary>Equivale a SwayData del original: resorte + sway al mirar + sway al moverse.</summary>
[Serializable]
public class SwayData
{
    public SpringSettings spring = new SpringSettings(12f, 165f, 1f, 1f);
    public SwayType look = new SwayType();
    public SwayType movement = new SwayType();

    public SwayData() { }

    public SwayData(SpringSettings spring, SwayType look, SwayType movement)
    {
        this.spring = spring;
        this.look = look;
        this.movement = movement;
    }
}