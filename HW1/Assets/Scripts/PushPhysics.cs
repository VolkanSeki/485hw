using UnityEngine;

public class PushPhysics : MonoBehaviour
{
    public float pushPower = 2.0f; // Magnitude of the push applied to dynamic rigidbodies

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Ignore collisions with objects that are not physically simulated
        if (body == null || body.isKinematic) return;

        // Do not apply push forces when moving significantly downward
        if (hit.moveDirection.y < -0.3) return;

        // Compute horizontal push direction from the controller's movement and apply it
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.velocity = pushDir * pushPower;
    }
}