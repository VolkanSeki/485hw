using UnityEngine;
using System.Collections;

public class CarryMode : MonoBehaviour
{
    [Header("Settings")]
    public float carryDistance = 0.6f; // Jammo'ya yakın durması için
    public KeyCode carryKey = KeyCode.E; 
    
    [Header("References")]
    public Transform playerTransform; 
    
    private Rigidbody rb;
    private Collider keyCollider;
    private bool isCarrying = false;
    private Coroutine carryCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        keyCollider = GetComponent<Collider>();
    }

    void Update()
    {
        if (playerTransform == null) return;

        // Mesafe kontrolü
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (Input.GetKeyDown(carryKey))
        {
            if (!isCarrying && distanceToPlayer < 2.0f)
            {
                StartCarrying();
            }
            else if (isCarrying)
            {
                StopCarrying();
            }
        }
    }

    void StartCarrying()
    {
        isCarrying = true;
        
        // Fırlama ve uçma sorununu çözen kısımlar:
        rb.useGravity = false; 
        rb.isKinematic = true; 

        // 1. Oyuncu ve Anahtar arasındaki çarpışmayı görmezden gel
        Collider playerCollider = playerTransform.GetComponent<Collider>();
        if (playerCollider != null && keyCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, keyCollider, true);
        }
        
        // Ödev Şartı: Periyodik takip işlemi Coroutine ile yapılıyor
        carryCoroutine = StartCoroutine(FollowPlayerRoutine());
    }

    void StopCarrying()
    {
        isCarrying = false;
        rb.useGravity = true;
        rb.isKinematic = false;

        // 2. Çarpışmayı tekrar aktif et
        Collider playerCollider = playerTransform.GetComponent<Collider>();
        if (playerCollider != null && keyCollider != null)
        {
            Physics.IgnoreCollision(playerCollider, keyCollider, false);
        }
        
        if (carryCoroutine != null)
            StopCoroutine(carryCoroutine);
    }

    IEnumerator FollowPlayerRoutine()
    {
        while (isCarrying)
        {
            // Jammo'nun önündeki konumu hesapla
            Vector3 targetPosition = playerTransform.position + playerTransform.forward * carryDistance;
            
            // Anahtarın yere gömülmemesi için hafif yukarı kaldır (Y ekseni)
            targetPosition.y = playerTransform.position.y + 0.5f; 

            // Pozisyonu ve rotasyonu güncelle
            transform.position = targetPosition;
            transform.rotation = playerTransform.rotation;

            yield return null; 
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Taşıma modundayken engele (duvara vb.) çarparsa otomatik bırak
        if (isCarrying && !collision.gameObject.CompareTag("Player"))
        {
            StopCarrying();
        }
    }
}