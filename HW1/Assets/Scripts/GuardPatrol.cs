using UnityEngine;
using System.Collections;

public class GuardPatrol : MonoBehaviour
{
    public enum Type { Enemy, Trap }

    [Header("Select Type")]
    public Type objectType;

    [Header("Patrol Settings (For Enemy)")]
    public Vector3 pointA;
    public Vector3 pointB;
    public float patrolSpeed = 3.0f;
    public float waitAtPoints = 1.0f;

    [Header("Trap Settings (For Trap)")]
    public Vector3 trapPosition;
    public float activeDuration = 2.0f;
    public float inactiveDuration = 2.0f;

    private CharacterSkinController skin;
    private MeshRenderer mesh;
    private Collider col;

    void Start()
    {
        mesh = GetComponent<MeshRenderer>();
        col = GetComponent<Collider>();
        skin = GetComponent<CharacterSkinController>();

        if (objectType == Type.Enemy)
        {
            // Düşmansa kızgın bak ve devriyeye başla
            if (skin != null) skin.eyeState = CharacterSkinController.EyePosition.angry;
            StartCoroutine(PatrolRoutine());
        }
        else if (objectType == Type.Trap)
        {
            // Tuzaksa belirlenen koordinata git ve açılıp kapanmaya başla
            transform.position = trapPosition;
            StartCoroutine(TrapRoutine());
        }
    }

    // --- ENEMY DEVRİYE MANTIĞI ---
    IEnumerator PatrolRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(MoveToTarget(pointB));
            yield return new WaitForSeconds(waitAtPoints);
            yield return StartCoroutine(MoveToTarget(pointA));
            yield return new WaitForSeconds(waitAtPoints);
        }
    }

    IEnumerator MoveToTarget(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, patrolSpeed * Time.deltaTime);
            Vector3 direction = (target - transform.position).normalized;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
            yield return null;
        }
        transform.position = target;
    }

    // --- TRAP PERİYODİK MANTIK ---
    IEnumerator TrapRoutine()
    {
        while (true)
        {
            // Tuzak Aktif
            if (mesh != null) mesh.enabled = true;
            if (col != null) col.enabled = true;
            yield return new WaitForSeconds(activeDuration);

            // Tuzak Pasif (Görünmez ve Çarpışmasız)
            if (mesh != null) mesh.enabled = false;
            if (col != null) col.enabled = false;
            yield return new WaitForSeconds(inactiveDuration);
        }
    }
}