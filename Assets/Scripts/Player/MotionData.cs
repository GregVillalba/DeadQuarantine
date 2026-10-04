using UnityEngine;

// =====================================================================================
//  ARCHIVO GENERADO a partir de los .asset del proyecto original (Infima LPSP):
//  Recoil/Curves, Jumping, Sway Settings. Los keyframes (tiempo, valor, tangentes)
//  son los reales, no estimados. NO hace falta tocarlo: los valores se ajustan en el
//  Inspector de cada componente (o con "Restaurar valores del original").
//
//  EJES: los motions del ARMA del original se aplican al hueso ik_hand_gun, cuyo espacio
//  local es el del Armature: X = derecha, Y = ATRÁS, Z = ARRIBA. WeaponMotionApplier
//  convierte eso a los ejes de tu FPS_Arms. Los de la CÁMARA usan ejes normales.
// =====================================================================================
public static class MotionData
{
    private static AnimationCurve Curve(params float[] keys)
    {
        // Grupos de 4: tiempo, valor, tangenteEntrada, tangenteSalida.
        AnimationCurve curve = new AnimationCurve();
        for (int i = 0; i + 3 < keys.Length; i += 4)
            curve.AddKey(new Keyframe(keys[i], keys[i + 1], keys[i + 2], keys[i + 3]));
        return curve;
    }

    public static MotionCurves RecoilAR()
    {
        return new MotionCurves(
            0.6f, new SpringSettings(25f, 150f, 1f, 1.3f), new AnimationCurve[] { Curve(0f, 0f, 0f, 0f, 40f, 0f, 0f, 0f), Curve(0f, 0f, 0.009621187f, 0.009621187f, 3.170534f, 0.030504301f, 0.004057731f, 0.004057731f, 9.605473f, 0.020815054f, 4.238324e-05f, 4.238324e-05f, 12.149252f, 0.02786665f, -9.591565e-05f, -9.591565e-05f, 14f, 0.024228102f, -0.00021598773f, float.PositiveInfinity, 41.064915f, 0.024394259f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 40f, 0f, 0f, 0f) },
            4f, new SpringSettings(25f, 150f, 1.5f, 1f), new AnimationCurve[] { Curve(0f, 0f, -4.7626414f, -4.7626414f, 1.1f, 0f, 8.598115f, 8.598115f, 2.2f, 0f, -7.7857957f, -7.7857957f), Curve(-0.0058898926f, 0.018508911f, -38.321175f, -38.321175f, 0.9941069f, 0f, 72.14552f, 72.14552f, 1.9882138f, 0f, -39.84691f, -39.84691f), Curve(0f, 0f, -0.9351005f, -0.9351005f, 2.8213525f, 0f, 0.7880297f, 0.7880297f, 5.642705f, 0f, -1.2904162f, -1.2904162f) });
    }

    public static MotionCurves RecoilSMG()
    {
        return new MotionCurves(
            1f, new SpringSettings(25f, 150f, 1f, 1.3f), new AnimationCurve[] { Curve(0f, 0f, 0f, 0f, 40f, 0f, 0f, 0f), Curve(0f, 0f, 0.011899612f, 0.011899612f, 3.170534f, 0.037728123f, 0.005082206f, 0.005082206f, 9.605473f, 0.02656222f, 0.00054233166f, 0.00054233166f, 12.21896f, 0.033931896f, -1.6006486e-05f, -1.6006486e-05f, 14f, 0.031990323f, 8.571287e-05f, float.PositiveInfinity, 41.498844f, 0.031767346f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 40f, 0f, 0f, 0f) },
            2f, new SpringSettings(15f, 150f, 1.1f, 1f), new AnimationCurve[] { Curve(0f, 0f, -4.7626414f, -4.7626414f, 1.1f, 0f, 8.598115f, 8.598115f, 2.2f, 0f, -7.7857957f, -7.7857957f), Curve(-0.0058898926f, 0.018508911f, -38.321175f, -38.321175f, 0.9941069f, 0f, 72.14552f, 72.14552f, 1.9882138f, 0f, -39.84691f, -39.84691f), Curve(0f, 0f, -0.9351005f, -0.9351005f, 2.8213525f, 0f, 0.7880297f, 0.7880297f, 5.642705f, 0f, -1.2904162f, -1.2904162f) });
    }

    public static MotionCurves RecoilCamera()
    {
        return new MotionCurves(
            1f, new SpringSettings(13f, 150f, 1f, 2f), new AnimationCurve[] { Curve(), Curve(), Curve() },
            1f, new SpringSettings(15f, 150f, 1.8f, 2.3f), new AnimationCurve[] { Curve(0.03236389f, 0f, -1.3485036f, -1.3485036f, 20f, -11.864287f, -0.014329689f, -0.014329689f, 24.463764f, -10.722601f, 0f, 0f), Curve(0f, -0.25f, -0.0067848656f, -0.0067848656f, 4f, 0.25f, 0f, 0f, 8f, -0.25f, 0.009980397f, 0.009980397f), Curve(0f, 0.1f, 1.1935829f, 0.004059049f, 4f, -0.1f, 0f, 0f, 8f, 0.1f, -0.00024146283f, -0.00024146283f) });
    }

    public static MotionCurves JumpWeapon()
    {
        return new MotionCurves(
            1f, new SpringSettings(15f, 200f, 1f, 1f), new AnimationCurve[] { Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f), Curve(0f, 0f, -5.5040374f, -5.5040374f, 0.024162954f, -0.1329938f, -1.4459367f, -1.4459367f, 0.102176934f, 0.07079151f, 0.38456622f, 0.38456622f, 0.16051234f, 0.0059920982f, -0.29677576f, -0.29677576f, 0.2f, 0.02f, 0.35474125f, 0.35474125f) },
            1f, new SpringSettings(11f, 150f, 1f, 1f), new AnimationCurve[] { Curve(0f, 0f, 121.47038f, 121.47038f, 0.013805813f, -12.497596f, 984.2741f, 984.2741f, 0.03086615f, 44.79797f, 1723.2646f, 1723.2646f, 0.08f, -5f, -2917.9932f, -2917.9932f), Curve(0f, 0f, 904.1857f, 904.1857f, 0.031440817f, 28.428337f, 10.920028f, 10.920028f, 0.07564846f, -54.1258f, -63.167934f, -63.167934f, 0.11442049f, 19.377163f, 152.52455f, 152.52455f, 0.15568748f, -5.8769054f, -51.69036f, -51.69036f, 0.20134632f, 3.2581434f, 116.65465f, 116.65465f, 0.2446167f, -0.71984863f, -91.93337f, -91.93337f), Curve(0f, 0f, 18f, 18f, 0.1f, 1.8f, 18f, 18f) });
    }

    public static MotionCurves JumpCamera()
    {
        return new MotionCurves(
            1f, new SpringSettings(15f, 150f, 1f, 1f), new AnimationCurve[] { Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.2f, 0f, 0f, 0f) },
            1f, new SpringSettings(15f, 200f, 1f, 1f), new AnimationCurve[] { Curve(0f, 0.134552f, 1901.3866f, 1901.3866f, 0.025007881f, 35.072742f, -349.38107f, -349.38107f, 0.040480003f, -16.436989f, -89.32217f, -89.32217f, 0.0681498f, 4.1072254f, 158.56767f, 158.56767f, 0.091853075f, -1.5545273f, -67.97817f, -67.97817f, 0.11f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.2446167f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f) });
    }

    public static MotionCurves FallWeapon()
    {
        return new MotionCurves(
            1f, new SpringSettings(15f, 150f, 1f, 1f), new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0.00053602556f, 0.00053602556f, 1f, 0f, -0.0007042933f, -0.0007042933f), Curve(0f, 0f, 0f, 0f, 0.18243617f, 0f, 0f, 0f) },
            1f, new SpringSettings(15f, 150f, 1f, 1f), new AnimationCurve[] { Curve(0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) });
    }

    public static MotionCurves FallCamera()
    {
        return new MotionCurves(
            1f, new SpringSettings(15f, 150f, 1f, 1f), new AnimationCurve[] { Curve(), Curve(), Curve() },
            1f, new SpringSettings(15f, 150f, 1f, 1f), new AnimationCurve[] { Curve(), Curve(), Curve() });
    }

    public static MotionCurves LandWeapon()
    {
        return new MotionCurves(
            1f, new SpringSettings(20f, 200f, 1f, 1.25f), new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(0f, -0.05f, -1.5982054f, -1.5982054f, 0.07672602f, -0.051095594f, 1.2807252f, 1.2807252f, 0.19382253f, 0.028878506f, 0.0073414524f, 0.0073414524f, 0.3f, 0f, -0.27198336f, -0.27198336f) },
            1f, new SpringSettings(9f, 200f, 1f, 1f), new AnimationCurve[] { Curve(0f, -75f, 5442.7856f, 5442.7856f, 0.039868165f, 41.849888f, -292.09915f, -292.09915f, 0.07546839f, -14.585838f, -495.345f, -495.345f, 0.1f, 0f, 594.5732f, 594.5732f), Curve(0.00024414062f, -0.054733276f, 1185.8762f, 1185.8762f, 0.012891825f, 9.956558f, -0.29473338f, -0.29473338f, 0.034184817f, -17.959877f, -1.2185087f, -1.2185087f, 0.061486028f, 9.933784f, 0.6211122f, 0.6211122f, 0.0938507f, -6.3786926f, 0.6753371f, 0.6753371f, 0.13599807f, 3.518958f, 6.7653465f, 6.7653465f, 0.18982545f, -0.87750244f, -33.37059f, -33.37059f, 0.25f, 0.021256335f, 14.935861f, 14.935861f), Curve(0f, 0f, 0f, 0f, 0.021951148f, 0f, 0f, 0f, 0.050014637f, 0f, 0f, 0f) });
    }

    public static MotionCurves LandCamera()
    {
        return new MotionCurves(
            1f, new SpringSettings(15f, 150f, 1f, 1f), new AnimationCurve[] { Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.2f, 0f, 0f, 0f) },
            1f, new SpringSettings(15f, 200f, 1f, 1f), new AnimationCurve[] { Curve(-0.00018310547f, 0.134552f, 7087.3325f, 7087.3325f, 0.005692754f, 50f, -104.30957f, -104.30957f, 0.0144354245f, -16.377365f, 0f, 0f, 0.024627969f, 2.506124f, 0f, 0f, 0.033525273f, -1.1721025f, -53.985188f, -53.985188f, 0.04f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.2446167f, 0f, 0f, 0f), Curve(0f, 0f, 0f, 0f, 0.1f, 0f, 0f, 0f) });
    }

    public static SwayData SwayDefault()
    {
        return new SwayData(new SpringSettings(12f, 165f, 1f, 1f),
            new SwayType(
            new MotionCurves(1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0.03f, -0.03f, -0.03f, 0f, 0f, -0.03f, -0.03f, 1f, -0.03f, -0.03f, -0.03f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, -0.01f, 0.01f, 0.01f, 0f, 0f, 0f, 0f, 1f, -0.01f, -0.01f, -0.01f) },
                1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, -5f, 5f, 5f, 0f, 0f, 5f, 5f, 1f, 5f, 5f, 5f), Curve(-1f, -2f, 2f, 2f, 0f, 0f, 2f, 2f, 1f, 2f, 2f, 2f) }),
            new MotionCurves(1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0.025f, 0.031840824f, 0.031840824f, 0f, 0f, -0.047889724f, -0.047889724f, 0.99676514f, -0.025f, 0.020769546f, 0.020769546f) },
                1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 1f, -1f, -1f, 0f, 0f, -1f, -1f, 1f, -1f, -1f, -1f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) })),
            new SwayType(
            new MotionCurves(2f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, -0.005f, 0.005f, 0.005f, 0f, 0f, 0.005f, 0.005f, 1f, 0.005f, 0.005f, 0.005f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) },
                2f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0.5f, -0.5f, -0.5f, 0f, 0f, -0.5f, -0.5f, 1f, -0.5f, -0.5f, -0.5f) }),
            new MotionCurves(2f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0.005f, -0.005f, -0.005f, 0f, 0f, -0.005f, -0.005f, 1f, -0.005f, -0.005f, -0.005f) },
                2f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) })));
    }

    public static SwayData SwayAiming()
    {
        return new SwayData(new SpringSettings(16f, 165f, 1f, 1f),
            new SwayType(
            new MotionCurves(0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0.025f, 0.06664852f, 0.06664852f, 0f, 0f, -0.1254174f, -0.1254174f, 1f, -0.025f, 0.11712433f, 0.11712433f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, -0.01f, 0.01f, 0.01f, 0f, 0f, 0f, 0f, 1f, -0.01f, -0.01f, -0.01f) },
                0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, -7f, 7f, 7f, 0f, 0f, 7f, 7f, 1f, 7f, 7f, 7f), Curve(-1f, -3f, 3f, 3f, 0f, 0f, 3f, 3f, 1f, 3f, 3f, 3f) }),
            new MotionCurves(0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0.025f, 0.031840824f, 0.031840824f, 0f, 0f, -0.047889724f, -0.047889724f, 0.99676514f, -0.025f, 0.020769546f, 0.020769546f) },
                0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 5f, -5f, -5f, 0f, 0f, -5f, -5f, 1f, -5f, -5f, -5f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) })),
            new SwayType(
            new MotionCurves(0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, -0.008f, 0.008f, 0.008f, 0f, 0f, 0.008f, 0.008f, 1f, 0.008f, 0.008f, 0.008f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) },
                0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 1f, -1f, -1f, 0f, 0f, -1f, -1f, 1f, -1f, -1f, -1f) }),
            new MotionCurves(0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0.005f, -0.005f, -0.005f, 0f, 0f, -0.005f, -0.005f, 1f, -0.005f, -0.005f, -0.005f) },
                0.1f, SpringSettings.Default, new AnimationCurve[] { Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f), Curve(-1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f) })));
    }

}