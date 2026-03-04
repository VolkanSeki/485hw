using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform Target; 
    public Vector3 distance = new Vector3(0, 5, -10); 

    void LateUpdate() 
    {
        if (Target != null)
        {
            transform.position = Target.position + distance;
            
            transform.LookAt(Target);
        }
    }
}