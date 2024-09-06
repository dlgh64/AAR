// # Wholesale script from FMOD forums: https://qa.fmod.com/t/accessing-real-time-audio-samples-of-incoming-mic-capture-via-dspcallback/18467

// The CustomDSPCallback code was adapted from: https://fmod.com/resources/documentation-unity?version=2.02&page=examples-dsp-capture.html


using System;
using FMODUnity;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Generic;

public class FMODMicDSP : MonoBehaviour
{
    //public variables
    [Header("Capture Device details")]
    public int captureDeviceIndex = 0;
    [TextArea] public string captureDeviceName = null;


    FMOD.CREATESOUNDEXINFO exinfo;

    public Classification classifier;

    // Custom DSPCallback variables 
    private FMOD.DSP_READ_CALLBACK mReadCallback;
    private FMOD.DSP mCaptureDSP;
    public float[] mDataBuffer;
    private GCHandle mObjHandle;
    private uint mBufferLength;
    private uint soundLength;
    int captureSrate;
    const int DRIFT_MS = 1;
    const int LATENCY_MS = 50;
    uint driftThreshold;
    uint desiredLatency;
    uint adjustedLatency;
    uint actualLatency;
    uint lastRecordPos = 0;
    uint samplesRecorded = 0;
    uint samplesPlayed = 0;
    uint minRecordDelta = (uint)uint.MaxValue;
    uint lastPlayPos = 0;

    bool recordingStarted = false;

    FMOD.ChannelGroup masterCG;
    FMOD.Channel channel;
    FMOD.Sound sound;

    //public List<float> yamnetBuffer;
    public float[] yamnetBuffer;
    //private int yamnetBufferSize = 96000;


    [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
    static FMOD.RESULT CaptureDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));

        IntPtr userData;
        functions.getuserdata(ref dsp_state, out userData);

        GCHandle objHandle = GCHandle.FromIntPtr(userData);
        FMODMicDSP obj = objHandle.Target as FMODMicDSP;

        //Debug.Log("inchannels:" + inchannels);
        //Debug.Log("outchannels:" + outchannels);

        // Copy the incoming buffer to process later
        int lengthElements = (int)length * inchannels;
        Marshal.Copy(inbuffer, obj.mDataBuffer, 0, lengthElements);
        Marshal.Copy(inbuffer, obj.yamnetBuffer, 0, lengthElements);

        // Copy the inbuffer to the outbuffer so we can still hear it
        Marshal.Copy(obj.mDataBuffer, 0, outbuffer, lengthElements);

        // obj.yamnetBuffer.AddRange(obj.mDataBuffer);

        return FMOD.RESULT.OK;
    }

    // Start is called before the first frame update
    void Start()
    {

        // how many capture devices are plugged in for us to use.
        int numOfDriversConnected;
        int numofDrivers;
        FMOD.RESULT res = RuntimeManager.CoreSystem.getRecordNumDrivers(out numofDrivers, out numOfDriversConnected);

        if (res != FMOD.RESULT.OK)
        {
            Debug.Log("Failed to retrieve driver details: " + res);
            return;
        }

        if (numOfDriversConnected == 0)
        {
            Debug.Log("No capture devices detected!");
            return;
        }
        else
            Debug.Log("You have " + numOfDriversConnected + " capture devices available to record with.");


        // info about the device we're recording with.
        System.Guid micGUID;
        FMOD.DRIVER_STATE driverState;
        FMOD.SPEAKERMODE speakerMode;
        int captureNumChannels;
        RuntimeManager.CoreSystem.getRecordDriverInfo(captureDeviceIndex, out captureDeviceName, 50,
            out micGUID, out captureSrate, out speakerMode, out captureNumChannels, out driverState);

        driftThreshold = (uint)(captureSrate * DRIFT_MS) / 1000;       /* The point where we start compensating for drift */
        desiredLatency = (uint)(captureSrate * LATENCY_MS) / 1000;     /* User specified latency */
        adjustedLatency = (uint)desiredLatency;                      /* User specified latency adjusted for driver update granularity */
        actualLatency = (uint)desiredLatency;                                 /* Latency measured once playback begins (smoothened for jitter) */


        Debug.Log("captureNumChannels of capture device: " + captureNumChannels);
        Debug.Log("captureSrate: " + captureSrate);


        // create sound where capture is recorded
        exinfo.cbsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = captureNumChannels;
        //exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;
        exinfo.defaultfrequency = captureSrate;
        exinfo.length = (uint)captureSrate * sizeof(short) * (uint)captureNumChannels;
        Debug.Log("Exinfo.Length: "+exinfo.length+" bytes");
        RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER,
            ref exinfo, out sound);

       

        // start recording    
        RuntimeManager.CoreSystem.recordStart(captureDeviceIndex, sound, true);


        sound.getLength(out soundLength, FMOD.TIMEUNIT.PCM);

        // play sound on dedicated channel in master channel group

        if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) != FMOD.RESULT.OK)
            Debug.LogWarningFormat("FMOD: Unable to create a master channel group: masterCG");

        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG);
        RuntimeManager.CoreSystem.playSound(sound, masterCG, true, out channel);
        channel.setPaused(true);

        // Assign the callback to a member variable to avoid garbage collection
        mReadCallback = CaptureDSPReadCallback;

        // Allocate a data buffer large enough for 8 channels, pin the memory to avoid garbage collection
        uint bufferLength;
        int numBuffers;
        FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out bufferLength, out numBuffers);
        mDataBuffer = new float[bufferLength * 8];
        mBufferLength = bufferLength;

        yamnetBuffer = new float[exinfo.defaultfrequency * exinfo.numchannels];

        

        // Tentatively changed buffer length by calling setDSPBufferSize in file Assets/Plugins/FMOD/src/RuntimeManager.cs	
        // Tentatively changed buffer length by calling setDSPBufferSize in file Assets/Plugins/FMOD/src/fmod.cs - line 1150

        //Debug.Log("buffer length:" + bufferLength);

        // Get a handle to this object to pass into the callback
        mObjHandle = GCHandle.Alloc(this);
        if (mObjHandle != null)
        {
            // Define a basic DSP that receives a callback each mix to capture audio
            FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
            desc.numinputbuffers = 2;
            desc.numoutputbuffers = 2;
            desc.read = mReadCallback;
            desc.userdata = GCHandle.ToIntPtr(mObjHandle);

            // Create an instance of the capture DSP and attach it to the master channel group to capture all audio            
            if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref desc, out mCaptureDSP) == FMOD.RESULT.OK)
            {
                if (masterCG.addDSP(0, mCaptureDSP) != FMOD.RESULT.OK)
                {
                    Debug.LogWarningFormat("FMOD: Unable to add mCaptureDSP to the master channel group");
                }
            }
            else
            {
                Debug.LogWarningFormat("FMOD: Unable to create a DSP: mCaptureDSP");
            }
        }
        else
        {
            Debug.LogWarningFormat("FMOD: Unable to create a GCHandle: mObjHandle");
        }

        StartCoroutine(MicPredictionTimer());
    }

    private void FixedUpdate()
    {
        RuntimeManager.CoreSystem.getRecordPosition(captureDeviceIndex, out uint recordPos);

        uint recordDelta = (recordPos >= lastRecordPos) ? (recordPos - lastRecordPos) : (recordPos + soundLength - lastRecordPos);
        lastRecordPos = recordPos;
        samplesRecorded += recordDelta;

        if (recordDelta != 0 && (recordDelta < minRecordDelta))
        {
            minRecordDelta = recordDelta; /* Smallest driver granularity seen so far */
            adjustedLatency = (recordDelta <= desiredLatency) ? desiredLatency : recordDelta; /* Adjust our latency if driver granularity is high */
        }

        if (!recordingStarted)
        {
            if (samplesRecorded >= adjustedLatency)
            {
                channel.setPaused(false);
                recordingStarted = true;
            }
        }

        /*
            Delay playback until our desired latency is reached.
        */
        if (recordingStarted)
        {
            sound.@lock(recordPos, soundLength, out IntPtr one, out IntPtr two, out uint lone, out uint ltwo);
            int lengthElements = (int)soundLength * 1;
            //Marshal.Copy(one, mDataBuffer, 0, lengthElements);
            
            // Debug.Log("Marshal length: " + lengthElements);
            // Marshal.Copy(one, yamnetBuffer, 0, lengthElements * exinfo.numchannels); // Copy to buffer. lengthElements is the length of the sound in samples, however each 'sample' includes all channels.
            
            /*
                Stop playback if recording stops.
            */
            RuntimeManager.CoreSystem.isRecording(captureDeviceIndex, out bool isRecording);

            if (!isRecording)
            {
                channel.setPaused(true);
            }

            /*
                Determine how much has been played since we last checked.
            */
            channel.getPosition(out uint playPos, FMOD.TIMEUNIT.PCM);

            uint playDelta = (playPos >= lastPlayPos) ? (playPos - lastPlayPos) : (playPos + soundLength - lastPlayPos);
            lastPlayPos = playPos;
            samplesPlayed += playDelta;

            /*
                Compensate for any drift.
            */
            uint latency = samplesRecorded - samplesPlayed;
            actualLatency = (uint)((0.97f * actualLatency) + (0.03f * latency));

            int playbackRate = captureSrate;
            if (actualLatency < (int)(adjustedLatency - driftThreshold))
            {
                /* Play position is catching up to the record position, slow playback down by 2% */
                playbackRate = captureSrate - (captureSrate / 50);
            }
            else if (actualLatency > (int)(adjustedLatency + driftThreshold))
            {
                /* Play position is falling behind the record position, speed playback up by 2% */
                playbackRate = captureSrate + (captureSrate / 50);
            }

            channel.setFrequency((float)playbackRate);
        }

        //CycleYamnetBuffer((int)samplesRecorded);

    }

    private void CycleYamnetBuffer(int samplesRecorded)
    {
        /*
        if (yamnetBuffer.Count > yamnetBufferSize)
        {
            int margin = yamnetBuffer.Count - yamnetBufferSize;
            yamnetBuffer.RemoveRange(0, margin); // Remove the overshoot
        }
        */
    }
    
    private System.Collections.IEnumerator MicPredictionTimer()
    {
        int yamnetBufferFullSize = exinfo.defaultfrequency * exinfo.numchannels;
        int bufferLengthSeconds = 1;
        int numPredictionsPerSecond = 2;
        while (true)
        {
            
            if (classifier != null && yamnetBuffer.Length > 0)
            {
                AudioClip microphoneClip = AudioClip.Create("mic", exinfo.defaultfrequency, exinfo.numchannels, exinfo.defaultfrequency, false);

                IntPtr one, two;
                uint lone, ltwo;
                sound.@lock(0, soundLength, out one, out two, out lone, out ltwo);

                // https://fmod.com/docs/2.02/unity/examples-video-playback.html
                int sampleLen1 = (int)(lone / sizeof(float));
                int sampleLen2 = (int)(ltwo / sizeof(float));

                if (lone > 0)
                {
                    Marshal.Copy(one, yamnetBuffer, 0, sampleLen1); // Copy to buffer.
                }
                if (ltwo > 0)
                {
                    Marshal.Copy(two, yamnetBuffer, 0, sampleLen2);
                }

                
                
                microphoneClip.SetData(yamnetBuffer, 0);

                if (microphoneClip != null)
                {
                   classifier.PredictAudioFile(microphoneClip);
                }
                sound.unlock(one, two, lone, ltwo);
            }


            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds((float)bufferLengthSeconds / numPredictionsPerSecond); // The weirdness was happening because not everything was specced as a float, hence it was waiting for 0 seconds and just running things as fast as humanly possible.

            // !!!
            // Right now this is extremely crude, because it's relying on Unity's rolling buffer. There's probably a better way of doing this, but it actually works pretty well, so...
            

        }
    }
    
}