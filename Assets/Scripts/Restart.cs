using UnityEngine;

public class Restart : MonoBehaviour
{
    public GameObject target;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            target.transform.position = new Vector3(0f, 5f, 0f);

            
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero; 
                rb.angularVelocity = Vector3.zero; 
            }

            Debug.Log("Player restarted!");
        }
    }
}