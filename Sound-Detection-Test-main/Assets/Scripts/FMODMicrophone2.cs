// Based on example from FMOD docs
//--------------------------------------------------------------------
//
// This is a Unity behavior script that demonstrates how to record
// continuously and play back the same data while keeping a specified
// latency between the two. This is achieved by delaying the start of
// playback until the specified number of milliseconds has been
// recorded. At runtime the playback speed will be slightly altered
// to compensate for any drift in either play or record drivers.
//
// Add this script to a Game Object in a Unity scene and play the
// Editor. Recording will start with the scene and playback will
// start after the defined latency.
//
// This document assumes familiarity with Unity scripting. See
// https://unity3d.com/learn/tutorials/topics/scripting for resources
// on learning Unity scripting.
//
//--------------------------------------------------------------------

using FMOD;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

public class FMODMicrophone2 : MonoBehaviour
{
    private uint LATENCY_MS = 25;
    private uint DRIFT_MS = 1;

    private uint samplesRecorded, samplesPlayed = 0;
    private int nativeRate, nativeChannels = 0;
    private uint recSoundLength = 0;
    private uint recSoundLengthBytes = 0;
    uint lastPlayPos = 0;
    uint lastRecordPos = 0;
    uint lastPlayPosBytes = 0;
    private uint driftThreshold = 0;
    private uint desiredLatency = 0;
    private uint adjustLatency = 0;
    private int actualLatency = 0;

    private FMOD.CREATESOUNDEXINFO exInfo = new FMOD.CREATESOUNDEXINFO();

    private FMOD.Sound recSound;
    private FMOD.Channel channel;

    public Classification classifier;
    public List<float> yamnetBuffer = new List<float>();
    public AudioClip micClip;
    public AudioSource tempListener;
    private bool _audition = false;
    public bool Audition { get { return _audition; } set { _audition = value; UpdateAudition(); } }

    // Start is called before the first frame update
    void Start()
    {
        /*
            Determine latency in samples.
        */
        FMODUnity.RuntimeManager.CoreSystem.getRecordDriverInfo(0, out _, 0, out _, out nativeRate, out _, out nativeChannels, out _);

        driftThreshold = (uint)(nativeRate * DRIFT_MS) / 1000;
        desiredLatency = (uint)(nativeRate * LATENCY_MS) / 1000;
        adjustLatency = desiredLatency;
        actualLatency = (int)desiredLatency;

        /*
            Create user sound to record into, then start recording.
        */
        exInfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exInfo.numchannels = nativeChannels;
        //exInfo.numchannels = 1;
        //exInfo.format = FMOD.SOUND_FORMAT.PCM16;
        exInfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;
        exInfo.defaultfrequency = nativeRate;        
        exInfo.length = (uint)(nativeRate * sizeof(short) * nativeChannels);

        FMODUnity.RuntimeManager.CoreSystem.createSound("", FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref exInfo, out recSound);

        FMODUnity.RuntimeManager.CoreSystem.recordStart(0, recSound, true);

        recSound.getLength(out recSoundLength, FMOD.TIMEUNIT.PCM);
        recSound.getLength(out recSoundLengthBytes, FMOD.TIMEUNIT.PCMBYTES);

        yamnetBuffer = new List<float>(new float[96000]);
        StartCoroutine(ClassificationTimer());
    }

    // Update is called once per frame
    void Update()
    {
        /*
            Determine how much has been recorded since we last checked
        */
        uint recordPos = 0;
        FMODUnity.RuntimeManager.CoreSystem.getRecordPosition(0, out recordPos);

        uint recordDelta = (recordPos >= lastRecordPos) ? (recordPos - lastRecordPos) : (recordPos + recSoundLength - lastRecordPos);
        lastRecordPos = recordPos;
        samplesRecorded += recordDelta;

        

        uint minRecordDelta = 0;
        if (recordDelta != 0 && (recordDelta < minRecordDelta))
        {
            minRecordDelta = recordDelta; // Smallest driver granularity seen so far
            adjustLatency = (recordDelta <= desiredLatency) ? desiredLatency : recordDelta; // Adjust our latency if driver granularity is high
        }

        /*
            Delay playback until our desired latency is reached.
        */
        if (!channel.hasHandle() && samplesRecorded >= adjustLatency)
        {
            FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup mCG);
            FMODUnity.RuntimeManager.CoreSystem.playSound(recSound, mCG, false, out channel);
        }

        /*
            Determine how much has been played since we last checked.
        */
        if (channel.hasHandle())
        {
            uint playPos = 0;
            channel.getPosition(out playPos, FMOD.TIMEUNIT.PCM);

            uint playDelta = (playPos >= lastPlayPos) ? (playPos - lastPlayPos) : (playPos + recSoundLength - lastPlayPos);
            lastPlayPos = playPos;
            samplesPlayed += playDelta;

            uint playPosBytes = 0;
            channel.getPosition(out playPosBytes, FMOD.TIMEUNIT.PCMBYTES);
            uint playPosBytesDelta = (playPosBytes >= lastPlayPosBytes) ? (playPosBytes - lastPlayPosBytes) : (playPosBytes + recSoundLengthBytes - lastPlayPosBytes);

            // Compensate for any drift.
            int latency = (int)(samplesRecorded - samplesPlayed);
            actualLatency = (int)((0.97f * actualLatency) + (0.03f * latency));

            int playbackRate = nativeRate;
            if (actualLatency < (int)(adjustLatency - driftThreshold))
            {
                // Playback position is catching up to the record position, slow playback down by 2%
                playbackRate = nativeRate - (nativeRate / 50);
            }

            else if (actualLatency > (int)(adjustLatency + driftThreshold))
            {
                // Playback is falling behind the record position, speed playback up by 2%
                playbackRate = nativeRate + (nativeRate / 50);
            }

            channel.setFrequency((float)playbackRate);
            UpdateAudition();



            IntPtr ptr1, ptr2;
            uint len1, len2;
            //uint bytesRead = (uint)(recordDelta * 32 * exInfo.numchannels) / 8;
            //uint bytesRead = (uint)(recordDelta / sizeof(float));
            uint bytesRead = playPosBytesDelta;
            recSound.@lock(lastRecordPos, bytesRead, out ptr1, out ptr2, out len1, out len2);

            int sampleLen1 = (int)(len1 / sizeof(float));
            int sampleLen2 = (int)(len2 / sizeof(float));
            int samplesRead = sampleLen1 + sampleLen2;
            float[] tempBuffer = new float[samplesRead];
            

            if (len1 > 0)
            {
                //UnityEngine.Debug.Log("Copying len1");
                Marshal.Copy(ptr1, tempBuffer, 0, sampleLen1);
            }
            if (len2 > 0)
            {
                //UnityEngine.Debug.Log("Copying len2");
                Marshal.Copy(ptr2, tempBuffer, sampleLen1, sampleLen2);
            }

            

            if (samplesRead > 0)
            {
                // UnityEngine.Debug.Log(tempBuffer.Min()); PCMFloat format outputs between -1 and 1f.
                yamnetBuffer.AddRange(tempBuffer);
                if (yamnetBuffer.Count > nativeRate * nativeChannels)
                {
                    yamnetBuffer.RemoveRange(0, tempBuffer.Length);
                }
            }
            

            recSound.unlock(ptr1, ptr2, len1, len2);

            

        }
    }

    private IEnumerator ClassificationTimer()
    {
        while (true)
        {
            if (channel.hasHandle() && classifier != null)
            {
                //micClip = AudioClip.Create("mic", nativeRate, nativeChannels, nativeRate, false); // Create clip to hold PCM values
                float[] clipBuffer = new float[nativeRate];

                float[] tempYamnet = yamnetBuffer.ToArray();

                
                Array.Copy(tempYamnet, clipBuffer, clipBuffer.Length);
                /*
                micClip.SetData(clipBuffer, 0);

                classifier.PredictAudioFile(micClip);
                */

                classifier.PredictAudioFile(yamnetBuffer.ToArray(), exInfo.defaultfrequency, exInfo.numchannels, 1f);
            }

            if (!tempListener.isPlaying)
            {
                tempListener.clip = micClip;
                tempListener.Play();
                
            }

            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(0.5f);
        }
    }

    private void OnDestroy()
    {
        recSound.release();
    }

    private void UpdateAudition()
    {
        if (_audition == true)
        {
            channel.setVolume(1);
        }
        else
        {
            channel.setVolume(0);
        }
    }
}