using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class MicrophoneSelect : MonoBehaviour
{
    private TMP_Dropdown menu;
    public MicrophoneRecorder recorder;

    private void Start()
    {
        menu = GetComponent<TMP_Dropdown>();
        UpdateList();
    }
    public void UpdateList()
    {
        menu.options.Clear();

        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            menu.options.Add(new TMP_Dropdown.OptionData(Microphone.devices[i]));
        }
    }

    public void SelectNewMicrophone()
    {
        recorder.ChangeMicrophone(Microphone.devices[menu.value]);
    }
}
