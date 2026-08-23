using UnityEngine;

public class PushObjects : MonoBehaviour
{
    [SerializeField] private float pushPower = 2.0f;

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Si el objeto no tiene Rigidbody o es cinemático, no hacemos nada
        if (body == null || body.isKinematic) return;

        // No empujamos objetos que estén debajo de nuestros pies
        if (hit.moveDirection.y < -0.3f) return;

        // Calculamos la dirección del empuje basándonos en el movimiento del jugador
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Aplicamos la fuerza al objeto basada en su masa y nuestro poder de empuje
        body.linearVelocity = pushDir * pushPower;
    }
}