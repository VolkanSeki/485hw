using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject spawned;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Instantiate(spawned, new Vector3(0, 10, 0), Quaternion.identity);
        }
    }
}