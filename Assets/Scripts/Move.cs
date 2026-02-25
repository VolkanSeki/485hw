using UnityEngine;

public class Move : MonoBehaviour
{
    public Rigidbody rb;
    public Vector3 kuvvetmiktari; 
    void FixedUpdate() {
        if (rb != null)
        {
            rb.AddForce(kuvvetmiktari);
        }
    }
}
