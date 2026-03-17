using UnityEngine;
using System.Collections;

public class CarryMode : MonoBehaviour
{
    [Header("Settings")]
    public float carryDistance = 0.5f; // Desired distance in front of the player while carrying
    public KeyCode carryKey = KeyCode.E; 
    
    [Header("References")]
    public Transform playerTransform; // Player transform reference (assign via Inspector)
    
    private Rigidbody rb;
    private Collider keyCollider; // Anahtarın çarpışma kutusu
    private bool isCarrying = false;
    private Coroutine carryCoroutine;

    void Start() 
    { 
        rb = GetComponent<Rigidbody>(); 
        keyCollider = GetComponent<Collider>(); // Cache the key's collider component
    }

    void Update()
    {
        if (playerTransform == null) return;
        float dist = Vector3.Distance(transform.position, playerTransform.position);

        if (Input.GetKeyDown(carryKey))
        {
            if (!isCarrying && dist < 2.0f) StartCarrying();
            else if (isCarrying) StopCarrying();
        }
    }

    void StartCarrying()
    {
        isCarrying = true;

        // Freeze physics and reset movement while carrying
        rb.isKinematic = true; 
        rb.useGravity = false; 
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Disable collisions completely while the object is being carried
        if (keyCollider != null)
        {
            keyCollider.enabled = false; // Turn off collider while carried
        }

        // Start coroutine to keep the object following the player
        carryCoroutine = StartCoroutine(FollowPlayerRoutine());
    }

    // Public so other scripts (e.g. LoseArranger) can stop carrying
    public void StopCarrying()
    {
        isCarrying = false;

        // Re-enable physics and gravity when dropping the object
        if (rb != null) 
        { 
            rb.isKinematic = false; 
            rb.useGravity = true; 
        }
        
        // Re-enable collisions so the object can interact with the environment again
        if (keyCollider != null)
        {
            keyCollider.enabled = true; // Turn collider back on when dropped
        }

        if (carryCoroutine != null)
        {
            StopCoroutine(carryCoroutine);
            carryCoroutine = null;
        }
    }

    IEnumerator FollowPlayerRoutine()
    {
        while (isCarrying)
        {
            // Position the object slightly in front of and above the player while carried
            transform.position = playerTransform.position + playerTransform.forward * carryDistance + Vector3.up * 0.4f;
            transform.rotation = playerTransform.rotation;
            yield return null; // Wait for the next frame
        }
    }
}