/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using Meta.Conduit.Editor;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.Utilities;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.Windows
{
    public class WitConfigurationEditor : Editor
    {
        public WitConfiguration configuration { get; private set; }
        private string _serverToken;
        private string _appName;
        private string _appID;
        private bool _initialized = false;
        public bool drawHeader = true;
        private bool _foldout = true;
        private int _requestTab = 0;
        private bool manifestAvailable = false;

        private static ConduitStatistics _statistics;
        private static readonly AssemblyMiner AssemblyMiner = new AssemblyMiner(new WitParameterValidator());
        private static readonly ManifestGenerator ManifestGenerator = new ManifestGenerator(new AssemblyWalker(), AssemblyMiner);

        // Tab IDs
        protected const string TAB_APPLICATION_ID = "application";
        protected const string TAB_INTENTS_ID = "intents";
        protected const string TAB_ENTITIES_ID = "entities";
        protected const string TAB_TRAITS_ID = "traits";
        private string[] _tabIds = new string[] { TAB_APPLICATION_ID, TAB_INTENTS_ID, TAB_ENTITIES_ID, TAB_TRAITS_ID };

        // Generate
        private static ConduitStatistics Statistics
        {
            get
            {
                if (_statistics == null)
                {
                    _statistics = new ConduitStatistics(new PersistenceLayer());
                }
                return _statistics;
            }
        }

        public virtual Texture2D HeaderIcon => WitTexts.HeaderIcon;
        public virtual string HeaderUrl => WitTexts.GetAppURL(WitConfigurationUtility.GetAppID(configuration), WitTexts.WitAppEndpointType.Settings);
        public virtual string OpenButtonLabel => WitTexts.Texts.WitOpenButtonLabel;

        public void Initialize()
        {
            // Refresh configuration & auth tokens
            configuration = target as WitConfiguration;

            // Get app server token
            _serverToken = WitAuthUtility.GetAppServerToken(configuration);
            if (CanConfigurationRefresh(configuration) && WitConfigurationUtility.IsServerTokenValid(_serverToken))
            {
                // Get client token if needed
                _appID = WitConfigurationUtility.GetAppID(configuration);
                if (string.IsNullOrEmpty(_appID))
                {
                    configuration.SetServerToken(_serverToken);
                }
                // Refresh additional data
                else
                {
                    SafeRefresh();
                }
            }
        }

        public void OnDisable()
        {
            Statistics.Persist();
        }

        public override void OnInspectorGUI()
        {
            // Init if needed
            if (!_initialized || configuration != target)
            {
                Initialize();
                _initialized = true;
            }

            // Draw header
            if (drawHeader)
            {
                WitEditorUI.LayoutHeaderButton(HeaderIcon, HeaderUrl);
                GUILayout.Space(WitStyles.HeaderPaddingBottom);
                EditorGUI.indentLevel++;
            }

            // Layout content
            LayoutContent();

            // Undent
            if (drawHeader)
            {
                EditorGUI.indentLevel--;
            }
        }

        private void LayoutConduitContent()
        {
            // Get full manifest path & ensure it exists
            string manifestPath = configuration.ManifestEditorPath;
            manifestAvailable = File.Exists(manifestPath);

            // Set conduit
            var useConduit = (GUILayout.Toggle(configuration.useConduit, "Use Conduit (Beta)"));
            if (configuration.useConduit != useConduit)
            {
                configuration.useConduit = useConduit;
                EditorUtility.SetDirty(configuration);
            }

            // Auto-generate manifest
            if (configuration.useConduit && !manifestAvailable)
            {
                GenerateManifest(configuration, configuration.openManifestOnGeneration);
            }

            {
                EditorGUI.indentLevel++;
                GUILayout.Space(EditorGUI.indentLevel * WitStyles.ButtonMargin);
                {
                    GUI.enabled = configuration.useConduit;
                    GUILayout.BeginHorizontal();
                    if (WitEditorUI.LayoutTextButton(manifestAvailable ? "Update Manifest" : "Generate Manifest"))
                    {
                        GenerateManifest(configuration, configuration.openManifestOnGeneration);
                    }
                    GUI.enabled = configuration.useConduit && manifestAvailable;
                    if (WitEditorUI.LayoutTextButton("Select Manifest") && manifestAvailable)
                    {
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(configuration.ManifestEditorPath);
                    }
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndDisabledGroup();
        }

        protected virtual void LayoutContent()
        {
            // Begin vertical box
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Check for app name/id update
            ReloadAppData();

            // Title Foldout
            GUILayout.BeginHorizontal();
            string foldoutText = WitTexts.Texts.ConfigurationHeaderLabel;
            if (!string.IsNullOrEmpty(_appName))
            {
                foldoutText = foldoutText + " - " + _appName;
            }

            _foldout = WitEditorUI.LayoutFoldout(new GUIContent(foldoutText), _foldout);
            // Refresh button
            if (CanConfigurationRefresh(configuration))
            {
                if (string.IsNullOrEmpty(_appName))
                {
                    bool isValid =  WitConfigurationUtility.IsServerTokenValid(_serverToken);
                    GUI.enabled = isValid;
                    if (WitEditorUI.LayoutTextButton(WitTexts.Texts.ConfigurationRefreshButtonLabel))
                    {
                        configuration.ResetData();
                        ApplyServerToken(_serverToken);
                    }
                }
                else
                {
                    bool isRefreshing = configuration.IsRefreshingData();
                    GUI.enabled = !isRefreshing;
                    if (WitEditorUI.LayoutTextButton(isRefreshing ? WitTexts.Texts.ConfigurationRefreshingButtonLabel : WitTexts.Texts.ConfigurationRefreshButtonLabel))
                    {
                        SafeRefresh(true);
                    }
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(WitStyles.ButtonMargin);

            // Show configuration app data
            if (_foldout)
            {
                // Indent
                EditorGUI.indentLevel++;

                // Server access token
                bool updated = false;
                WitEditorUI.LayoutPasswordField(WitTexts.ConfigurationServerTokenContent, ref _serverToken, ref updated);
                if (updated && WitConfigurationUtility.IsServerTokenValid(_serverToken))
                {
                    configuration.ResetData();
                    ApplyServerToken(_serverToken);
                }

                // Additional data
                if (configuration)
                {
                    LayoutConfigurationData();
                }

                // Undent
                EditorGUI.indentLevel--;
            }

            // End vertical box layout
            GUILayout.EndVertical();

            GUILayout.BeginVertical(EditorStyles.helpBox);
            LayoutConduitContent();
            GUILayout.EndVertical();

            // Layout configuration request tabs
            LayoutConfigurationRequestTabs();

            // Additional open wit button
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(OpenButtonLabel, WitStyles.TextButton))
            {
                Application.OpenURL(HeaderUrl);
            }
        }
        // Reload app data if needed
        private void ReloadAppData()
        {
            // Check for changes
            string checkName = "";
            string checkID = "";
            if (configuration != null && configuration.application != null)
            {
                checkName = configuration.application.name;
                checkID = configuration.application.id;
            }
            // Reset
            if (!string.Equals(_appName, checkName) || !string.Equals(_appID, checkID))
            {
                _appName = checkName;
                _appID = checkID;
                _serverToken = WitAuthUtility.GetAppServerToken(configuration);
            }
        }
        // Apply server token
        public void ApplyServerToken(string newToken)
        {
            if (newToken != _serverToken)
            {
                _serverToken = newToken;
                configuration.ResetData();
            }

            configuration.SetServerToken(_serverToken);
        }
        // Whether or not to allow a configuration to refresh
        protected virtual bool CanConfigurationRefresh(WitConfiguration configuration)
        {
            return configuration;
        }
        // Layout configuration data
        protected virtual void LayoutConfigurationData()
        {
            // Reset update
            bool updated = false;
            // Client access field
            WitEditorUI.LayoutPasswordField(WitTexts.ConfigurationClientTokenContent, ref configuration.clientAccessToken, ref updated);
            if (updated && string.IsNullOrEmpty(configuration.clientAccessToken))
            {
                Debug.LogError("Client access token is not defined. Cannot perform requests with '" + configuration.name + "'.");
            }
            // Timeout field
            WitEditorUI.LayoutIntField(WitTexts.ConfigurationRequestTimeoutContent, ref configuration.timeoutMS, ref updated);
            // Updated
            if (updated)
            {
                EditorUtility.SetDirty(configuration);
            }

            // Show configuration app data
            LayoutConfigurationEndpoint();
        }
        // Layout endpoint data
        protected virtual void LayoutConfigurationEndpoint()
        {
            // Generate if needed
            if (configuration.endpointConfiguration == null)
            {
                configuration.endpointConfiguration = new WitEndpointConfig();
                EditorUtility.SetDirty(configuration);
            }

            // Handle via serialized object
            var serializedObj = new SerializedObject(configuration);
            var serializedProp = serializedObj.FindProperty("endpointConfiguration");
            EditorGUILayout.PropertyField(serializedProp);
            serializedObj.ApplyModifiedProperties();
        }
        // Tabs
        protected virtual void LayoutConfigurationRequestTabs()
        {
            // Indent
            EditorGUI.indentLevel++;

            // Iterate tabs
            if (_tabIds != null)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < _tabIds.Length; i++)
                {
                    // Enable if not selected
                    GUI.enabled = _requestTab != i;
                    // If valid and clicked, begin selecting
                    string tabPropertyID = _tabIds[i];
                    if (ShouldTabShow(configuration, tabPropertyID))
                    {
                        if (WitEditorUI.LayoutTabButton(GetTabText(configuration, tabPropertyID, true)))
                        {
                            _requestTab = i;
                        }
                    }
                    // If invalid, stop selecting
                    else if (_requestTab == i)
                    {
                        _requestTab = -1;
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            // Layout selected tab using property id
            string propertyID = _requestTab >= 0 && _requestTab < _tabIds.Length ? _tabIds[_requestTab] : string.Empty;
            if (!string.IsNullOrEmpty(propertyID) && configuration != null)
            {
                SerializedObject serializedObj = new SerializedObject(configuration);
                SerializedProperty serializedProp = serializedObj.FindProperty(propertyID);
                if (serializedProp == null)
                {
                    WitEditorUI.LayoutErrorLabel(GetTabText(configuration, propertyID, false));
                }
                else if (!serializedProp.isArray)
                {
                    EditorGUILayout.PropertyField(serializedProp);
                }
                else if (serializedProp.arraySize == 0)
                {
                    WitEditorUI.LayoutErrorLabel(GetTabText(configuration, propertyID, false));
                }
                else
                {
                    for (int i = 0; i < serializedProp.arraySize; i++)
                    {
                        SerializedProperty serializedPropChild = serializedProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(serializedPropChild);
                    }
                }
                serializedObj.ApplyModifiedProperties();
            }

            // Undent
            EditorGUI.indentLevel--;
        }
        // Determine if tab should show
        protected virtual bool ShouldTabShow(WitConfiguration configuration, string tabID)
        {
            if(null == configuration.application ||
                   string.IsNullOrEmpty(configuration.application.id))
            {
                return false;
            }

            switch (tabID)
            {
                case TAB_INTENTS_ID:
                    return null != configuration.intents;
                case TAB_ENTITIES_ID:
                    return null != configuration.entities;
                case TAB_TRAITS_ID:
                    return null != configuration.traits;
            }

            return true;
        }
        // Get tab text
        protected virtual string GetTabText(WitConfiguration configuration, string tabID, bool titleLabel)
        {
            switch (tabID)
            {
                case TAB_APPLICATION_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationApplicationTabLabel : WitTexts.Texts.ConfigurationApplicationMissingLabel;
                case TAB_INTENTS_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationIntentsTabLabel : WitTexts.Texts.ConfigurationIntentsMissingLabel;
                case TAB_ENTITIES_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationEntitiesTabLabel : WitTexts.Texts.ConfigurationEntitiesMissingLabel;
                case TAB_TRAITS_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationTraitsTabLabel : WitTexts.Texts.ConfigurationTraitsMissingLabel;
            }
            return string.Empty;
        }

        protected virtual void SafeRefresh()
        {
            SafeRefresh(false);
        }

        // Safe refresh
        private void SafeRefresh(bool resetData)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (resetData)
            {
                configuration.ResetData();
            }

            if (WitConfigurationUtility.IsServerTokenValid(_serverToken))
            {
                configuration.SetServerToken(_serverToken);
            }
            else if (WitConfigurationUtility.IsClientTokenValid(configuration.clientAccessToken))
            {
                configuration.RefreshData();
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded() {
            foreach (var witConfig in WitConfigurationUtility.WitConfigs)
            {
                if (witConfig.useConduit)
                {
                    GenerateManifest(witConfig, false);
                }
            }
        }

        /// <summary>
        /// Generates a manifest and optionally opens it in the editor.
        /// </summary>
        /// <param name="configuration">The configuration that we are generating the manifest for.</param>
        /// <param name="openManifest">If true, will open the manifest file in the code editor.</param>
        private static void GenerateManifest(WitConfiguration configuration, bool openManifest)
        {
            // Generate
            var startGenerationTime = DateTime.UtcNow;
            var manifest = ManifestGenerator.GenerateManifest(configuration.application.name,
                configuration.application.id);
            var endGenerationTime = DateTime.UtcNow;

            // Get file path
            string fullPath = configuration.ManifestEditorPath;
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                string directory = Application.dataPath + "/Oculus/Voice/Resources";
                IOUtility.CreateDirectory(directory, true);
                fullPath = directory + "/" + configuration.manifestLocalPath;
            }

            // Write to file
            try
            {
                var writer = new StreamWriter(fullPath);
                writer.WriteLine(manifest);
                writer.Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"Wit Configuration Editor - Conduit manifest generation Failed\nPath: {fullPath}\n{e}");
                return;
            }

            Statistics.SuccessfulGenerations++;
            Statistics.AddFrequencies(AssemblyMiner.SignatureFrequency);
            Statistics.AddIncompatibleFrequencies(AssemblyMiner.IncompatibleSignatureFrequency);

            string unityPath = fullPath.Replace(Application.dataPath, "Assets");
            AssetDatabase.ImportAsset(unityPath);

            string configName = configuration.name;
            string manifestName = Path.GetFileNameWithoutExtension(unityPath);
#if UNITY_2021_2_OR_NEWER
            string configPath = AssetDatabase.GetAssetPath(configuration);
            configName = $"<a href=\"{configPath}\">{configName}</a>";
            manifestName = $"<a href=\"{unityPath}\">{manifestName}</a>";
#endif
            Debug.Log($"Ref: {unityPath}");
            var generationTime = endGenerationTime - startGenerationTime;
            Debug.Log($"Wit Configuration Editor - Conduit manifest generated\nConfiguration: {configName}\nManifest: {manifestName}\nGeneration Time: {generationTime.TotalMilliseconds} ms");

            if (openManifest)
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
            }
        }
    }
}
