using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KeyboardMusicControl : MonoBehaviour
{
    public AudioSource bgMusic; 

    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.M))
        {
            bgMusic.mute = !bgMusic.mute;
        }
    }
}