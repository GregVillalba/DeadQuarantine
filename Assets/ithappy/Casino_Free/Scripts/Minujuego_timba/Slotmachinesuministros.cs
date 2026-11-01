using System.Collections;
using UnityEngine;

namespace ithappy.Casino
{
    /// <summary>
    /// Va en la máquina tragamonedas. Cuando sale premio arroja suministros.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PresetUVSlotMachine))]
    public sealed class SlotMachineSuministros : MonoBehaviour
    {
        [Header("Suministros")]
        [Tooltip("Prefabs posibles (munición, curas, recursos...). Se elige uno al azar por cada ítem.")]
        [SerializeField] private GameObject[] suministros;
        [Tooltip("De dónde salen. Si queda vacío se usa la máquina.")]
        [SerializeField] private Transform puntoDeSalida;
        [SerializeField, Min(1)] private int cantidadMin = 2;
        [SerializeField, Min(1)] private int cantidadMax = 4;

        [Header("Lanzamiento")]
        [SerializeField, Min(0f)] private float retrasoInicial = 0.3f;
        [SerializeField, Min(0f)] private float tiempoEntreItems = 0.15f;
        [SerializeField, Min(0f)] private float fuerzaImpulso = 4f;
        [SerializeField, Min(0f)] private float dispersion = 0.4f;

        private PresetUVSlotMachine machine;

        private void Awake()
        {
            machine = GetComponent<PresetUVSlotMachine>();
        }

        private void OnEnable()
        {
            if (machine != null)
                machine.OnSpinFinished += AlTerminarGiro;
        }

        private void OnDisable()
        {
            if (machine != null)
                machine.OnSpinFinished -= AlTerminarGiro;
        }

        private void AlTerminarGiro(bool victoria)
        {
            if (victoria)
                StartCoroutine(ArrojarSuministros());
        }

        private IEnumerator ArrojarSuministros()
        {
            if (suministros == null || suministros.Length == 0)
            {
                Debug.LogWarning($"[SlotMachineSuministros] No hay prefabs asignados en {name}.", this);
                yield break;
            }

            if (retrasoInicial > 0f)
                yield return new WaitForSeconds(retrasoInicial);

            Transform origen = puntoDeSalida != null ? puntoDeSalida : transform;
            int cantidad = Random.Range(cantidadMin, Mathf.Max(cantidadMin, cantidadMax) + 1);

            for (int i = 0; i < cantidad; i++)
            {
                GameObject prefab = suministros[Random.Range(0, suministros.Length)];

                if (prefab != null)
                {
                    Vector2 azar = Random.insideUnitCircle * dispersion;
                    Vector3 posicion = origen.position + new Vector3(azar.x, 0f, azar.y);

                    GameObject item = Instantiate(prefab, posicion, Random.rotation);

                    if (item.TryGetComponent(out Rigidbody rb))
                    {
                        Vector3 direccion = (origen.forward + Vector3.up).normalized;
                        rb.AddForce(direccion * fuerzaImpulso, ForceMode.Impulse);
                    }
                }

                if (tiempoEntreItems > 0f)
                    yield return new WaitForSeconds(tiempoEntreItems);
            }
        }
    }
}