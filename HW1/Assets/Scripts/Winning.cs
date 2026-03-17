using UnityEngine;

public class Winning : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject winPanel; // Panel that renders the winning UI (e.g., a RawImage with the winning screen)

    [Header("References")]
    public GameObject player; // Player character instance
    public GameObject key;    // Key object that must reach the goal

    private Vector3 playerSpawn = new Vector3(38f, 0.1f, 2f);
    private Vector3 keySpawn = new Vector3(24f, 1f, 38f); 
    
    private bool isWon = false;
    private MovementInput move;
    private Animator anim;

    void Start()
    {
        if (player != null)
        {
            move = player.GetComponent<MovementInput>();
            anim = player.GetComponent<Animator>();
        }
    }

    void Update()
    {
        // While the win screen is visible, allow resetting the game state with SPACE
        if (isWon && Input.GetKeyDown(KeyCode.Space))
        {
            ResetToSpawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Trigger win condition when the key collides with the goal object
        if (collision.gameObject.name == "Key" && !isWon)
        {
            WinGame();
        }
    }

    void WinGame()
    {
        isWon = true;
        if (winPanel != null) winPanel.SetActive(true);
        if (anim != null) anim.SetTrigger("happy");
        if (move != null) move.enabled = false; // Disable player input after winning
    }

    void ResetToSpawn()
    {
        isWon = false;
        if (winPanel != null) winPanel.SetActive(false);
        if (anim != null) anim.SetTrigger("normal");

        // 1. Teleport the player to the spawn point by temporarily disabling the CharacterController
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = playerSpawn;
        if (cc != null) cc.enabled = true;
        if (move != null) move.enabled = true;

        // 2. Teleport the key and reset its physics state
        if (key != null)
        {
            Rigidbody rb = key.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Fully stop the key's linear and angular motion
                rb.velocity = Vector3.zero; 
                rb.angularVelocity = Vector3.zero; 
                
                // Ensure the key's rigidbody is back to a normal, simulated state
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            key.transform.position = keySpawn;
        }
    }
}