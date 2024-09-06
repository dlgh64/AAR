using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMOD;
using FMODUnity;
using System.Runtime.InteropServices;

public class FMODMicrophone : MonoBehaviour
{
    private FMOD.System fmodSystem;
    private FMOD.RESULT result;
    private int maxChannels = 4;
    private DriverData driverData = new DriverData();
    private List<FMOD.Channel> fmodChannels = new List<FMOD.Channel>();

    public string selectedMicrophone = "Microphone Array (Realtek(R) Audio)";




    // Start is called before the first frame update
    void Start()
    {
        // https://qa.fmod.com/t/mic-state-when-recording/15977/6

        // https://qa.fmod.com/t/get-data-from-4-microphones-audio-interface-with-4-channels/14736

        result = FMOD.Factory.System_Create(out fmodSystem);
        PrintResult(result);
        result = fmodSystem.init(maxChannels, FMOD.INITFLAGS.NORMAL, System.IntPtr.Zero);
        PrintResult(result);

        int numInputDevices;
        int numInputDevicesConnected;

        result = fmodSystem.getRecordNumDrivers(out numInputDevices, out numInputDevicesConnected);
        PrintResult(result);
        UnityEngine.Debug.Log(numInputDevices + ", " + numInputDevicesConnected);

        for (int i = 0; i < numInputDevices; i++)
        {
            // Fetch all audio drivers.
            result = fmodSystem.getRecordDriverInfo(i, out driverData.driverName, driverData.driverNameLength, out driverData.guid, out driverData.systemRate, out driverData.speakerMode, out driverData.speakerModeChannels, out driverData.driverState);
            driverData.driverID = i;

            if (driverData.driverName == selectedMicrophone)
            {
                UnityEngine.Debug.Log(
                    "NAME: <color=red>" + driverData.driverName + "</color>" +
                    " | RATE: <color=red>" + driverData.systemRate + "</color>" +
                    " | MODE: <color=red>" + driverData.speakerMode + "</color>" +
                    " | CHANNELS: <color=red>" + driverData.speakerModeChannels + "</color>" +
                    " | STATE: <color=red>" + driverData.driverState + "</color>"
                );
                fmodSystem.setDriver(i); // Select the chosen driver if it's the same as the string set by user.
                break;
            }
        }

        // MicrophonesListener() function in above link.

        FMOD.ChannelGroup channelGroup = new FMOD.ChannelGroup();
        fmodSystem.createChannelGroup("microphoneSignal", out channelGroup);
        
        for (int i = 0; i < driverData.speakerModeChannels; i++)
        {
            FMOD.Sound fmodSound;
            FMOD.Channel fmodChannel;
            FMOD.CREATESOUNDEXINFO exinfo = new CREATESOUNDEXINFO();

            exinfo.cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO));
            exinfo.numchannels = driverData.speakerModeChannels;
            exinfo.format = FMOD.SOUND_FORMAT.PCM16;
            exinfo.defaultfrequency = driverData.systemRate; // For lowest latency set the FMOD::Sound sample rate to the rate returned by System::getRecordDriverInfo, otherwise a resampler will be allocated to handle the difference in frequencies, which adds latency. https://www.fmod.com/docs/2.00/api/core-api-system.html#system_recordstart
            exinfo.length = (uint)(exinfo.defaultfrequency * exinfo.numchannels * sizeof(ushort) * 0.5);
            exinfo.channelorder = FMOD.CHANNELORDER.ALLMONO;

            result = fmodSystem.createSound("Microphone_" + i, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref exinfo, out fmodSound);
            PrintResult(result);
            result = fmodSystem.recordStart(i, fmodSound, true);
            PrintResult(result);



            result = fmodSystem.playSound(fmodSound, channelGroup, false, out fmodChannel);
            fmodChannel.setVolume(0.5f);
            PrintResult(result);
            fmodChannel.setPaused(true); // Don't immediately play
            fmodChannels.Add(fmodChannel);

            StartCoroutine(PlayMic(fmodChannel));
        }

        int numChannels = 0;
        channelGroup.getNumChannels(out numChannels);
        UnityEngine.Debug.Log(numChannels);

        int desiredLatencyMs = 50;
        uint latency = (uint)(driverData.systemRate * desiredLatencyMs) / 1000;

    }

    private IEnumerator PlayMic(FMOD.Channel channel)
    {
        yield return new WaitForSeconds(0.05f);
        channel.setPaused(false);

    }

    void PrintResult(FMOD.RESULT result)
    {
        UnityEngine.Debug.Log(result.ToString());
    }

    private void OnDestroy()
    {
        fmodSystem.recordStop(driverData.driverID); // This is just a crude thing to stop the mic continuing to play after play is quit.
    }

}

public class DriverData
{
    // Taken from https://qa.fmod.com/t/get-data-from-4-microphones-audio-interface-with-4-channels/14736
    public string driverName;
    public int driverNameLength = 256;
    public System.Guid guid;
    public int systemRate;
    public FMOD.SPEAKERMODE speakerMode;
    public int speakerModeChannels;
    public FMOD.DRIVER_STATE driverState;

    public int driverID; // Jake addition
}
