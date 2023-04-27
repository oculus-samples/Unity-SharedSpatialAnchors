/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.WitAi.Requests
{
    // Audio stream support type
    public enum AudioStreamDecodeType
    {
        PCM16,
        MP3
    }
    // Data used to handle stream
    public struct AudioStreamData
    {
        // Generated clip name
        public string ClipName;
        // Amount of clip length in seconds that must be received before stream is considered ready.
        public float ClipReadyLength;
        // Total samples to be used to generate clip. A new clip will be generated every time this chunk size is surpassed
        public int ClipChunkSize;

        // Type of audio code to be decoded
        public AudioStreamDecodeType DecodeType;
        // Total channels being streamed
        public int DecodeChannels;
        // Samples per second being streamed
        public int DecodeSampleRate;
    }

    // Audio stream handler
    public class AudioStreamHandler : DownloadHandlerScript, IVRequestStreamable
    {
        // Audio stream data
        public AudioStreamData StreamData { get; private set; }

        // Current audio clip
        public AudioClip Clip { get; private set; }
        // Ready to stream
        public bool IsStreamReady { get; private set; }
        // Ready to stream
        public bool IsStreamComplete { get; private set; }

        // Current number of samples in clip
        private int _clipSamples = 0;
        // Current total samples loaded
        private int _loadedSamples = 0;
        // Leftover byte
        private bool _hasLeftover = false;
        private byte[] _leftovers = new byte[2];

        // Delegate that accepts an old clip and a new clip
        public delegate void AudioStreamClipUpdateDelegate(AudioClip oldClip, AudioClip newClip);
        // Callback for audio clip update during stream
        public static event AudioStreamClipUpdateDelegate OnClipUpdated;
        // Callback for audio stream complete
        public static event Action<AudioClip> OnStreamComplete;

        // Generate
        public AudioStreamHandler(AudioStreamData streamData) : base()
        {
            // Apply parameters
            StreamData = streamData;

            // Setup data
            _clipSamples = 0;
            _loadedSamples = 0;
            _hasLeftover = false;
            IsStreamReady = false;
            IsStreamComplete = false;
            VLog.D($"Clip Stream - Began\nStream Data:\n{JsonConvert.SerializeObject(streamData)}");
        }
        // If size is provided, generate clip using size
        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            // Ignore if already complete
            if (contentLength == 0 || IsStreamComplete)
            {
                return;
            }
            // Apply size
            int newMaxSamples = Mathf.Max(GetClipSamplesFromContentLength(contentLength, StreamData.DecodeType), _clipSamples);
            VLog.D($"Clip Stream - Received Size\nTotal Samples: {newMaxSamples}");
            GenerateClip(newMaxSamples);
        }
        // Receive data
        protected override bool ReceiveData(byte[] receiveData, int dataLength)
        {
            // Exit if desired
            if (!base.ReceiveData(receiveData, dataLength) || IsStreamComplete)
            {
                return false;
            }

            // Next decoded samples
            float[] newSamples = null;

            // Decode PCM chunk
            if (StreamData.DecodeType == AudioStreamDecodeType.PCM16)
            {
                newSamples = DecodeChunkPCM16(receiveData, dataLength, ref _hasLeftover, ref _leftovers);
            }
            // Not supported
            else
            {
                VLog.E($"Not Supported Decode File Type\nType: {StreamData.DecodeType}");
            }
            // Failed
            if (newSamples == null)
            {
                return false;
            }

            // Generate initial clip
            if (Clip == null)
            {
                int newMaxSamples = Mathf.Max(StreamData.ClipChunkSize,
                    _loadedSamples + newSamples.Length);
                GenerateClip(newMaxSamples);
            }
            // Generate larger clip if needed
            else if (_loadedSamples + newSamples.Length > _clipSamples)
            {
                int newMaxSamples = Mathf.Max(_clipSamples + StreamData.ClipChunkSize,
                    _loadedSamples + newSamples.Length);
                GenerateClip(newMaxSamples);
            }

            // Apply to clip
            Clip.SetData(newSamples, _loadedSamples);
            _loadedSamples += newSamples.Length;

            // Stream is now ready
            if (!IsStreamReady && (float)_loadedSamples / StreamData.DecodeSampleRate >= StreamData.ClipReadyLength)
            {
                IsStreamReady = true;
                VLog.D($"Clip Stream - Stream Ready");
            }

            // Return data
            return true;
        }

        // Clean up clip with final sample count
        protected override void CompleteContent()
        {
            // Ignore if called multiple times
            if (IsStreamComplete)
            {
                return;
            }

            // Reduce to actual size if needed
            if (_loadedSamples != _clipSamples)
            {
                GenerateClip(_loadedSamples);
            }

            // Stream complete
            IsStreamComplete = true;
            OnStreamComplete?.Invoke(Clip);
            VLog.D($"Clip Stream - Complete\nSamples: {_loadedSamples}");
        }

        // Destroy old clip
        public void CleanUp()
        {
            if (Clip != null)
            {
                // If successfully completed, destroy elsewhere
                if (!IsStreamComplete)
                {
                    Clip.DestroySafely();
                }
                Clip = null;
            }
            IsStreamComplete = true;
            VLog.D($"Clip Stream - Cleanup");
        }

        // Generate clip
        private void GenerateClip(int samples)
        {
            // Already generated
            if (Clip != null && _clipSamples == samples)
            {
                return;
            }

            // Get old clip if applicable
            AudioClip oldClip = Clip;
            int oldClipSamples = _clipSamples;

            // Generate new clip
            _clipSamples = samples;
            Clip = AudioClip.Create(StreamData.ClipName, samples, StreamData.DecodeChannels, StreamData.DecodeSampleRate, false);
            VLog.D($"Clip Stream - Clip Generated\nSamples: {samples}");

            // If previous clip existed, get previous data
            if (oldClip != null)
            {
                // Apply existing data
                oldClipSamples = Mathf.Min(oldClipSamples, samples);
                float[] oldSamples = new float[oldClipSamples];
                oldClip.GetData(oldSamples, 0);
                Clip.SetData(oldSamples, 0);

                // Invoke clip updated callback
                OnClipUpdated?.Invoke(oldClip, Clip);
                VLog.D($"Clip Stream - Clip Updated\nSamples: {samples}\nOld Samples: {oldClipSamples}");

                // Destroy previous clip
                oldClip.DestroySafely();
                oldClip = null;
            }
        }

        #region STATIC
        // Decode raw pcm data
        public static AudioClip GetClipFromRawData(byte[] rawData, AudioStreamDecodeType decodeType, string clipName, int channels, int sampleRate)
        {
            // Decode data
            float[] samples = null;
            if (decodeType == AudioStreamDecodeType.PCM16)
            {
                samples = DecodePCM16(rawData);
            }
            // Not supported
            else
            {
                VLog.E($"Not Supported Decode File Type\nType: {decodeType}");
            }
            // Failed to decode
            if (samples == null)
            {
                return null;
            }

            // Generate clip
            AudioClip result = AudioClip.Create(clipName, samples.Length, channels, sampleRate, false);
            result.SetData(samples, 0);
            return result;
        }
        // Determines clip sample count via content length dependent on file type
        public static int GetClipSamplesFromContentLength(ulong contentLength, AudioStreamDecodeType decodeType)
        {
            switch (decodeType)
            {
                    case AudioStreamDecodeType.PCM16:
                        return Mathf.FloorToInt(contentLength / 2f);
            }
            return 0;
        }
        #endregion

        #region PCM DECODE
        // Decode an entire array
        public static float[] DecodePCM16(byte[] rawData)
        {
            float[] samples = new float[Mathf.FloorToInt(rawData.Length / 2f)];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = DecodeSamplePCM16(rawData, i * 2);
            }
            return samples;
        }
        // Decode a single chunk
        private static float[] DecodeChunkPCM16(byte[] chunkData, int chunkLength, ref bool hasLeftover, ref byte[] leftovers)
        {
            // Determine if previous chunk had a leftover or if newest chunk contains one
            bool prevLeftover = hasLeftover;
            bool nextLeftover = (chunkLength - (prevLeftover ? 1 : 0)) % 2 != 0;
            hasLeftover = nextLeftover;

            // Generate sample array
            int startOffset = prevLeftover ? 1 : 0;
            int endOffset = nextLeftover ? 1 : 0;
            int newSampleCount = (chunkLength + startOffset - endOffset) / 2;
            float[] newSamples = new float[newSampleCount];

            // Append first byte to previous array
            if (prevLeftover)
            {
                // Append first byte to leftover array
                leftovers[1] = chunkData[0];
                // Decode first sample
                newSamples[0] = DecodeSamplePCM16(leftovers, 0);
            }

            // Store last byte
            if (nextLeftover)
            {
                leftovers[0] = chunkData[chunkLength - 1];
            }

            // Decode remaining samples
            for (int i = startOffset; i < newSamples.Length - startOffset; i++)
            {
                newSamples[i] = DecodeSamplePCM16(chunkData, startOffset + i * 2);
            }

            // Return samples
            return newSamples;
        }
        // Decode a single sample
        private static float DecodeSamplePCM16(byte[] rawData, int index)
        {
            return Mathf.Clamp((float)BitConverter.ToInt16(rawData, index) / Int16.MaxValue, -1f, 1f);
        }
        #endregion
    }
}
