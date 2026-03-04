using UnityEngine;

public class SesKontrol : MonoBehaviour
{
    public AudioSource source; 

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (source.isPlaying)
            {
                source.Pause(); 
                Debug.Log("Müzik Durdu");
            }
            else
            {
                source.UnPause(); 
                Debug.Log("Müzik Devam Ediyor");
            }
        }
    }
}