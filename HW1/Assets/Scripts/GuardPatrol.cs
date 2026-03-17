using UnityEngine;
using System.Collections;

public class GuardPatrol : MonoBehaviour
{
    public enum Type { Enemy, Trap }

    [Header("Select Type")]
    public Type objectType;

    [Header("Common Settings")]
    public Vector3 manualPosition; // Müfettişten (Inspector) girilecek X, Y, Z

    [Header("Patrol Settings (For Enemy)")]
    public Vector3 pointA;
    public Vector3 pointB;
    public float patrolSpeed = 3.0f;

    [Header("Trap Settings (2s On / 4s Off)")]
    public float activeTime = 2.0f;
    public float inactiveTime = 4.0f;

    private MeshRenderer mesh;
    private Collider col;
    private CharacterSkinController skin;

    void Start()
    {
        mesh = GetComponent<MeshRenderer>();
        col = GetComponent<Collider>();
        skin = GetComponent<CharacterSkinController>();

        // Objenin yerini müfettişten girilen koordinata sabitle
        transform.position = manualPosition;

        if (objectType == Type.Enemy)
        {
            if (skin != null) skin.eyeState = CharacterSkinController.EyePosition.angry;
            StartCoroutine(PatrolRoutine());
        }
        else if (objectType == Type.Trap)
        {
            // ÖDEV ŞARTI: Periyodik çalışma Coroutine ile yapılıyor
            StartCoroutine(TrapCycleRoutine());
        }
    }

    // --- TUZAK DÖNGÜSÜ (2sn Açık / 4sn Kapalı) ---
    IEnumerator TrapCycleRoutine()
    {
        while (true)
        {
            // 1. TUZAK AKTİF (2 Saniye)
            if (mesh != null) mesh.enabled = true;
            if (col != null) col.enabled = true;
            yield return new WaitForSeconds(activeTime);

            // 2. TUZAK PASİF (4 Saniye)
            if (mesh != null) mesh.enabled = false;
            if (col != null) col.enabled = false;
            yield return new WaitForSeconds(inactiveTime);
        }
    }

    // --- DÜŞMAN DEVRİYE MANTIĞI ---
    IEnumerator PatrolRoutine()
    {
        while (true)
        {
            yield return StartCoroutine(MoveToTarget(pointB));
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(MoveToTarget(pointA));
            yield return new WaitForSeconds(1f);
        }
    }

    IEnumerator MoveToTarget(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, patrolSpeed * Time.deltaTime);
            Vector3 dir = (target - transform.position).normalized;
            if (dir != Vector3.zero) 
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            yield return null;
        }
    }
}