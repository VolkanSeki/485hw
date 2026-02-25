using UnityEngine;

public class Mover : MonoBehaviour
{
    public Rigidbody rb;
    public float velocity = 20f; 
    void Start()
    {

    }

    void FixedUpdate()
    {
        float dikey = Input.GetAxis("Vertical"); 
        
        float yatay = Input.GetAxis("Horizontal");

        Vector3 direction = new Vector3(yatay, 0, dikey);

        if (rb != null)
        {
            rb.AddForce(direction * velocity);
        }
    }
}