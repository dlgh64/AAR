using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Barracuda;
using Unity.VisualScripting;
using UnityEngine.Events;
using System.IO;
using System;
using YamNetUnity;
using TMPro; // For text display on screen.




public class Classification : MonoBehaviour
{
    /* Needs to:
        - take an audio input
        - Preprocess it to suit the model
        - Feed it to the model
        - Identify the name of the class the model returns
        - Return the class and confidence score.
    */

    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************

    public AudioClip dogBarkingClip; // ���ڴ洢�������� AudioClip   AudioClip for storing dog barking sounds

    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************



    public AudioClip inputClip;
    public NNModel modelFile;
    private Model model;
    private IWorker worker;
    private int numModelClasses = -1; // Used to cache the number of classes the model outputs. Negligible performance gain over checking with each prediction.

    private string[] classMap;

    [Header("Classifications")]
    public List<YamnetClassification> classificationList;
    public List<YamnetClassification> shortTermClassificationList;
    public List<YamnetMatrix> matrices;

    [Header("Display")]
    public TMP_Text className;
    public TMP_Text confidenceScore;
    public TMP_Text shortClassName;
    public TMP_Text shortConfidenceScore;

    [Header("Frame Averaging")]
    [Tooltip("Number of predictions to block together and take the majority vote from.")]
    public int frameSequenceLength = 4;
    private List<Tensor> frameFeatures = new();

    [Header("Logging")]
    [Tooltip("Logging interval in seconds")]
    public bool logEnabled = false;
    public int logInterval = 300;



    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************

    //[Header("Audio Source Direction")]
    //public Transform soundSourceTransform;  // The Transform of the real-time sound source should be synchronized with the real-time position of the sound source.

    //���� 1: ����ȫ�ֱ���  define public varibles
    //���������ж���һЩ�������洢����˷������ж�ȡ�������������ݡ�������Ҫ����һ��������������Դ��������Ʒ���
    //Define some variables in your class to store the left and right channel data read from the microphone input. You also need to define a variable to track the closest estimated direction of the sound source.
    [Header("Audio Source Localization")]
    public AudioSource audioSource;  // This should be assigned in the Unity editor
    public float directionThreshold = 0.1f;  // Threshold to determine significant direction change
    private float lastDirection = 0.0f;  // Last estimated direction
    public Transform soundSourceTransform;  // Transform that will track the sound source position

    //���� 2: ��ȡ��˷�����
    //�� Unity �У�������ʹ�� Microphone ����ֱ�ӷ�����˷����ݡ�����һ�������������Ͷ�ȡ��˷����롣
    void StartMicrophone()
    {
        audioSource.clip = Microphone.Start(null, true, 10, 44100);
        audioSource.loop = true;
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.Play();
    }


    //���� 3: ���� 3: ������Ƶ�����Թ��Ʒ���
    //����һ��������������Ƶ���ݣ���������Դ���������ԱȽ��������������������������������������ĸ�����
    //Create a method to analyze audio data and estimate sound source direction
    //compare the energy of the left and right channels to determine which direction the sound is more likely to come from.
    void Update()
    {
        if (audioSource.clip == null || Microphone.GetPosition(null) <= 0)
        {
            //Debug.LogWarning("Microphone is not ready or no data available.");
            return;
        }

        float direction = calculateDirection(); // ��ȡ����ķ���

        // ��鷽��仯�Ƿ�����
        if (Mathf.Abs(direction - lastDirection) > directionThreshold)
        {
            UpdateSoundDirection(direction);
            lastDirection = direction;
        }
        // ���ģ�͵�Ԥ���������Ź�����
        //CheckAndPlayDogBarkingSound(direction);
    }


    //private void CheckAndPlayDogBarkingSound(float direction)
    // {
    //   if (frameFeatures.Count >= frameSequenceLength)
    //     {
    //       (int bestClass, float bestConfidence) = ReturnBestClassAndConfidence(frameFeatures);
    //
    //      if (classMap[bestClass] == "Speech" && bestConfidence > 0.75f)
    //      {
    //          if (!isSpeechActive)
    //        {
    //            isSpeechActive = true;
    //            PlayDogBarkingSound(direction);  // ʹ�ü���õķ��򲥷�����
    //        }
    //    }
    //    else if (isSpeechActive)
    //     {
    //        isSpeechActive = false;
    //        StopDogBarkingSound();
    //    }

    //    frameFeatures.Clear(); // ����֡���л�����
    // }
    //  }


    private float calculateDirection()
    {

        Debug.Log("calculateDirection method called.");

        if (audioSource.clip == null)
        {
            Debug.LogWarning("audioSource.clip is null.");
            return 0.0f; // ���û����ƵƬ�Σ��޷����㷽��
        }
        int sampleSize = 1024;  // �������ڵĴ�С sample size
        float[] samples = new float[sampleSize * 2];  // ������ͨ�� Dual Channel

        int position = Microphone.GetPosition(null) - sampleSize;
        Debug.Log($"Microphone position: {position}");
        if (position < 0)
        {
            Debug.LogWarning("Microphone position is less than sample size.");
            return 0.0f;  // ȷ������ʹ�ø����� Make sure don't use negative indexes
        }
        audioSource.clip.GetData(samples, position);

        float leftSum = 0, rightSum = 0;
        for (int i = 0; i < sampleSize; i++)
        {
            leftSum += Mathf.Abs(samples[2 * i]);
            rightSum += Mathf.Abs(samples[2 * i + 1]);
        }
        Debug.Log($"LeftSum: {leftSum}, RightSum: {rightSum}, Difference: {rightSum - leftSum}");

        return rightSum - leftSum;  // �ұ�Ϊ�������Ϊ��    Right side is positive, left side is negative
    }




    private void UpdateSoundDirection(float direction)
    {
        Debug.Log($"Calculated Direction: {direction}");
        // ȷ����Դ��x���꣬10�������ң�-10��������
        //Determine the x-coordinate of the sound source, 10 represents right, -10 represents left
        float xPosition = direction > 0 ? 10 : -10;

        // ���ݷ���������Դ�ľ���λ��
        //Set the specific location of the sound source according to the direction
        Vector3 position = new Vector3(xPosition, soundSourceTransform.position.y, soundSourceTransform.position.z);

        // Ӧ��λ�ñ��
        //Apply location changes
        soundSourceTransform.position = position;
        Debug.Log($"Direction: {direction}, New Position: {position}");
    }

    //����˫�������룺

    //ȷ�������˷��豸�ܹ��ṩ˫�������롣
    //�� Unity �У���ȡ��˷�����ʱ���ֱ��ȡ�������������ݡ�
    //��Դ��λ�㷨��

    //����һ���������Ƚ��������������������Ŷȡ�
    //ͨ���������������Ĳ��죬������Դ�������磬�����������������������������Դƫ��
    //void Update()
    //{
    // float leftVolume = GetVolume(audioInputLeft);
    // float rightVolume = GetVolume(audioInputRight);
    // float direction = leftVolume - rightVolume; // ��ֵԽ����ԴԽƫ�󣻲�ֵԽС��Ϊ������ԴԽƫ�ҡ�

    // ������Դ��λ��
    //  soundSourceTransform.position = new Vector3(direction, 0, 0); // ���������Դֻ��ˮƽ�����ƶ�
    //}
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************




    private void Start()
    {

        // ��ʼ����Դ��λ�õ���������
        soundSourceTransform.position = new Vector3(0, soundSourceTransform.position.y, soundSourceTransform.position.z);
        //*********************************************************************************************************************************************
        //*********************************************************************************************************************************************
        //*********************************************************************************************************************************************
        audioSource.spatialBlend = 1.0f;  // ��ȫ��3D��Ч
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;  // ����˥��
        audioSource.minDistance = 1;
        audioSource.maxDistance = 50;
        //*********************************************************************************************************************************************
        //*********************************************************************************************************************************************
        //*********************************************************************************************************************************************

        // ������˷�¼��
        StartMicrophone();

        //Screen.sleepTimeout = SleepTimeout.NeverSleep; // Never sleep so that logging device stays awake while running.
        model = ModelLoader.Load(modelFile);
        worker = WorkerFactory.CreateWorker(model, WorkerFactory.Device.GPU);
        ClassMap();
        if (inputClip != null)
        {
            PredictAudioFile(inputClip);
        }
        StartCoroutine(LogTimer()); // Start log timer
    }

    public void PredictAudioFile(AudioClip input)
    {
        /*
        AudioClip preProcessedClip = YamnetPreprocess(input);
        YamnetProcess(preProcessedClip);
        */
        float[] waveform = new float[input.channels * input.samples];
        input.GetData(waveform, 0);
        PredictAudioFile(waveform, (int)(input.samples / input.length), input.channels, input.length);
    }

    public void PredictAudioFile(float[] input, int sampleRate, int numChannels, float lengthSeconds)
    {
        // Overload for raw waveform data.
        float[] preprocessedInput = YamnetPreprocess(input, sampleRate, numChannels, lengthSeconds);
        YamnetProcess(preprocessedInput);
    }


    private void Predict(double[] input)
    {
        float[] features = new float[96 * 64];
        for (int i = 0; i < input.Length; i++)
        {
            features[i] = (float)input[i];
        }


        Tensor inputTensor;
        string inputName = model.inputs[0].name;
        int[] shape = new int[4] { 1, 96, 64, 1 }; // Not sure what this does. Copied from other code, but it seems to be the format of tensor that the model expects.
        var inputs = new Dictionary<string, Tensor>();
        inputTensor = new Tensor(shape, features);
        inputs.Add(inputName, inputTensor);
        worker.Execute(inputs);


        // The try/finally block sturcture is pinched from the demo.
        try
        {
            string outputName = model.outputs[0];
            Tensor output = worker.CopyOutput(outputName);
            frameFeatures.Add(output);

            (int bestShortClass, float bestShortConfidence) = ReturnBestClassAndConfidence(output);
            YamnetClassification shortTermClassification = ReturnClassification(bestShortClass, bestShortConfidence);
            shortTermClassificationList.Add(shortTermClassification);
            shortClassName.text = shortTermClassification.className;
            shortConfidenceScore.text = shortTermClassification.confidence.ToString();

            matrices.Add(ReturnMatrix(output)); // Add classifications to matrix
            if (frameFeatures.Count < frameSequenceLength)
            {
                return;
            }
            if (frameFeatures.Count >= frameSequenceLength)
            {
                (int bestClass, float bestConfidence) = ReturnBestClassAndConfidence(frameFeatures);

                YamnetClassification classification = ReturnClassification(bestClass, bestConfidence);

                className.text = classification.className;
                confidenceScore.text = classification.confidence.ToString();
                Debug.Log(classification.className + ", " + classification.confidence);

                classificationList.Add(classification);

                frameFeatures = new List<Tensor>(0); // Reset frame sequence buffer.
            }
        }
        finally
        {
            inputTensor?.Dispose();
        }
    }






    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    // strat here!!!!!!!
    private bool isSpeechActive = false;

    private (int bestClass, float bestConfidence) ReturnBestClassAndConfidence(Tensor input)
    {

        if (numModelClasses == -1 || input.length < numModelClasses)
        {
            numModelClasses = input.AsFloats().Length; // ���������������ȷ�����ᳬ������߽�    Update the number of classes and ensure that the array bounds are not exceeded
        }
        int bestClass = -1;
        float bestConfidence = -1f;

        for (int i = 0; i < numModelClasses; i++)
        {
            float confidence = input[0, 0, 0, i]; // ȷ������i���ᳬ���߽�    Make sure index i does not go out of bounds

            //float confidence = input[0, 0, 0, i];
            if (confidence > bestConfidence)
            {
                bestClass = i;
                bestConfidence = confidence;
            }
        }

        // �ض�����speechʶ�� Specific processing of speech recognition
        if (classMap[bestClass] == "Speech" && bestConfidence > 0.5)
        {
            if (!isSpeechActive)
            {
                isSpeechActive = true;
                UpdateSoundDirection(calculateDirection()); // update sound source direction
                PlayDogBarkingSound();  // play in new position
            }
        }
        else if (isSpeechActive)
        {
            isSpeechActive = false;
            StopDogBarkingSound();
        }

        return (bestClass, bestConfidence);
    }



    //public AudioSource audioSource; // ȷ���� Unity �༭���н� AudioSource ����ϵ�����ֶ���
    private AudioSource barkingAudioSource;  // ���ڱ��沥�Ź������� AudioSource

    private void PlayDogBarkingSound()
    {
        if (dogBarkingClip != null)
        {
            //AudioSource.PlayClipAtPoint(dogBarkingClip, Camera.main.transform.position); // ���������λ�ò��Ź�����
            // ʹ��3D��Ƶ����
            //AudioSource.PlayClipAtPoint(dogBarkingClip, soundSourceTransform.position);
            // �����ǰû�в��ŵĹ��������򴴽�һ���µ� AudioSource ������
            //If there is no dog barking sound currently playing, create a new AudioSource to play it.
            if (barkingAudioSource == null)
            {
                barkingAudioSource = gameObject.AddComponent<AudioSource>();
                barkingAudioSource.spatialBlend = 1.0f;  // 3D��Ч
                barkingAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;  // ����˥��
                barkingAudioSource.minDistance = 1;
                barkingAudioSource.maxDistance = 50;
            }

            barkingAudioSource.clip = dogBarkingClip;
            barkingAudioSource.transform.position = soundSourceTransform.position;
            barkingAudioSource.Play();
        }
    }

    private void StopDogBarkingSound()
    {
        //if (audioSource.isPlaying)
        //{
        //    Debug.Log("Stopping dog barking sound.");
        //    audioSource.Stop();
        //}
        if (barkingAudioSource != null && barkingAudioSource.isPlaying)
        {
            barkingAudioSource.Stop();
        }
    }





    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************
    //*********************************************************************************************************************************************







    /*private (int bestClass, float bestConfidence) ReturnBestClassAndConfidence(Tensor input)
    {
        if (numModelClasses == -1)
        {
            // If number of model classes aren't set...
            numModelClasses = input.AsFloats().Length; // This is a blocking operation and was previously used every prediction, but the model outputs a static number of classes. Given we aren't changing models, we can cache this number.
        }
        int bestClass = -1;
        float bestConfidence = -1;
        for (int i = 0; i < numModelClasses; i++)
        {
            float confidence = input[0, 0, 0, i]; // Return confidence score

            if (confidence > bestConfidence)
            {
                bestClass = i;
                bestConfidence = confidence;
            }
        }

        return (bestClass, bestConfidence);
    }*/

    private YamnetClassification ReturnClassification(int id, float confidence)
    {
        YamnetClassification classification = new YamnetClassification();
        classification.className = classMap[id];
        classification.confidence = confidence;
        classification.timestamp = DateTime.Now;
        classification.timestampText = classification.timestamp.ToString("yyyy-MM-dd-HH-mm-ss");

        return classification;
    }

    private (int bestClass, float bestConfidence) ReturnBestClassAndConfidence(List<Tensor> input)
    {
        if (numModelClasses == -1)
        {
            // If number of model classes aren't set...
            numModelClasses = input[0].AsFloats().Length; // This is a blocking operation and was previously used every prediction, but the model outputs a static number of classes. Given we aren't changing models, we can cache this number.
        }
        int bestClass = -1;
        float bestConfidence = -1;
        for (int i = 0; i < numModelClasses; i++)
        {
            float meanScore = 0f;

            for (int tensor = 0; tensor < input.Count; tensor++)
            {
                meanScore += input[tensor][0, 0, 0, i]; // Sum the confidence of each prediction for this class.
            }
            meanScore /= input.Count;

            if (meanScore > bestConfidence)
            {
                bestClass = i;
                bestConfidence = meanScore;
            }
        }

        return (bestClass, bestConfidence);
    }

    private YamnetMatrix ReturnMatrix(Tensor input)
    {
        // This method constructs a matrix of all 521 YAMnet classes from the input tensor.
        YamnetMatrix matrix = new YamnetMatrix();
        matrix.classifications = new();

        for (int i = 0; i < input.AsFloats().Length; i++)
        {
            YamnetClassification classification = new();
            classification.className = classMap[i];
            classification.confidence = input[0, 0, 0, i];
            classification.timestamp = DateTime.Now;
            classification.timestampText = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            matrix.classifications.Add(classification);
        }

        //matrix.timestamp = DateTime.Now;
        //matrix.timestampText = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");

        return matrix;
    }

    private AudioClip YamnetPreprocess(AudioClip clip)
    {
        // ==========================================
        // CLIP RESAMPLING AND DOWNMIXING
        // ==========================================
        int yamnetSampleRate = 16000;
        AudioClip resampledClip = JakeDSP.ResampleAudioClip(clip, yamnetSampleRate); // Resample to 16kHz
        AudioClip monoClip = JakeDSP.DownmixAudioClip(resampledClip, 1); // Convert to mono
        // I believe Unity already normalises audio to the appropriate range of [-1.0, 1.0].

        return monoClip;
    }

    private float[] YamnetPreprocess(float[] waveform, int originalSampleRate, int numChannels, float originalLengthSeconds)
    {
        // Overload for raw waveform data.
        int yamnetSampleRate = 16000;
        float[] resampledWaveform = JakeDSP.ResampleAudioClip(waveform, originalSampleRate, yamnetSampleRate, numChannels, originalLengthSeconds);
        float[] downmixedWaveform = JakeDSP.DownmixAudioClip(resampledWaveform, numChannels, 1);
        return downmixedWaveform;
    }

    private void YamnetProcess(AudioClip clip)
    {
        int yamnetSampleRate = 16000;

        // ==========================================
        // CLIP PROCESSING
        // ==========================================

        // Based on https://github.com/tensorflow/models/blob/master/research/audioset/yamnet/features.py

        // "A spectrogram is computed using magnitudes of the Short-Time Fourier Transform with a window size of 25 ms, a window hop of 10 ms, and a periodic Hann window."

        AudioClip inputClip = clip;

        // Cache the waveform
        float[] waveform = new float[inputClip.samples];
        inputClip.GetData(waveform, 0);

        int windowLength = (int)(0.025 * yamnetSampleRate); // Calculate window length in samples for 25ms
        int windowHop = (int)(0.010 * yamnetSampleRate); // Calculate hop length in samples for 10ms.
        double[] window = JakeDSP.HannWindow(windowLength, true); // Generate Hann window

        // Get windowed waveform.
        int sampledCount = 0;
        double[] featuresBuffer = new double[96 * 64];
        int featuresBufferOffset = 0; // Used as destination index for writing mel specs to the features buffer.
        while (sampledCount < waveform.Length)
        {
            double[] windowed = JakeDSP.WindowWaveform(waveform, window, windowLength, sampledCount);
            double[] melSpectrogram = WindowedWaveformToMelSpec(windowed, windowLength, yamnetSampleRate);

            // Copy Mel Spec to feature buffer
            Array.Copy(melSpectrogram, 0, featuresBuffer, featuresBufferOffset, 64);
            featuresBufferOffset += 64;

            if (featuresBufferOffset / 64 == 96)
            {
                // If buffer is full:
                // - Make prediction
                // - Clear out predicted chunk and reset buffer offset.


                Predict(featuresBuffer);
                Array.Copy(featuresBuffer, 48 * 64, featuresBuffer, 0, 48 * 64); // "These features are then framed into 50%-overlapping examples of 0.96 seconds, where each example covers 64 mel bands and 96 frames of 10 ms each."
                // We ditch the 50% overlap here.
                featuresBufferOffset = 48 * 64;

                // I am not 100% convinced this is all correct, but it anecdotally is about as accurate as the demo.
            }


            sampledCount += windowHop; // Hop forward.
        }
    }

    private void YamnetProcess(float[] waveform)
    {
        int yamnetSampleRate = 16000;
        int windowLength = (int)(0.025 * yamnetSampleRate); // Calculate window length in samples for 25ms
        int windowHop = (int)(0.010 * yamnetSampleRate); // Calculate hop length in samples for 10ms.
        double[] window = JakeDSP.HannWindow(windowLength, true); // Generate Hann window

        // Get windowed waveform.
        int sampledCount = 0;
        double[] featuresBuffer = new double[96 * 64];
        int featuresBufferOffset = 0; // Used as destination index for writing mel specs to the features buffer.
        while (sampledCount < waveform.Length)
        {
            double[] windowed = JakeDSP.WindowWaveform(waveform, window, windowLength, sampledCount);
            double[] melSpectrogram = WindowedWaveformToMelSpec(windowed, windowLength, yamnetSampleRate);

            // Copy Mel Spec to feature buffer
            Array.Copy(melSpectrogram, 0, featuresBuffer, featuresBufferOffset, 64);
            featuresBufferOffset += 64;

            if (featuresBufferOffset / 64 == 96)
            {
                // If buffer is full:
                // - Make prediction
                // - Clear out predicted chunk and reset buffer offset.


                Predict(featuresBuffer);
                Array.Copy(featuresBuffer, 48 * 64, featuresBuffer, 0, 48 * 64); // "These features are then framed into 50%-overlapping examples of 0.96 seconds, where each example covers 64 mel bands and 96 frames of 10 ms each."
                // We ditch the 50% overlap here.
                featuresBufferOffset = 48 * 64;

                // I am not 100% convinced this is all correct, but it anecdotally is about as accurate as the demo.
            }


            sampledCount += windowHop; // Hop forward.
        }
    }

    private double[] WindowedWaveformToMelSpec(double[] windowed, int windowLength, int yamnetSampleRate)
    {
        // This is a messy set of inputs, frankly.
        // This function takes a windowed waveform, of windowLength, and transforms it into a Mel Spectrogram.
        int fftLength = (int)Math.Pow(2, (int)Math.Ceiling(Math.Log(windowLength) / Math.Log(2))); // This is the C# translation of the FFT length calculation used in the model docs. It's 512. Might be more efficient just to use that rather than recalculate every time.
        // int numSpectrogramBins = (int)Math.Floor((double)(fftLength / 2 + 1)); // This is the C# of the number of bins calculation used in the model docs. Not sure where it's used yet. Notes in https://stackoverflow.com/questions/4364823/how-do-i-obtain-the-frequencies-of-each-value-in-an-fft suggest that the higher bins are irrelevant as they're above Nyquist.
        System.Numerics.Complex[] fftBuffer = JakeDSP.ZeroPadBuffer(windowed, fftLength);
        RosettaFFT.FFT(fftBuffer);

        double[] fftMagnitudes = new double[fftLength];

        for (int i = 0; i < fftBuffer.Length; i++)
        {
            fftMagnitudes[i] = fftBuffer[i].Magnitude; // This restricts everything to real numbers, and computed the magnitude of each bin.
        }

        // ================
        // Mel Spectrogram
        // ================
        double fftBinWidth = (double)yamnetSampleRate / fftLength; //This is the width of each bin in Hz https://dsp.stackexchange.com/questions/48216/understanding-fft-fft-size-and-bins
        // The bin at index 0 is the DC signal, and so irrelevant. Bins beyond (N / 2) + 1 are above Nyquist
        int numMelBins = 64;
        int melMinHz = 125;
        int melMaxHz = 7500; // I'm presuming this is because it's about Nyquist for a 16k sample rate.
        double[] melSpectrogram = JakeDSP.SpecToMelSpec(fftMagnitudes, fftBinWidth, numMelBins, melMinHz, melMaxHz);

        return melSpectrogram;
    }

    private void ClassMap()
    {
        // Pinched wholesale from the Yamnet Unity demo:
        this.classMap = new string[521];

        TextAsset classMapData = (TextAsset)Resources.Load("yamnet_class_map", typeof(TextAsset));
        using (var reader = new StringReader(classMapData.text))
        {
            string line = reader.ReadLine(); // Discard header line
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    string[] parts = line.Split(',');
                    int classId = int.Parse(parts[0]);
                    if (parts.Length == 3) // If the class name doesn't contain a comma in it
                    {
                        classMap[classId] = parts[2];
                    }
                    else
                    {
                        string fullClassName = "";
                        for (int i = 2; i < parts.Length; i++)
                        {
                            fullClassName += parts[i];
                            if (i < parts.Length - 1)
                            {
                                // Add comma if this isn't the last part
                                fullClassName += ",";
                            }
                        }
                        fullClassName = fullClassName.Replace("\"", ""); // Strip out quotation marks
                        classMap[classId] = fullClassName;
                    }
                }
            }
        }
    }
    public void OnDestroy()
    {
        // Destroy worker if it exists.
        worker?.Dispose();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus && logEnabled)
        {
            // Focus lost: log to JSON.
            LogToJSON();
        }
    }

    private IEnumerator LogTimer()
    {
        while (true)
        {
            yield return new WaitForSeconds((float)logInterval); // Right now this just logs at each interval, rather than checking the last time logs were saved. I think this is probably ok ofr my purposes.
            if (logEnabled)
            {
                LogToJSON();
                LogMatrixToJSON();
            }
        }
    }

    private void LogToJSON()
    {
        string firstTimestamp = classificationList[0].timestamp.ToString("yyyy-MM-dd-HH-mm-ss");
        string lastTimestamp = classificationList[^1].timestamp.ToString("yyyy-MM-dd-HH-mm-ss"); // The ^ means to count from the end, so ^1 is a shorter way of writing the last element: https://stackoverflow.com/questions/1246918/how-can-i-find-the-last-element-in-a-list
        string logName = "Classification_Log-" + firstTimestamp + "--" + lastTimestamp + ".json";

        JSONLog newLog = new JSONLog();
        newLog.classifications = classificationList;
        newLog.device = SystemInfo.deviceModel;
        string dataToWrite = JsonUtility.ToJson(newLog, true);

        System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/LongTerm");
        System.IO.File.WriteAllText(Application.persistentDataPath + "/LongTerm/" + logName, dataToWrite);
        classificationList.Clear();

        firstTimestamp = shortTermClassificationList[0].timestamp.ToString("yyyy-MM-dd-HH-mm-ss");
        lastTimestamp = shortTermClassificationList[^1].timestamp.ToString("yyyy-MM-dd-HH-mm-ss");
        logName = "Short_Classification_Log-" + firstTimestamp + "--" + lastTimestamp + ".json";

        newLog = new JSONLog();
        newLog.classifications = shortTermClassificationList;
        newLog.device = SystemInfo.deviceModel;
        dataToWrite = JsonUtility.ToJson(newLog, true);
        System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/ShortTerm");
        System.IO.File.WriteAllText(Application.persistentDataPath + "/ShortTerm/" + logName, dataToWrite);
        shortTermClassificationList.Clear();
    }

    public void LogMatrixToJSON()
    {
        string firstTimestamp = matrices[0].classifications[0].timestamp.ToString("yyyy-MM-dd-HH-mm-ss");
        string lastTimestamp = matrices[^1].classifications[0].timestamp.ToString("yyyy-MM-dd-HH-mm-ss"); // The ^ means to count from the end, so ^1 is a shorter way of writing the last element: https://stackoverflow.com/questions/1246918/how-can-i-find-the-last-element-in-a-list
        string logName = "Matrix_Log-" + firstTimestamp + "--" + lastTimestamp + ".json";

        JSONMatrixLog newLog = new JSONMatrixLog();
        newLog.matrices = matrices;
        newLog.device = SystemInfo.deviceModel;
        string dataToWrite = JsonUtility.ToJson(newLog, true);

        System.IO.Directory.CreateDirectory(Application.persistentDataPath + "/Matrices");
        System.IO.File.WriteAllText(Application.persistentDataPath + "/Matrices/" + logName, dataToWrite);
        matrices.Clear();
    }
}

[System.Serializable]
public struct YamnetClassification
{
    // This struct is used to store relevant info for each classification.
    public string className;
    public float confidence;
    public DateTime timestamp;
    public string timestampText;
}

[System.Serializable]
public struct YamnetMatrix
{
    // This struct stores a full matrix output from the model
    //public DateTime timestamp;
    //public string timestampText;
    public List<YamnetClassification> classifications;
}

public class JSONLog
{
    public List<YamnetClassification> classifications;
    public string device; // Used so that I can track what device a log came from.
}

public class JSONMatrixLog
{
    public List<YamnetMatrix> matrices;
    public string device; // Used so that I can track what device a log came from.
}