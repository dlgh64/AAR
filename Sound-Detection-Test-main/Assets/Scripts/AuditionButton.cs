using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AuditionButton : MonoBehaviour
{
    public MicrophoneRecorder recorder;
    public FMODMicrophone2 fmodRecorder;
    private Toggle toggle;

    private void Awake()
    {
        toggle = GetComponent<Toggle>();
        SetAudition();
    }
    public void SetAudition()
    {
        if (recorder != null)
        {
            recorder.AuditionMicrophone(toggle.isOn);
        }
        if (fmodRecorder != null)
        {
            fmodRecorder.Audition = toggle.isOn;
        }
    }
}
