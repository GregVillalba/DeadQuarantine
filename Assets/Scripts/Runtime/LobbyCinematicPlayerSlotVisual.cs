using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Actor visual para un jugador del lobby.
/// Es una copia visual del Player prefab, sin gameplay.
/// </summary>
public class LobbyCinematicPlayerSlotVisual : MonoBehaviour
{
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform anchor;
    [SerializeField] private string weaponId = "Handgun";
    [SerializeField] private float animatorSpeed = 0.92f;
    [SerializeField] private float subtleTurnDegrees = 0.8f;
    [SerializeField] private float subtleTurnSpeed = 0.22f;

    private GameObject current;
    private Animator bodyAnimator;
    private int currentWeaponAnimatorId = 1;
    private int currentCharacterIndex = int.MinValue;
    private Quaternion baseAnchorRotation;

    private static readonly int TP_Speed =
        Animator.StringToHash("TP_Speed");
    private static readonly int TP_IsAiming =
        Animator.StringToHash("TP_IsAiming");
    private static readonly int TP_MoveX =
        Animator.StringToHash("TP_MoveX");
    private static readonly int TP_MoveZ =
        Animator.StringToHash("TP_MoveZ");
    private static readonly int TP_MoveAmount =
        Animator.StringToHash("TP_MoveAmount");
    private static readonly int TP_WeaponID =
        Animator.StringToHash("TP_WeaponID");
    private static readonly int TP_IsAlive =
        Animator.StringToHash("TP_IsAlive");
    private static readonly int TP_IsGrounded =
        Animator.StringToHash("TP_IsGrounded");
    private static readonly int TP_IsReloading =
        Animator.StringToHash("TP_IsReloading");
    private static readonly int TP_IsSprinting =
        Animator.StringToHash("TP_IsSprinting");
    private static readonly int TP_IsCrouching =
        Animator.StringToHash("TP_IsCrouching");
    private static readonly int TP_AimWeight =
        Animator.StringToHash("TP_AimWeight");

    public Transform Anchor => anchor;

    public void Configure(
        GameObject prefab,
        Transform newAnchor,
        string preferredWeaponId)
    {
        playerPrefab = prefab;
        anchor = newAnchor;
        weaponId = string.IsNullOrWhiteSpace(preferredWeaponId)
            ? "Handgun"
            : preferredWeaponId;

        if (anchor != null)
            baseAnchorRotation = anchor.rotation;
    }

    private void OnEnable()
    {
        if (anchor != null)
            baseAnchorRotation = anchor.rotation;
    }

    private void OnDisable()
    {
        if (current != null)
        {
            Destroy(current);
            current = null;
        }

        bodyAnimator = null;
        currentCharacterIndex = int.MinValue;
    }

    private void Update()
    {
        UpdatePose();
        UpdateSubtleMotion();
    }

    public void SetCharacterIndex(
        int characterIndex)
    {
        if (characterIndex < 0)
            characterIndex = 0;

        if (current != null &&
            currentCharacterIndex == characterIndex)
        {
            return;
        }

        currentCharacterIndex = characterIndex;
        RebuildPlayer(characterIndex);
    }

    public void SetVisible(
        bool value)
    {
        if (anchor != null &&
            anchor.gameObject.activeSelf != value)
        {
            anchor.gameObject.SetActive(value);
        }
    }

    private void RebuildPlayer(
        int characterIndex)
    {
        if (playerPrefab == null ||
            anchor == null)
        {
            return;
        }

        if (current != null)
            Destroy(current);

        bodyAnimator = null;

        current =
            Instantiate(
                playerPrefab,
                anchor
            );

        current.name =
            "Lobby_Player_Visual";

        current.transform.localPosition =
            Vector3.zero;

        current.transform.localRotation =
            Quaternion.identity;

        current.transform.localScale =
            Vector3.one;

        PlayerCharacter playerCharacter =
            current.GetComponent<PlayerCharacter>();

        if (playerCharacter != null)
        {
            MethodInfo apply =
                typeof(PlayerCharacter).GetMethod(
                    "Apply",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic
                );

            if (apply != null)
            {
                try
                {
                    apply.Invoke(
                        playerCharacter,
                        new object[] { characterIndex }
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[LobbyCinematicPlayerSlotVisual] " +
                        "No pude aplicar la apariencia elegida: " +
                        ex.Message
                    );
                }
            }
        }

        NetworkWeaponPresentation presentation =
            current.GetComponent<NetworkWeaponPresentation>();

        Transform bodyRoot = null;

        if (presentation != null)
        {
            bodyRoot = presentation.Body;
            bodyAnimator = presentation.BodyAnimator;

            NetworkWeaponPresentation.Definition[] defs =
                presentation.Weapons;

            if (defs != null &&
                defs.Length > 0)
            {
                int index =
                    FindWeaponIndex(defs, weaponId);

                if (index < 0)
                    index = 0;

                currentWeaponAnimatorId =
                    index + 1;

                NetworkWeaponPresentation.Definition def =
                    defs[index];

                if (bodyAnimator != null &&
                    def.Controller != null)
                {
                    bodyAnimator.runtimeAnimatorController =
                        def.Controller;
                }

                if (bodyRoot != null &&
                    def.Model != null)
                {
                    Transform hand =
                        FindDeep(bodyRoot, "Hand_R");

                    if (hand != null)
                    {
                        GameObject held =
                            Instantiate(def.Model, hand);

                        held.name = "Lobby_" + def.Id;
                        held.transform.localPosition =
                            def.GripPosition;
                        held.transform.localRotation =
                            Quaternion.Euler(def.GripRotation);
                        held.transform.localScale =
                            Vector3.one;

                        ForceRenderers(held);
                    }
                }
            }
        }

        if (bodyRoot != null)
        {
            Renderer[] renderers =
                current.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                bool belongsToBody =
                    renderer.transform == bodyRoot ||
                    renderer.transform.IsChildOf(bodyRoot);

                renderer.enabled = belongsToBody;

                if (belongsToBody)
                {
                    renderer.forceRenderingOff = false;

                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        skinned.updateWhenOffscreen = true;
                    }
                }
            }
        }
        else
        {
            ForceRenderers(current);
        }

        DisableGameplay(current);

        if (bodyAnimator != null)
        {
            bodyAnimator.enabled = true;
            bodyAnimator.applyRootMotion = false;
            bodyAnimator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
            bodyAnimator.updateMode =
                AnimatorUpdateMode.UnscaledTime;
            bodyAnimator.speed = animatorSpeed;
            bodyAnimator.Rebind();
            bodyAnimator.Update(0f);
        }

        UpdatePose();
    }

    private void UpdatePose()
    {
        if (bodyAnimator == null)
            return;

        SetFloat(TP_Speed, 0f);
        SetFloat(TP_MoveX, 0f);
        SetFloat(TP_MoveZ, 0f);
        SetFloat(TP_MoveAmount, 0f);
        SetFloat(TP_WeaponID, currentWeaponAnimatorId);
        SetFloat(TP_AimWeight, 0f);

        SetBool(TP_IsAlive, true);
        SetBool(TP_IsGrounded, true);
        SetBool(TP_IsAiming, false);
        SetBool(TP_IsReloading, false);
        SetBool(TP_IsSprinting, false);
        SetBool(TP_IsCrouching, false);
    }

    private void UpdateSubtleMotion()
    {
        if (anchor == null)
            return;

        float yaw =
            Mathf.Sin(Time.unscaledTime * subtleTurnSpeed) *
            subtleTurnDegrees;

        anchor.rotation =
            baseAnchorRotation *
            Quaternion.Euler(0f, yaw, 0f);
    }

    private static int FindWeaponIndex(
        NetworkWeaponPresentation.Definition[] defs,
        string wanted)
    {
        if (defs == null)
            return -1;

        for (int i = 0; i < defs.Length; i++)
        {
            if (defs[i] == null)
                continue;

            if (string.Equals(
                defs[i].Id,
                wanted,
                StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void SetBool(
        int hash,
        bool value)
    {
        if (!HasParameter(
            hash,
            AnimatorControllerParameterType.Bool))
        {
            return;
        }

        bodyAnimator.SetBool(hash, value);
    }

    private void SetFloat(
        int hash,
        float value)
    {
        if (!HasParameter(
            hash,
            AnimatorControllerParameterType.Float))
        {
            return;
        }

        bodyAnimator.SetFloat(hash, value);
    }

    private bool HasParameter(
        int hash,
        AnimatorControllerParameterType type)
    {
        if (bodyAnimator == null)
            return false;

        AnimatorControllerParameter[] pars =
            bodyAnimator.parameters;

        for (int i = 0; i < pars.Length; i++)
        {
            if (pars[i].nameHash == hash &&
                pars[i].type == type)
            {
                return true;
            }
        }

        return false;
    }

    private static void DisableGameplay(
        GameObject root)
    {
        foreach (Camera cam in
            root.GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = false;
        }

        foreach (AudioListener listener in
            root.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = false;
        }

        foreach (Collider col in
            root.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false;
        }

        foreach (Rigidbody rb in
            root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        foreach (MonoBehaviour behaviour in
            root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
                continue;

            behaviour.enabled = false;
        }
    }

    private static Transform FindDeep(
        Transform root,
        string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void ForceRenderers(
        GameObject root)
    {
        if (root == null)
            return;

        foreach (Renderer renderer in
            root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            renderer.forceRenderingOff = false;

            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.updateWhenOffscreen = true;
            }
        }
    }
}
