/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using Meta.Conduit;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.Data.Intents;
using Facebook.WitAi.Events;
using Facebook.WitAi.Events.UnityEventListeners;
using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi
{
    public abstract class VoiceService : MonoBehaviour, IVoiceService, IInstanceResolver, IAudioEventProvider
    {
        /// <summary>
        /// When set to true, Conduit will be used. Otherwise, the legacy dispatching will be used.
        /// </summary>
        private bool UseConduit => _witConfiguration && _witConfiguration.useConduit;

        /// <summary>
        /// The wit configuration.
        /// </summary>
        private WitConfiguration _witConfiguration;

        private readonly IParameterProvider conduitParameterProvider = new WitConduitParameterProvider();

        [Tooltip("Events that will fire before, during and after an activation")] [SerializeField]
        public VoiceEvents events = new VoiceEvents();

        /// <summary>
        /// Returns true if this voice service is currently active and listening with the mic
        /// </summary>
        public abstract bool Active { get; }

        /// <summary>
        /// The Conduit-based dispatcher that dispatches incoming invocations based on a manifest.
        /// </summary>
        internal IConduitDispatcher ConduitDispatcher { get; set; }

        /// <summary>
        /// Returns true if the service is actively communicating with Wit.ai during an Activation. The mic may or may not still be active while this is true.
        /// </summary>
        public abstract bool IsRequestActive { get; }

        /// <summary>
        /// Gets/Sets a custom transcription provider. This can be used to replace any built in asr
        /// with an on device model or other provided source
        /// </summary>
        public abstract ITranscriptionProvider TranscriptionProvider { get; set; }

        /// <summary>
        /// Returns true if this voice service is currently reading data from the microphone
        /// </summary>
        public abstract bool MicActive { get; }

        public virtual VoiceEvents VoiceEvents
        {
            get => events;
            set => events = value;
        }

        /// <summary>
        /// A subset of events around collection of audio data
        /// </summary>
        public IAudioInputEvents AudioEvents => VoiceEvents;

        /// <summary>
        /// A subset of events around receiving transcriptions
        /// </summary>
        public ITranscriptionEvent TranscriptionEvents => VoiceEvents;

        /// <summary>
        /// Returns true if the audio input should be read in an activation
        /// </summary>
        protected abstract bool ShouldSendMicData { get; }

        /// <summary>
        /// Constructs a <see cref="VoiceService"/>
        /// </summary>
        protected VoiceService()
        {
            var conduitDispatcherFactory = new ConduitDispatcherFactory(this, this.conduitParameterProvider);
            ConduitDispatcher = conduitDispatcherFactory.GetDispatcher();
        }

        /// <summary>
        /// Send text data for NLU processing. Results will return the same way a voice based activation would.
        /// </summary>
        /// <param name="text"></param>
        public void Activate(string text) => Activate(text, new WitRequestOptions());

        /// <summary>
        /// Send text data for NLU processing with custom request options.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="requestOptions"></param>
        public abstract void Activate(string text, WitRequestOptions requestOptions);

        /// <summary>
        /// Start listening for sound or speech from the user and start sending data to Wit.ai once sound or speech has been detected.
        /// </summary>
        public void Activate() => Activate(new WitRequestOptions());

        /// <summary>
        /// Activate the microphone and send data for NLU processing. Includes optional additional request parameters like dynamic entities and maximum results.
        /// </summary>
        /// <param name="requestOptions"></param>
        public abstract void Activate(WitRequestOptions requestOptions);

        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.
        /// </summary>
        public void ActivateImmediately() => ActivateImmediately(new WitRequestOptions());

        /// <summary>
        /// Activate the microphone and send data for NLU processing immediately without waiting for sound/speech from the user to begin.  Includes optional additional request parameters like dynamic entities and maximum results.
        /// </summary>
        public abstract void ActivateImmediately(WitRequestOptions requestOptions);

        /// <summary>
        /// Stop listening and submit any remaining buffered microphone data for processing.
        /// </summary>
        public abstract void Deactivate();

        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        public abstract void DeactivateAndAbortRequest();

        /// <summary>
        /// Returns objects of the specified type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>Objects of the specified type.</returns>
        public IEnumerable<object> GetObjectsOfType(Type type)
        {
            return FindObjectsOfType(type);
        }

        protected virtual void Awake()
        {
            var witConfigProvider = this.GetComponent<IWitRuntimeConfigProvider>();
            _witConfiguration = witConfigProvider?.RuntimeConfiguration?.witConfiguration;

            InitializeEventListeners();

            if (!UseConduit)
            {
                MatchIntentRegistry.Initialize();
            }
        }

        private void InitializeEventListeners()
        {
            var audioEventListener = GetComponent<AudioEventListener>();
            if (!audioEventListener)
            {
                gameObject.AddComponent<AudioEventListener>();
            }

            var transcriptionEventListener = GetComponent<TranscriptionEventListener>();
            if (!transcriptionEventListener)
            {
                gameObject.AddComponent<TranscriptionEventListener>();
            }
        }

        protected virtual void OnEnable()
        {
            if (UseConduit)
            {
                ConduitDispatcher.Initialize(_witConfiguration.manifestLocalPath);
            }
            VoiceEvents.OnPartialResponse.AddListener(ValidateShortResponse);
            VoiceEvents.OnResponse.AddListener(HandleResponse);
        }

        protected virtual void OnDisable()
        {
            VoiceEvents.OnPartialResponse.RemoveListener(ValidateShortResponse);
            VoiceEvents.OnResponse.RemoveListener(HandleResponse);
        }

        protected virtual void ValidateShortResponse(WitResponseNode response)
        {
            if (VoiceEvents.OnValidatePartialResponse != null)
            {
                // Create short response data
                VoiceSession validationData = new VoiceSession();
                validationData.service = this;
                validationData.response = response;
                validationData.validResponse = false;

                // Call short response
                VoiceEvents.OnValidatePartialResponse.Invoke(validationData);

                // Invoke
                if (UseConduit)
                {
                    // Ignore without an intent
                    WitIntentData intent = response.GetFirstIntentData();
                    if (intent != null)
                    {
                        Dictionary<string, object> parameters = GetConduitResponseParameters(response);
                        parameters[WitConduitParameterProvider.VoiceSessionReservedName] = validationData;
                        ConduitDispatcher.InvokeAction(intent.name, parameters, intent.confidence, true);
                    }
                }

                // Deactivate
                if (validationData.validResponse)
                {
                    // Call response
                    VoiceEvents.OnResponse?.Invoke(response);

                    // Deactivate immediately
                    DeactivateAndAbortRequest();
                }
            }
        }

        protected virtual void HandleResponse(WitResponseNode response)
        {
            HandleIntents(response);
        }

        private void HandleIntents(WitResponseNode response)
        {
            var intents = response.GetIntents();
            foreach (var intent in intents)
            {
                HandleIntent(intent, response);
            }
        }

        private void HandleIntent(WitIntentData intent, WitResponseNode response)
        {
            if (UseConduit)
            {
                ConduitDispatcher.InvokeAction(intent.name, GetConduitResponseParameters(response), intent.confidence, false);
            }
            else
            {
                var methods = MatchIntentRegistry.RegisteredMethods[intent.name];
                foreach (var method in methods)
                {
                    ExecuteRegisteredMatch(method, intent, response);
                }
            }
        }

        // Handle conduit response parameters
        private Dictionary<string, object> GetConduitResponseParameters(WitResponseNode response)
        {
            var parameters = new Dictionary<string, object>();
            foreach (var entity in response.AsObject["entities"].Childs)
            {
                var parameterName = entity[0]["role"].Value;
                var parameterValue = entity[0]["value"].Value;
                parameters.Add(parameterName, parameterValue);
            }
            parameters.Add(WitConduitParameterProvider.WitResponseNodeReservedName, response);
            return parameters;
        }

        private void ExecuteRegisteredMatch(RegisteredMatchIntent registeredMethod,
            WitIntentData intent, WitResponseNode response)
        {
            if (intent.confidence >= registeredMethod.matchIntent.MinConfidence &&
                intent.confidence <= registeredMethod.matchIntent.MaxConfidence)
            {
                foreach (var obj in GetObjectsOfType(registeredMethod.type))
                {
                    var parameters = registeredMethod.method.GetParameters();
                    if (parameters.Length == 0)
                    {
                        registeredMethod.method.Invoke(obj, Array.Empty<object>());
                        continue;
                    }
                    if (parameters[0].ParameterType != typeof(WitResponseNode) || parameters.Length > 2)
                    {
                        VLog.E("Match intent only supports methods with no parameters or with a WitResponseNode parameter. Enable Conduit or adjust the parameters");
                        continue;
                    }
                    if (parameters.Length == 1)
                    {
                        registeredMethod.method.Invoke(obj, new object[] {response});
                    }
                }
            }
        }
    }

    public interface IVoiceService : IVoiceEventProvider
    {
        /// <summary>
        /// Returns true if this voice service is currently active and listening with the mic
        /// </summary>
        bool Active { get; }

        bool IsRequestActive { get; }

        bool MicActive { get; }

        new VoiceEvents VoiceEvents { get; set; }

        ITranscriptionProvider TranscriptionProvider { get; set; }

        /// <summary>
        /// Send text data for NLU processing with custom request options.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="requestOptions">Custom request options</param>
        void Activate(string text, WitRequestOptions requestOptions);

        /// <summary>
        /// Activate the microphone and wait for threshold and then send data
        /// </summary>
        /// <param name="requestOptions">Custom request options</param>
        void Activate(WitRequestOptions requestOptions);

        /// <summary>
        /// Activate the microphone and send data for NLU processing with custom request options.
        /// </summary>
        /// <param name="requestOptions">Custom request options</param>
        void ActivateImmediately(WitRequestOptions requestOptions);

        /// <summary>
        /// Stop listening and submit the collected microphone data for processing.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Stop listening and abort any requests that may be active without waiting for a response.
        /// </summary>
        void DeactivateAndAbortRequest();
    }

    public static class VLog
    {
        #if UNITY_EDITOR
        /// <summary>
        /// Ignores logs in editor if less than log level (Error = 0, Warning = 2, Log = 3)
        /// </summary>
        public static LogType EditorLogLevel
        {
            get
            {
                if (_editorLogLevel == (LogType) (-1))
                {
                    string editorLogLevel = UnityEditor.EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, EDITOR_LOG_LEVEL_DEFAULT.ToString());
                    if (!Enum.TryParse(editorLogLevel, out _editorLogLevel))
                    {
                        _editorLogLevel = EDITOR_LOG_LEVEL_DEFAULT;
                    }
                }
                return _editorLogLevel;
            }
            set
            {
                _editorLogLevel = value;
                UnityEditor.EditorPrefs.SetString(EDITOR_LOG_LEVEL_KEY, _editorLogLevel.ToString());
            }
        }
        private static LogType _editorLogLevel = (LogType)(-1);
        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const LogType EDITOR_LOG_LEVEL_DEFAULT = LogType.Warning;
        #endif

        /// <summary>
        /// Event for appending custom data to a log before logging to console
        /// </summary>
        public static event Action<StringBuilder, string, LogType> OnPreLog;

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void D(string log) => Log(LogType.Log, null, log);
        public static void D(string logCategory, string log) => Log(LogType.Log, logCategory, log);

        /// <summary>
        /// Performs a Debug.LogWarning with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void W(string log) => Log(LogType.Warning, null, log);
        public static void W(string logCategory, string log) => Log(LogType.Warning, logCategory, log);

        /// <summary>
        /// Performs a Debug.LogError with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void E(string log) => Log(LogType.Error, null, log);
        public static void E(string logCategory, string log) => Log(LogType.Error, logCategory, log);

        /// <summary>
        /// Filters out unwanted logs, appends category information
        /// and performs UnityEngine.Debug.Log as desired
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="log"></param>
        /// <param name="category"></param>
        private static void Log(LogType logType, string logCategory, string log)
        {
            #if UNITY_EDITOR
            // Skip logs with higher log type then global log level
            if ((int) logType > (int)EditorLogLevel)
            {
                return;
            }
            #endif

            // Use calling category if null
            string category = logCategory;
            if (string.IsNullOrEmpty(category))
            {
                category = GetCallingCategory();
            }

            // String builder
            StringBuilder result = new StringBuilder();

            #if !UNITY_EDITOR && !UNITY_ANDROID
            {
                // Start with datetime if not done so automatically
                DateTime now = DateTime.Now;
                result.Append($"[{now.ToShortDateString()} {now.ToShortTimeString()}] ");
            }
            #endif

            // Insert log type
            int start = result.Length;
            result.Append($"[{logType.ToString().ToUpper()}] ");
            WrapWithLogColor(result, start, logType);

            // Append VDSK & Category
            start = result.Length;
            result.Append("[VSDK");
            if (!string.IsNullOrEmpty(category))
            {
                result.Append($" {category}");
            }
            result.Append("] ");
            WrapWithCallingLink(result, start);

            // Append the actual log
            result.Append(log);

            // Final log append
            OnPreLog?.Invoke(result, logCategory, logType);

            // Log
            switch (logType)
            {
                case LogType.Error:
                    UnityEngine.Debug.LogError(result);
                    break;
                case LogType.Warning:
                    UnityEngine.Debug.LogWarning(result);
                    break;
                default:
                    UnityEngine.Debug.Log(result);
                    break;
            }
        }

        /// <summary>
        /// Determines a category from the script name that called the previous method
        /// </summary>
        /// <returns>Assembly name</returns>
        private static string GetCallingCategory()
        {
            StackTrace stackTrace = new StackTrace();
            string path = stackTrace.GetFrame(3).GetMethod().DeclaringType.ToString();
            int index = path.LastIndexOf('.');
            if (index != -1)
            {
                path = path.Substring(index + 1);
            }
            index = path.IndexOf("+<");
            if (index != -1)
            {
                path = path.Substring(0, index);
            }
            return path;
        }

        /// <summary>
        /// Determines a category from the script name that called the previous method
        /// </summary>
        /// <returns>Assembly name</returns>
        private static void WrapWithCallingLink(StringBuilder builder, int startIndex)
        {
            #if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            StackTrace stackTrace = new StackTrace(true);
            StackFrame stackFrame = stackTrace.GetFrame(3);
            string callingFileName = stackFrame.GetFileName().Replace('\\', '/');
            int callingFileLine = stackFrame.GetFileLineNumber();
            builder.Insert(startIndex, $"<a href=\"{callingFileName}\" line=\"{callingFileLine}\">");
            builder.Append("</a>");
            #endif
        }

        /// <summary>
        /// Get hex value for each log type
        /// </summary>
        private static void WrapWithLogColor(StringBuilder builder, int startIndex, LogType logType)
        {
            #if UNITY_EDITOR
            string hex;
            switch (logType)
            {
                case LogType.Error:
                    hex = "FF0000";
                    break;
                case LogType.Warning:
                    hex = "FFFF00";
                    break;
                default:
                    hex = "00FF00";
                    break;
            }
            builder.Insert(startIndex, $"<color=\"#{hex}\">");
            builder.Append("</color>");
            #endif
        }
    }
}
