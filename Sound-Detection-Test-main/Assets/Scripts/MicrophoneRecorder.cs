using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MicrophoneRecorder : MonoBehaviour
{

    public int sampleRate;
    public int maxSampleRate;
    private int bufferLengthSeconds = 1;
    AudioSource audioSource;
    public AudioClip microphoneClip;

    public Classification classifier;

    public int numPredictionsPerSecond = 1;

    private string microphone = null;



    // Start is called before the first frame update
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        
        

        StartRecording();

        StartCoroutine(MicPredictionTimer());
    }

    private IEnumerator MicPredictionTimer()
    {
        while (true)
        {
            if (classifier != null)
            {
                classifier.PredictAudioFile(microphoneClip);
            }
            //Debug.Log(microphoneClip.samples);
            //Debug.Log(Microphone.GetPosition(null));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds((float)bufferLengthSeconds / numPredictionsPerSecond); // The weirdness was happening because not everything was specced as a float, hence it was waiting for 0 seconds and just running things as fast as humanly possible.

            // !!!
            // Right now this is extremely crude, because it's relying on Unity's rolling buffer. There's probably a better way of doing this, but it actually works pretty well, so...
        }
    }

    void StartRecording()
    {
        Microphone.GetDeviceCaps(microphone, out sampleRate, out maxSampleRate);
        microphoneClip = Microphone.Start(microphone, true, bufferLengthSeconds, sampleRate);
    }

    public void ChangeMicrophone(string newMicrophone)
    {
        Microphone.End(microphone);
        microphone = newMicrophone;
        StartRecording();
    }

    public void AuditionMicrophone(bool audition)
    {
        if (audioSource != null)
        {
            if (audition)
            {
                audioSource.clip = microphoneClip;
                audioSource.timeSamples = Microphone.GetPosition(microphone);
                audioSource.loop = true;
                while (!(Microphone.GetPosition(microphone) > 32)) { } // This is suggested in the docs to control for latency. It's effectively the gap between the read and write head in samples.
                audioSource.Play();
            }
            else
            {
                audioSource.Stop();
            }
        }
    }
}
