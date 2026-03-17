using UnityEngine;

public class LoseArranger : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject losePanel; 
    
    [Header("References")]
    public GameObject key; // Reference to the key object in the scene
    
    // Player respawn coordinates
    private Vector3 playerSpawn = new Vector3(38f, 0.1f, 2f);
    // Key respawn coordinates
    private Vector3 keySpawn = new Vector3(24f, 1f, 38f);
    
    private bool isLost = false;
    private MovementInput playerMove; // Reference to the player's movement controller
    private Animator playerAnim;      // Reference to the player's animator

    void Start()
    {
        // Cache player movement and animation components
        playerMove = GetComponent<MovementInput>();
        playerAnim = GetComponent<Animator>();
    }

    void Update()
    {
        if (isLost && Input.GetKeyDown(KeyCode.Space))
        {
            ResetToSpawn();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Trigger lose condition when colliding with an enemy or trap
        if (other.CompareTag("Enemy") && !isLost)
        {
            TriggerLose();
        }
    }

    public void TriggerLose()
    {
        if (isLost) return;

        isLost = true;
        if (losePanel != null) losePanel.SetActive(true);
        
        // Play the player's "dead" animation
        if (playerAnim != null) playerAnim.SetTrigger("dead"); 
        
        // Disable player control while in the lose state
        if (playerMove != null) playerMove.enabled = false;    
    }

    void ResetToSpawn()
    {
        isLost = false;
        if (losePanel != null) losePanel.SetActive(false);
        if (playerAnim != null) playerAnim.SetTrigger("normal");

        // 1. Reset the player by disabling the character controller before teleporting
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        
        transform.position = playerSpawn;

        if (cc != null) cc.enabled = true;
        if (playerMove != null) playerMove.enabled = true;

        // 2. Reset the key object back to its spawn state
        if (key != null)
        {
            // Stop any active carrying behavior on the key immediately
            CarryMode carryScript = key.GetComponent<CarryMode>();
            if (carryScript != null)
            {
                carryScript.StopCarrying(); // Stop the follow coroutine on the key
                Debug.Log("CarryMode takibi durduruldu.");
            }

            Rigidbody rb = key.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Fully reset the key's physical movement
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                // Ensure physics settings are restored for the key
                rb.isKinematic = false; 
                rb.useGravity = true;
            }

            // Teleport the key back to its spawn coordinates
            key.transform.position = keySpawn;
            Debug.Log("Anahtar ışınlandı: " + keySpawn);
        }
        else
        {
            Debug.LogError("HATA: LoseArranger içindeki 'Key' kutucuğu boş! Hierarchy'den anahtarı sürükle.");
        }
    }
}