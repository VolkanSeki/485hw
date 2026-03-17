using UnityEngine;

public class PushPhysics : MonoBehaviour
{
    public float pushPower = 2.0f; // İtme gücü

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Eğer çarptığımız şeyin Rigidbody'si yoksa veya Kinematic ise itme
        if (body == null || body.isKinematic) return;

        // Aşağı doğru itmeyi engelle
        if (hit.moveDirection.y < -0.3) return;

        // İtme yönünü hesapla ve kuvvet uygula
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.velocity = pushDir * pushPower;
    }
}