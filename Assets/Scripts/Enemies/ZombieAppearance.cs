using UnityEngine;
using Unity.Netcode;

public class ZombieAppearance : NetworkBehaviour
{
    [SerializeField] private Transform models;

    private GameObject[] modelObjects;

    private NetworkVariable<int> selectedModelIndex =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private void Awake()
    {
        if (models == null)
            return;

        modelObjects =
            new GameObject[models.childCount];

        for (int i = 0; i < models.childCount; i++)
        {
            modelObjects[i] =
                models.GetChild(i).gameObject;
        }
    }

    public void SelectRandomModel()
    {
        if (modelObjects == null ||
            modelObjects.Length == 0)
        {
            return;
        }

        int randomIndex =
            Random.Range(
                0,
                modelObjects.Length
            );

        selectedModelIndex.Value =
            randomIndex;

        ApplySelectedModel(
            randomIndex
        );
    }

    public override void OnNetworkSpawn()
    {
        selectedModelIndex.OnValueChanged +=
            OnModelChanged;

        ApplySelectedModel(
            selectedModelIndex.Value
        );
    }

    public override void OnNetworkDespawn()
    {
        selectedModelIndex.OnValueChanged -=
            OnModelChanged;
    }

    private void OnModelChanged(
        int previousIndex,
        int newIndex
    )
    {
        ApplySelectedModel(newIndex);
    }

    private void ApplySelectedModel(
        int selectedIndex
    )
    {
        if (modelObjects == null)
            return;

        for (int i = 0; i < modelObjects.Length; i++)
        {
            if (modelObjects[i] != null)
            {
                modelObjects[i].SetActive(
                    i == selectedIndex
                );
            }
        }
    }
}