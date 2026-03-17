using UnityEngine;
using System.Collections;

public class CarryMode : MonoBehaviour
{
    [Header("Settings")]
    public float carryDistance = 0.5f; // Jammo'ya yakın durması için
    public KeyCode carryKey = KeyCode.E; 
    
    [Header("References")]
    public Transform playerTransform; // Inspector'dan atandığından emin ol
    
    private Rigidbody rb;
    private Collider keyCollider; // Anahtarın çarpışma kutusu
    private bool isCarrying = false;
    private Coroutine carryCoroutine;

    void Start() 
    { 
        rb = GetComponent<Rigidbody>(); 
        keyCollider = GetComponent<Collider>(); // Collider bileşenini al
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

        // FİZİĞİ DONDUR VE HAREKETİ SIFIRLA
        rb.isKinematic = true; 
        rb.useGravity = false; 
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // %100 ÇÖZÜM: TAŞIRKEN ÇARPIŞMAYI TAMAMEN KAPAT
        if (keyCollider != null)
        {
            keyCollider.enabled = false; // Çarpışma kutusunu devre dışı bırak
        }

        // ÖDEV ŞARTI: Takip işlemi Coroutine ile yapılıyor
        carryCoroutine = StartCoroutine(FollowPlayerRoutine());
    }

    // BU FONKSİYON PUBLIC OLMALI (LoseArranger buradan erişecek)
    public void StopCarrying()
    {
        isCarrying = false;

        // FİZİĞİ VE GRAVİTASYONU GERİ AÇ
        if (rb != null) 
        { 
            rb.isKinematic = false; 
            rb.useGravity = true; 
        }
        
        // ÇARPIŞMAYI GERİ AÇ (Böylece yere düşebilir, kapıya değebilir)
        if (keyCollider != null)
        {
            keyCollider.enabled = true; // Çarpışma kutusunu tekrar aç
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
            // Jammo'nun önündeki konumu hesapla
            transform.position = playerTransform.position + playerTransform.forward * carryDistance + Vector3.up * 0.4f;
            transform.rotation = playerTransform.rotation;
            yield return null; // Bir sonraki kareyi bekle
        }
    }
}