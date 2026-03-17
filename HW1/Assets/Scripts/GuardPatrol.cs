using UnityEngine;
using System.Collections;

public class GuardPatrol : MonoBehaviour
{
    public enum Type { Enemy, Trap }

    [Header("Select Type")]
    public Type objectType;

    [Header("Common Settings")]
    public Vector3 manualPosition; // World position set from the Inspector

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

        // Set initial position to the manually configured coordinates
        transform.position = manualPosition;

        if (objectType == Type.Enemy)
        {
            if (skin != null) skin.eyeState = CharacterSkinController.EyePosition.angry;
            StartCoroutine(PatrolRoutine());
        }
        else if (objectType == Type.Trap)
        {
            // Start trap cycle using a coroutine for periodic activation
            StartCoroutine(TrapCycleRoutine());
        }
    }

    // Trap cycle: 2 seconds active, 4 seconds inactive
    IEnumerator TrapCycleRoutine()
    {
        while (true)
        {
            // Phase 1: trap active for the configured active time
            if (mesh != null) mesh.enabled = true;
            if (col != null) col.enabled = true;
            yield return new WaitForSeconds(activeTime);

            // Phase 2: trap inactive for the configured inactive time
            if (mesh != null) mesh.enabled = false;
            if (col != null) col.enabled = false;
            yield return new WaitForSeconds(inactiveTime);
        }
    }

    // Enemy patrol loop between point A and point B
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