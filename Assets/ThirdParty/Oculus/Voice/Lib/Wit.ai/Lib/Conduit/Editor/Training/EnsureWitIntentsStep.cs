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
using System.Net;
using Meta.Conduit.Editor.Training;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Adds to Wit.Ai any intents that are found in code but not on Wit.Ai.
    /// </summary>
    internal class EnsureWitIntentsStep : ProcessStep
    {
        private readonly StepRunner stepRunner = new StepRunner();
        public EnsureWitIntentsStep(WitHttp witHttp, Manifest manifest, Payload payload)  : base("Update intents", witHttp, manifest, payload)
        {
        }

        public override IEnumerator Run(Action<String, float> updateProgress, StepResult completionCallback)
        {
            yield return AddMissingIntents(this.Manifest.Actions, updateProgress, completionCallback);
        }

        private IEnumerator AddMissingIntents(List<ManifestAction> actions, Action<String, float> updateProgress, StepResult completionCallback)
        {
            Debug.Log($"Ensuring {actions.Count} actions/intents exist");
            var error = "";

            for (var i = 0; i < actions.Count; i++)
            {
                updateProgress(this.StepName, i / (float) actions.Count);
                var action = actions[i];
                var intent = action.Name.ToLower();
                Debug.Log($"Checking intent {intent}");

                var intentExists = false;
                yield return IntentExists(intent, (success, data) =>  intentExists = success );
                if (intentExists)
                {
                    // TODO: Check if the intent is identical, if not, replace it (need to validate this behavior)
                    Debug.Log($"Intent {intent} already exists. Removing");

                    var removeSuccessful = false;
                    yield return RemoveIntent(intent, (success, data) =>
                    {
                        removeSuccessful = success;
                        error = data;
                    });

                    if (!removeSuccessful)
                    {
                        Debug.LogError($"Failed to remove intent {intent}. Error: {error}");
                        completionCallback(false, $"Failed to remove intent {{intent}}. Error: {error}");
                        yield break;
                    }
                }

                Debug.Log($"Adding intent: {intent}");
                bool intentTrained = false;
                yield return TrainIntent(intent, (success, data) =>
                {
                    intentTrained = success;
                    error = data;
                });

                if (!intentTrained)
                {
                    Debug.LogError($"Failed to train intent {intent}. Error: {error}");
                    completionCallback(false, $"Failed to train intent {intent}. Error: {error}");
                    yield break;
                }
            }

            completionCallback(true, "");
        }

        private IEnumerator TrainIntent(string actionName, StepResult completionCallback)
        {
            var intentData = $"{{\"name\": \"{actionName}\"}}";
            yield return this.WitHttp.MakeUnityWebRequest("/intents", WebRequestMethods.Http.Post, intentData, completionCallback);
        }

        private IEnumerator IntentExists(string intent, StepResult completionCallback)
        {
            yield return this.WitHttp.MakeUnityWebRequest($"/intents/{intent}", WebRequestMethods.Http.Get, completionCallback);
        }

        private IEnumerator RemoveIntent(string intent, StepResult completionCallback)
        {
            yield return this.WitHttp.MakeUnityWebRequest($"/intents/{intent}", "DELETE", completionCallback);
        }
    }
}
