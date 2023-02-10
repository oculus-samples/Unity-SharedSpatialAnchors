/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Conduit;
using Meta.Conduit.Editor.Training;
using Meta.WitAi.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.Conduit.Editor
{
    internal class ObtainIdmData : ProcessStep
    {
        private const string IdmEndpoint = "http://[2401:db00:eef0:1120:3520:0:3801:9569]:8085/";

        private readonly bool skipIdm;
        private readonly bool useCachedIdmResponse;
        private readonly string debugOutputPath;
        private readonly string witDataPath;
        private readonly bool skipWit;
        public ObtainIdmData(WitHttp witHttp, Manifest manifest, Payload payload, bool skipIdm, bool skipWit, bool useCachedIdmResponse, string debugOutputPath, string witDataPath) : base("Obtain IDM payload", witHttp, manifest, payload)
        {
            this.skipIdm = skipIdm;
            this.useCachedIdmResponse = useCachedIdmResponse;
            this.debugOutputPath = debugOutputPath;
            this.witDataPath = witDataPath;
            this.skipWit = skipWit;
        }

        public override IEnumerator Run(Action<string, float> updateProgress, StepResult completionCallback)
        {
            updateProgress("Obtaining IDM payload", 0.1f);
            Payload.Error = "";
            if (!skipIdm)
            {
                string outputData = "";
                if (useCachedIdmResponse)
                {
                    Debug.LogError("Skipping IDM request by design for debugging");
                    Payload.Data = File.ReadAllText(debugOutputPath);
                }
                else
                {
                    updateProgress("Querying IDM service", 0.2f);

                    bool dataObtained = false;
                    yield return TryGetIdmTrainingData(this.Manifest, (success, data) =>
                    {
                        dataObtained = success;
                        outputData = data;
                    });

                    if (!dataObtained)
                    {
                        Payload.Error = $"Failed to obtain IDM data. Error: {outputData}";
                        completionCallback(false, Payload.Error);
                        yield break;
                    }

                    File.WriteAllText(debugOutputPath, Payload.Data);
                }

                updateProgress("Deserializing payload", 0.6f);
                var parsedTrainingData = JsonConvert.DeserializeObject<List<WitTrainingUtterance>>(Payload.Data);

                updateProgress("Transforming payload", 0.7f);
                TransformIdmPayload(ref parsedTrainingData);

                updateProgress("Serializing payload", 0.8f);
                outputData = JsonConvert.SerializeObject(parsedTrainingData);

                updateProgress("Persisting payload", 0.9f);
                File.WriteAllText(witDataPath, outputData);
                if (skipWit)
                {
                    // If we are skipping wit, then we're likely debugging the IDM step. Open it up
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(witDataPath, 1);
                }

                Payload.Data = outputData;
            }
            else
            {
                updateProgress("Loading cached payload", 0.9f);
                Payload.Data = File.ReadAllText(witDataPath);
            }

            updateProgress("Persisting payload", 1f);
            Payload.Error = "";
            completionCallback(true, Payload.Error);
        }

        /// <summary>
        /// Transforms the IDM payload into a format that Wit.ai accepts
        /// </summary>
        /// <param name="utteranceTrainingData"></param>
        private void TransformIdmPayload(ref List<WitTrainingUtterance> utteranceTrainingData)
        {
            foreach (var utterance in utteranceTrainingData)
            {
                foreach (var entity in utterance.Entities)
                {
                    var entityComponents = entity.Entity.Split(':');
                    var type = entityComponents[0];
                    var role = entityComponents[1];

                    // Until the IDM service can maintain the case, we need to update the type manually to match.
                    // TODO: Do this more efficiently if we keep it.
                    var manifestType = this.Manifest.Entities.Find(p =>
                        p.Name.Equals(type, StringComparison.InvariantCultureIgnoreCase));

                    if (manifestType != null)
                    {
                        if (manifestType.Name != type)
                        {
                            Debug.Log($"Transformed type {type} to {manifestType.Name}");
                            type = manifestType.Name;
                        }
                    }

                    // TODO: This only works for a single method.
                    var method = this.Manifest.GetInvocationContexts(utterance.Intent).First().MethodInfo;
                    var qualifiedRole = $"{method.DeclaringType.FullName}_{method.Name}_{role}";
                    entity.Entity = $"{type}:{qualifiedRole}";
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="manifest"></param>
        /// <param name="completionCallback"></param>
        /// <returns></returns>
        private IEnumerator TryGetIdmTrainingData(Manifest manifest, StepResult completionCallback)
        {
            var manifestJson = JsonConvert.SerializeObject(manifest);
            var escapedManifest = WebUtility.UrlEncode(manifestJson).Replace(@"\", @"\\").Replace(@"""", @"\""")
                .Replace("%22", "%5C\"");

            var configs = "\"%2C\"config_type\"%3A1%2C\"config_value\"%3A\"\"";
            var url = $"{IdmEndpoint}?manifest={escapedManifest}{configs}";
            Debug.Log($"IDM URL: {url}");
            File.WriteAllText(debugOutputPath+".url", $"{url}");
            HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create(url);

            httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip;
            httpWebRequest.Method = WebRequestMethods.Http.Get;

            using (var webRequest = WitHttp.CreateUnityWebRequest(url, WebRequestMethods.Http.Get))
            {
                yield return webRequest.SendWebRequest();
                #if UNITY_2020_1_OR_NEWER
                if (webRequest.result != UnityWebRequest.Result.Success)
                #else
                if (!string.IsNullOrEmpty(webRequest.error))
                #endif
                {
                    completionCallback(false, $"Failed to get IDM data. Error: {webRequest.error}");
                    yield break;
                }

                var response = webRequest.downloadHandler.text;
                var output = CleanupIdmResponse(response);

                Debug.Log($"IDM Data: {output}");
                completionCallback(true, output);
            }
        }

        // TODO: Once IDM service returns data in the correct form, remove this.
        private string CleanupIdmResponse(string rawResponse)
        {
            // Clean up service additional characters
            rawResponse = rawResponse.TrimStart('b').TrimStart('\'').TrimEnd('\'').Replace(@"\n", "").Replace(@"\\", @"\");

            var idmContent = JsonConvert.DeserializeToken(rawResponse);
            var nlu = idmContent["nlu"];
            var innerJson = nlu["model_response"]["serialized_model_json"].ToString();
            var cleanJson = innerJson.Replace(@"\", "");
            var inner = JsonConvert.SerializeToken(cleanJson);
            var wits = inner["WIT"].AsArray;

            var sb = new StringBuilder();
            sb.Append('[');
            var first = true;
            foreach(WitResponseNode wit in wits)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;

                // Temporary workarounds for service issues
                wit["text"] = wit["text"].ToString().TrimEnd();
                var entities = wit["entities"];
                foreach (WitResponseNode entity in entities.AsArray)
                {
                    entity["entity"] = entity["entity"].ToString().ToLower();
                }

                sb.Append(wit);
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
