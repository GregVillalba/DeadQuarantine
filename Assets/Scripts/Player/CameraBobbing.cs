using UnityEngine;

public class CameraBobbing : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Configuración de Head Bobbing")]
    [Tooltip("Debe coincidir con el bobFrequency de WeaponSway.cs para estar sincronizado.")]
    [SerializeField] private float bobFrequency = 8f;
    [SerializeField] private float bobAmount = 0.05f;
    [SerializeField] private float bobFrequencySprintMultiplier = 1.5f;

    [Header("Alturas de Cámara (Agacharse)")]
    [Tooltip("Ajusta este valor según qué tan bajo deba ir la cámara al agacharse.")]
    [SerializeField] private float crouchingHeight = 0.4f;
    [Tooltip("Velocidad de la transición visual entre pararse y agacharse.")]
    [SerializeField] private float crouchTransitionSpeed = 8f;

    private float standingHeight = 0f;
    private float currentBasePosY = 0f;
    private float currentBobOffset = 0f;
    private float timer = 0f;

    void Start()
    {
        if (characterController == null) 
            characterController = GetComponentInParent<CharacterController>();
        
        if (playerMovement == null) 
            playerMovement = GetComponentInParent<PlayerMovement>();
            
        // Guarda la altura inicial como la altura por defecto (de pie)
        standingHeight = transform.localPosition.y;
        currentBasePosY = standingHeight;
    }

    void Update()
    {
        // 1. Determinar si el jugador está agachado
        bool isCrouching = playerMovement != null && playerMovement.IsCrouching;
        
        // 2. Definir la altura objetivo de la cámara
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;

        // 3. Interpolar suavemente entre la posición de pie y agachado
        currentBasePosY = Mathf.Lerp(currentBasePosY, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        // 4. Calcular la oscilación (Bobbing) solo en movimiento
        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        bool isWalking = horizontalVelocity.magnitude > 0.1f && characterController.isGrounded;

        if (isWalking)
        {
            float freq = bobFrequency * (playerMovement.IsSprinting ? bobFrequencySprintMultiplier : 1f);
            timer += Time.deltaTime * freq;
        }
        else
        {
            // Reiniciar el timer si la oscilación ya es casi nula para evitar cortes bruscos
            if (Mathf.Abs(currentBobOffset) < 0.001f)
                timer = 0f;
        }

        // Si no camina, la onda objetivo es 0, y se desvanece suavemente
        float targetBobOffset = isWalking ? Mathf.Sin(timer) * bobAmount : 0f;
        currentBobOffset = Mathf.Lerp(currentBobOffset, targetBobOffset, Time.deltaTime * 10f);

        // 5. Aplicar la posición final (Altura actual + Movimiento del Bobbing)
        transform.localPosition = new Vector3(
            transform.localPosition.x,
            currentBasePosY + currentBobOffset,
            transform.localPosition.z);
    }
}