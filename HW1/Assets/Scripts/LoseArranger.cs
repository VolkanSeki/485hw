using UnityEngine;

public class LoseArranger : MonoBehaviour
{
    [Header("UI Settings")]
    public GameObject losePanel; 
    
    [Header("References")]
    public GameObject key; // Sahnedeki Key objesi image_0.png
    
    // image_14.png Koordinatlar
    private Vector3 playerSpawn = new Vector3(38f, 0.1f, 2f);
    // image_0.png Koordinatlar
    private Vector3 keySpawn = new Vector3(24f, 1f, 38f);
    
    private bool isLost = false;
    private MovementInput playerMove; // image_1.png Jammo'nun hareketi
    private Animator playerAnim;       // image_1.png Jammo'nun animasyonu

    void Start()
    {
        // Player (Jammo image_1.png) bileşenlerini al
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
        // Tag'i Enemy veya Trap olan bir şeye değersek
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
        
        // Jammo "dead" animasyonu
        if (playerAnim != null) playerAnim.SetTrigger("dead"); 
        
        // Kazanma anında olduğu gibi, kaybetme anında da karakter kontrolünü kapat
        if (playerMove != null) playerMove.enabled = false;    
    }

    void ResetToSpawn()
    {
        isLost = false;
        if (losePanel != null) losePanel.SetActive(false);
        if (playerAnim != null) playerAnim.SetTrigger("normal");

        // 1. Player Reset (Character Controller kapatılarak ışınlanır)
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        
        transform.position = playerSpawn;

        if (cc != null) cc.enabled = true;
        if (playerMove != null) playerMove.enabled = true;

        // 2. Key Reset (Burayı Dikkatle Kontrol Et)
        if (key != null)
        {
            // YENİ VE KRİTİK ADIM:
            // Anahtarın üzerindeki CarryMode scriptini bulup 'Dur!' talimatı gönderiyoruz.
            // Bu, takip Coroutine'ini anında durdurur.
            CarryMode carryScript = key.GetComponent<CarryMode>();
            if (carryScript != null)
            {
                carryScript.StopCarrying(); // Takibi durdur!
                Debug.Log("CarryMode takibi durduruldu.");
            }

            Rigidbody rb = key.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Fiziksel hareketleri tamamen sıfırla
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                
                // StopCarrying zaten fiziği normale döndürüyor, emin olmak için tekrarlayalım
                rb.isKinematic = false; 
                rb.useGravity = true;
            }

            // Anahtarı koordinatlarına ışınla
            key.transform.position = keySpawn;
            Debug.Log("Anahtar ışınlandı: " + keySpawn);
        }
        else
        {
            Debug.LogError("HATA: LoseArranger içindeki 'Key' kutucuğu boş! Hierarchy'den anahtarı sürükle.");
        }
    }
}