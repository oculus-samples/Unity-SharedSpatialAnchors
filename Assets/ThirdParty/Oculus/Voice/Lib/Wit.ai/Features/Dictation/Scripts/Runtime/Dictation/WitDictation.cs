/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Events;
using Meta.WitAi.Interfaces;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.WitAi.Dictation
{
    public class WitDictation : DictationService, IWitRuntimeConfigProvider, IVoiceEventProvider, IWitRequestProvider, IWitConfigurationProvider
    {
        [SerializeField] private WitRuntimeConfiguration witRuntimeConfiguration;

        private WitService witService;

        public WitRuntimeConfiguration RuntimeConfiguration
        {
            get => witRuntimeConfiguration;
            set => witRuntimeConfiguration = value;
        }
        public WitConfiguration Configuration => RuntimeConfiguration?.witConfiguration;

        #region Voice Service Properties

        public override bool Active => null != witService && witService.Active;
        public override bool IsRequestActive => null != witService && witService.IsRequestActive;

        public override ITranscriptionProvider TranscriptionProvider
        {
            get => witService.TranscriptionProvider;
            set => witService.TranscriptionProvider = value;

        }

        public override bool MicActive => null != witService && witService.MicActive;

        protected override bool ShouldSendMicData => witRuntimeConfiguration.sendAudioToWit ||
                                                     null == TranscriptionProvider;

        private readonly VoiceEvents voiceEvents = new VoiceEvents();

        /// <summary>
        /// Events specific to wit voice activation.
        /// </summary>
        public VoiceEvents VoiceEvents => voiceEvents;

        #endregion

        #region IWitRequestProvider
        public WitRequest CreateWitRequest(WitConfiguration config, WitRequestOptions requestOptions,
            IDynamicEntitiesProvider[] additionalEntityProviders = null)
        {
            return config.CreateDictationRequest(requestOptions);
        }

        #endregion

        #region Voice Service Methods


        /// <summary>
        /// Activates and waits for the user to exceed the min wake threshold before data is sent to the server.
        /// </summary>
        public override void Activate()
        {
            witService.Activate();
        }

        /// <summary>
        /// Activates and waits for the user to exceed the min wake threshold before data is sent to the server.
        /// </summary>
        /// <param name="options"></param>
        public override void Activate(WitRequestOptions options)
        {
            witService.Activate(options);
        }

        /// <summary>
        /// Activates immediately and starts sending data to the server. This will not wait for min wake threshold
        /// </summary>
        public override void ActivateImmediately()
        {
            witService.ActivateImmediately();
        }

        /// <summary>
        /// Activates immediately and starts sending data to the server. This will not wait for min wake threshold
        /// </summary>
        /// <param name="options"></param>
        public override void ActivateImmediately(WitRequestOptions options)
        {
            witService.ActivateImmediately(options);
        }

        /// <summary>
        /// Deactivates. If a transcription is in progress the network request will complete and any additional
        /// transcription values will be returned.
        /// </summary>
        public override void Deactivate()
        {
            witService.Deactivate();
        }

        /// <summary>
        /// Deactivates and ignores any pending transcription content.
        /// </summary>
        public override void Cancel()
        {
            witService.DeactivateAndAbortRequest();
        }
        #endregion

        protected override void Awake()
        {
            base.Awake();
            witService = gameObject.AddComponent<WitService>();
            witService.VoiceEventProvider = this;
            witService.ConfigurationProvider = this;
            witService.WitRequestProvider = this;
            witService.TelemetryEventsProvider = this;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            VoiceEvents.OnFullTranscription.AddListener(OnFullTranscription);
            VoiceEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
            VoiceEvents.OnStartListening.AddListener(OnStartedListening);
            VoiceEvents.OnStoppedListening.AddListener(OnStoppedListening);
            VoiceEvents.OnMicLevelChanged.AddListener(OnMicLevelChanged);
            VoiceEvents.OnError.AddListener(OnError);
            VoiceEvents.OnResponse.AddListener(OnResponse);
            VoiceEvents.OnRequestCompleted.AddListener(OnCompleted);
            VoiceEvents.OnAborting.AddListener(OnAborting);
            VoiceEvents.OnAborted.AddListener(OnAborted);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            VoiceEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
            VoiceEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
            VoiceEvents.OnStartListening.RemoveListener(OnStartedListening);
            VoiceEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
            VoiceEvents.OnMicLevelChanged.RemoveListener(OnMicLevelChanged);
            VoiceEvents.OnError.RemoveListener(OnError);
            VoiceEvents.OnResponse.RemoveListener(OnResponse);
            VoiceEvents.OnRequestCompleted.RemoveListener(OnCompleted);
            VoiceEvents.OnAborting.RemoveListener(OnAborting);
            VoiceEvents.OnAborted.RemoveListener(OnAborted);
        }

        private void OnCompleted()
        {
            DictationEvents.OnRequestCompleted?.Invoke();
        }

        private void OnAborted()
        {
            DictationEvents.OnAborted?.Invoke();
        }

        private void OnAborting()
        {
            DictationEvents.OnAborting?.Invoke();
        }

        private void OnFullTranscription(string transcription)
        {
            DictationEvents.OnFullTranscription?.Invoke(transcription);
        }

        private void OnPartialTranscription(string transcription)
        {
            DictationEvents.OnPartialTranscription?.Invoke(transcription);
        }

        private void OnStartedListening()
        {
            DictationEvents.onStart?.Invoke();
        }

        private void OnStoppedListening()
        {
            DictationEvents.onStopped?.Invoke();
        }

        private void OnMicLevelChanged(float level)
        {
            DictationEvents.onMicAudioLevel?.Invoke(level);
        }

        private void OnError(string error, string message)
        {
            DictationEvents.onError?.Invoke(error, message);
        }

        private void OnResponse(WitResponseNode response)
        {
            DictationEvents.onResponse?.Invoke(response);
        }
    }
}
