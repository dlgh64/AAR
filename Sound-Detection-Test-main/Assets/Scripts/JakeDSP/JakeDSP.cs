using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Barracuda.TextureAsTensorData;

public class JakeDSP
{
    public static AudioClip ResampleAudioClip(AudioClip clip, int newRate)
    {
        // Downsampling
        // https://stackoverflow.com/questions/61081526/how-to-change-the-audio-sample-rate-in-unity
        // As I understand it this isn't really the correct way to do this.

        float[] samples = new float[clip.samples * clip.channels]; // Create a memory buffer to store the samples in. The file needs to be mono anyways, but this allows for the downmixing to happen anywhere.
        clip.GetData(samples, 0); // Fill samples array with clip data.
        // int newRate = 16000;


        int newSamplesCount = (int)(clip.length * newRate); // Calculates the amount of samples the downsampled file will consist of. This number is per-channel.

        List<float[]> clipOriginal = new(); // ORiginal clip audio
        List<float[]> clipNew = new(); // Where resampled clip will be held.

        if (clip.channels == 1) // If mono, prepare above clip arrays
        {
            clipOriginal.Add(samples);
            clipNew.Add(new float[(int)(newRate * clip.length)]);
        }
        else
        {
            for (int i = 0; i < clip.channels; i++) // Loop through number of channels to prepare clip arrays.
            {
                // Setup array lengths
                clipOriginal.Add(new float[clip.samples]);
                clipNew.Add(new float[(int)(newRate * clip.length)]);
            }

            for (int i = 0; i < samples.Length; i++)
            {
                clipOriginal[i % clip.channels][i / clip.channels] = samples[i]; // Double [] here is to access the arrays within the list elements.
            }
        }

        for (int c = 0; c < clip.channels; c++)
        {
            int index = 0;
            float sum = 0f;
            int count = 0;
            float[] channelSamples = clipOriginal[c];

            for (int i = 0; i < channelSamples.Length; i++)
            {
                int tempIndex = (int)((float)i / channelSamples.Length * clipNew[c].Length);

                if (tempIndex == index)
                {
                    sum += channelSamples[i];
                    count++;
                }
                else
                {
                    clipNew[c][index] = sum / count;
                    index = tempIndex;
                    sum = channelSamples[i];
                    count = 1;
                }
            }
        }

        float[] samplesNew;

        if (clip.channels == 1)
        {
            samplesNew = clipNew[0];
        }
        else
        {
            samplesNew = new float[clipNew[0].Length + clipNew[1].Length];

            for (int i = 0; i < samplesNew.Length; i++)
            {
                samplesNew[i] = clipNew[i % clip.channels][i / clip.channels];
            }
        }

        AudioClip resampledClip = AudioClip.Create(clip.name + "_" + newRate, newSamplesCount, clip.channels, newRate, false);
        resampledClip.SetData(samplesNew, 0);

        return resampledClip;
    }

    public static float[] ResampleAudioClip(float[] input, int originalRate, int newRate, int numChannels, float originalLength)
    {
        if (newRate == originalRate)
        {
            return input;
        }

        int newSamplesCount = (int)originalLength * newRate; // Calculate the number of samples the new array will consist of, based on the time length of the file and the new sample rate. This number is per-channel.

        List<float[]> clipOriginal = new();
        List<float[]> clipNew = new();

        for (int i = 0; i < numChannels; i++)
        {
            // In each channel, de-interleave the array
            float[] originalChannel = new float[(int)originalLength * originalRate];
            for(int j = 0; j < (originalChannel.Length / numChannels); j++)
            {
                int channelIndex = (j * numChannels) + i;
                originalChannel[j] = input[channelIndex]; 
            }
            clipOriginal.Add(originalChannel);
            clipNew.Add(new float[(int)(newRate * originalLength)]);
        }

        for (int c = 0; c < numChannels; c++)
        {
            int index = 0;
            float sum = 0f;
            int count = 0;
            float[] channelSamples = clipOriginal[c];

            for (int i = 0; i < channelSamples.Length; i++)
            {
                int tempIndex = (int)((float)i / channelSamples.Length * clipNew[c].Length);

                if (tempIndex == index)
                {
                    sum += channelSamples[i];
                    count++;
                }
                else
                {
                    clipNew[c][index] = sum / count;
                    index = tempIndex;
                    sum = channelSamples[i];
                    count = 1;
                }
            }
        }

        float[] samplesNew;

        if (numChannels == 1)
        {
            samplesNew = clipNew[0];
        }
        else
        {
            // Interleave new channels
            samplesNew = new float[newSamplesCount * numChannels];

            for (int i = 0; i < samplesNew.Length; i++)
            {
                samplesNew[i] = clipNew[i % numChannels][i / numChannels];
            }
        }

        return samplesNew;
    }
    public static AudioClip DownmixAudioClip(AudioClip clip, int newChannelCount)
    {
        // This one's all me, baby.

        if (clip.channels < newChannelCount)
        {
            System.Exception upmixException = new Exception("Method only allows downmixing, not upmixing");
            Debug.LogException(upmixException);
            return null;
        }

        if (clip.channels == newChannelCount)
        {
            return clip;
        }

        float[] oldSamples = new float[clip.samples * clip.channels];
        float[] newSamples = new float[clip.samples * newChannelCount];
        clip.GetData(oldSamples, 0);


        if (newChannelCount == 1 && clip.channels == 2)
        {
            // Stereo Downmix
            for (int i = 0; i < newSamples.Length; i++)
            {
                int sampleIndex = i * 2;
                newSamples[i] = Math.Clamp((oldSamples[sampleIndex] + oldSamples[sampleIndex + 1]) / 2, -1f, 1f);
            }
        }
        else
        {
            // Just pull the left channel. *shrug*
            for (int i = 0; (i < newSamples.Length); i++)
            {
                int sampleIndex = i * clip.channels;
                newSamples[i] = oldSamples[sampleIndex];
            }
        }

        int downMixLength = clip.samples;
        string newChannelLabel;

        if (newChannelCount ==1)
        {
            newChannelLabel = "MONO";
        }
        else if (newChannelCount ==2)
        {
            newChannelLabel = "STEREO";
        }
        else if (newChannelCount==4)
        {
            newChannelLabel = "4CHAN";
        }
        else
        {
            newChannelLabel = "WEIRD";
        }

        AudioClip downMixedClip = AudioClip.Create(clip.name + "_" + newChannelLabel, downMixLength, 1, clip.frequency, false);
        downMixedClip.SetData(newSamples, 0);


        return downMixedClip;
    }

    
    public static float[] DownmixAudioClip(float[] waveform, int originalChannelCount, int newChannelCount)
    {
        // Overload for raw wave data
        if (originalChannelCount < newChannelCount)
        {
            System.Exception upmixException = new Exception("Method only allows downmixing, not upmixing");
            Debug.LogException(upmixException);
            return null;
        }

        if (originalChannelCount == newChannelCount)
        {
            return waveform;
        }

        float[] oldSamples = waveform;
        float[] newSamples = new float[(waveform.Length / originalChannelCount) * newChannelCount];


        if (newChannelCount == 1 && originalChannelCount == 2)
        {
            //Debug.Log("Downmixing Stereo to Mono");
            // Stereo Downmix
            for (int i = 0; i < newSamples.Length; i++)
            {
                int sampleIndex = i * 2;
                float newSample = (oldSamples[sampleIndex] + oldSamples[sampleIndex + 1]) / 2; // Average L and R
                newSamples[i] = Math.Clamp(newSample, -1f, 1f);
            }
        }
        else
        {
            // Just pull the left channel. *shrug*
            Debug.Log("Downmixing unaccounted for. Pulling left channel only.");
            for (int i = 0; (i < newSamples.Length); i++)
            {
                int sampleIndex = i * originalChannelCount;
                newSamples[i] = oldSamples[sampleIndex];

            }
        }

        int downMixLength = waveform.Length / originalChannelCount;

        return newSamples;
    }
    

    public static double[] HannWindow(int windowLength, bool periodic = false)
    {
        int L;
        if (periodic)
        {
            L = windowLength + 1; // in a periodic Hann window, we generate a window of length L + 1, but only return L points.
        }
        else
        {
            L = windowLength;
        }

        int N = L - 1;

        double[] w = new double[L - 1]; // Double used for greater precision. We only return L points, however.

        for (int i = 0; i < N; i++)
        {
            w[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / N)); // Using the algorithm in https://uk.mathworks.com/help/signal/ref/hann.html
            
        }

        if (periodic)
        {
            // Taking this chunk from https://toto-share.com/2012/07/cc-hanning-matlab-code/

            for (int i = windowLength - 1; i >= 1; i--)
            {
                w[i] = w[i - 1];
            }
            w[0] = 0.0;
            

            // Which I think means I need to remove one sample:
            double[] _w = new double[windowLength];
            Array.Copy(w, _w, windowLength);
            return _w;

        }        

        return w;
    }

    public static void JakeFFT()
    {
        
    }

    public static double HzToMel(double hz)
    {
        // Based on equation set out in: https://uk.mathworks.com/help/audio/ref/hz2mel.html
        // This is the O'Shaughnessy (sp?) style. There's also Slaney-style Mel.
        double mel = 2595 * Math.Log10(1 + (hz / 700));
        return mel;
    }

    public static double MelToHz(double mel)
    {
        // Based on https://uk.mathworks.com/help/audio/ref/mel2hz.html
        // This is also O'Shaughnessy style, rather than Slaney.
        double hz = 700 * (Math.Pow(10, (mel/2595)) - 1);
        return hz;
    }

    public static double[] SpecToMelSpec(double[] spectrogram, double spectrogramBinWidth, int numMelBins, int melMinHz, int melMaxHz)
    {
        // http://practicalcryptography.com/miscellaneous/machine-learning/guide-mel-frequency-cepstral-coefficients-mfccs/ seems like a useful, accessible resource
        double[] melBandEdges = new double[numMelBins + 2]; // In the implementation of the linear_to_mel_weight_matrix that the official implementation calls, they note that they have 2 more than the bins. I guess that makes sense, as you need a lower and upper edge.
        double[] melSpectrogram = new double[numMelBins];
        for (int i = 0; i < numMelBins + 2; i++) // Compute mel band edges
        {
            // The Mel/Hz conversions are done here so that bands are spaced in the Mel domain, rather than in the Hz domain.
            double delta = (JakeDSP.HzToMel(melMaxHz) - JakeDSP.HzToMel(melMinHz));
            double div = numMelBins + 1; // In their example, they actually feed linspace numMelBins + 2, however that linspace function subtracts 1 internally, so effectively they're computing numMelBins + 1. https://github.com/numpy/numpy/blob/v1.26.0/numpy/core/function_base.py#L125C6-L125C6
            double step = delta / div;
            
            // Evenly space numMelBins bands between melMinHz and melMaxHz.
            melBandEdges[i] = JakeDSP.MelToHz(JakeDSP.HzToMel(melMinHz) + (i * step)); // I'm currently *assuming* we need to convert back to Hz
        }
        // Now turn those into actual bands.
        for (int i = 0; i < numMelBins; i++)
        {
            // Could also do these possibly more intuitively by starting i at 1.
            double bandLowerEdge = melBandEdges[i]; // Lower edge of this band
            double bandCentre = melBandEdges[i + 1]; // Upper edge of previous band, and lower end of next band
            double bandUpperEdge = melBandEdges[i + 2]; // Upper edge of this band.

            // Need to apply the mel bands to the existing spectrogram. Mel bands use triangular filters, where the peak is the bandCentre.
            // So from bandLowerEdge to bandCentre, things increase from 0 to 1, and then from bandCentre to bandUpperEdge, things decrease from 1 to 0.

            int spectrogramIndex = 1; // Start at 1 to exclude DC component at index 0.

            double filteredMagnitude = 0.0;
            while (spectrogramIndex * spectrogramBinWidth < bandLowerEdge)
            {
                spectrogramIndex++; // Iterate until we find the first bin in range. Probably inefficient as all hell.
            }

            while (spectrogramIndex * spectrogramBinWidth > bandLowerEdge && spectrogramIndex * spectrogramBinWidth < bandCentre)
            {
                // Ramp up from 0 to 1

                filteredMagnitude += spectrogram[spectrogramIndex] * ((HzToMel(spectrogramIndex * spectrogramBinWidth) - HzToMel(bandLowerEdge)) / (HzToMel(bandCentre) - HzToMel(bandLowerEdge)));
                spectrogramIndex++; // Move to next sample
            }
            while (spectrogramIndex * spectrogramBinWidth == bandCentre)
            {
                // 1

                filteredMagnitude += spectrogram[spectrogramIndex];
                spectrogramIndex++; // Move to next sample
            }
            while (spectrogramIndex * spectrogramBinWidth > bandCentre && spectrogramIndex * spectrogramBinWidth < bandUpperEdge)
            {
                // Ramp down from 1
                filteredMagnitude += spectrogram[spectrogramIndex] * ((HzToMel(bandUpperEdge) - HzToMel(spectrogramIndex * spectrogramBinWidth)) / (HzToMel(bandUpperEdge) - HzToMel(spectrogramIndex * spectrogramBinWidth)));
                spectrogramIndex++; // Move to next sample
            }
            // melSpectrogram[i] = filteredMagnitude
            melSpectrogram[i] = Math.Log(filteredMagnitude) + 0.001; // Stabilized log mel spectrogram.
                                                                     // Think this is now Mel spectrogram complete?

            // We get our 0-1 ratio based on Mel values as it seems to be how Google are doing it. Other fella is not doing that...
            // Line 204 in https://github.com/tensorflow/tensorflow/blob/v2.14.0/tensorflow/python/ops/signal/mel_ops.py#L90-L216
        }
        return melSpectrogram;
    }

    public static System.Numerics.Complex[] ZeroPadBuffer(double[] inputBuffer, int outputBufferLength)
    {
        // Takes an input buffer of an arbitrary length, and a desired length, and pads a buffer with zeroes to match that length.
        // Useful for padding FFT input buffers to the nearest power of 2, for example.
        System.Numerics.Complex[] outputBuffer = new System.Numerics.Complex[outputBufferLength];
        for (int i = 0; i < outputBufferLength; i++)
        {
            if (i < inputBuffer.Length)
            {
                outputBuffer[i] = new System.Numerics.Complex(inputBuffer[i], 0);
            }
            else
            {
                // Zero it if there's no waveform left. That's what the other fella did and I did see a source saying ot do that, but need to find it again.
                // FFT algorithms are more efficient if their length is a power of 2, so windows are zeroed out to pad to the nearest power.
                // - https://dsp.stackexchange.com/questions/48216/understanding-fft-fft-size-and-bins
                outputBuffer[i] = 0.0;
            }
        }
        return outputBuffer;
    }

    public static double[] WindowWaveform(float[] waveformInput, double[] window, int windowLength, int startSampleIndex)
    {
        // Multiplies a segment of waveform with a given window. Offset is set by startSampleIndex, e.g. to window the windowLength samples beginning from sample 167.
        double[] windowedOutput = new double[windowLength];

        for (int i = 0; i < window.Length; i++)
        {
            if (i + startSampleIndex < waveformInput.Length)
            {
                // If the index is within range, window it based on waveform
                windowedOutput[i] = waveformInput[i + startSampleIndex] * window[i];
            }
            else
            {
                // Pad to zero otherwise
                windowedOutput[i] = 0;
            }
        }

        return windowedOutput;
    }

}
