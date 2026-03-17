using UnityEngine;

public class Winning : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject winPanel; // "winning.jpg" içeren RawImage nesnesi

    [Header("References")]
    public GameObject player; // Jammo karakteri
    public GameObject key;    // Anahtar (Key) objesi

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
        // Kazanma ekranı açıkken SPACE basılırsa her şeyi sıfırla
        if (isWon && Input.GetKeyDown(KeyCode.Space))
        {
            ResetToSpawn();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Anahtar kapıya değdiğinde
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
        if (move != null) move.enabled = false; // Karakteri durdur
    }

    void ResetToSpawn()
    {
        isWon = false;
        if (winPanel != null) winPanel.SetActive(false);
        if (anim != null) anim.SetTrigger("normal");

        // 1. Player Teleport (CharacterController kapatılarak yapılır)
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.transform.position = playerSpawn;
        if (cc != null) cc.enabled = true;
        if (move != null) move.enabled = true;

        // 2. Key Teleport ve Hız Sıfırlama
        if (key != null)
        {
            Rigidbody rb = key.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Fiziksel hareketleri tamamen durduruyoruz
                rb.velocity = Vector3.zero; 
                rb.angularVelocity = Vector3.zero; 
                
                // Eğer CarryMode scripti açıksa fizik ayarlarını normale döndür
                rb.isKinematic = false;
                rb.useGravity = true;
            }
            key.transform.position = keySpawn;
        }
    }
}