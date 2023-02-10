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
using System.Threading;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    internal class StepRunner
    {
        public IEnumerator ExecuteTrainingSteps(List<IProcessStep> trainingSteps, Action<String, float> updateProgress, StepResult completionCallback)
        {
            for (var i = 0; i < trainingSteps.Count; i++)
            {
                var step = trainingSteps[i];
                Debug.Log($"Starting step: {step.StepName}");
                var overallProgress =  i/(float)trainingSteps.Count;

                var stepSuccessful = false;
                var lastError = "";
                yield return RunWithRetry(step, (status, nestedProgress) =>
                {
                    updateProgress($"{step.StepName}:{status}", overallProgress + 0.1f * nestedProgress);
                } , (success, data) =>
                {
                    stepSuccessful = success;
                    lastError = data;
                });

                if (!stepSuccessful)
                {
                    completionCallback(false, $"Failed step: {step.StepName}. Error: {lastError}");
                    yield break;
                }

                Debug.Log($"Done: {step.StepName}");
            }

            completionCallback(true, "");
        }

        /// <summary>
        /// Runs a step in the process and repeats it on failure with exponential backoff.
        /// </summary>
        /// <param name="step">The step to execute.</param>
        /// <param name="maxRetries">Number of retries.</param>
        /// <param name="coolDown">The initial cool down. This will be doubled each time.</param>
        /// <returns></returns>
        private IEnumerator RunWithRetry(IProcessStep step, Action<String, float> updateProgress, StepResult completionCallback, int maxRetries = 2, int coolDown = 500)
        {
            string lastError = "";
            while (maxRetries >= 0)
            {
                bool result = false;
                yield return step.Run(updateProgress, (success, data) =>
                {
                    result = success;
                    lastError = data;
                });
                if (result)
                {
                    completionCallback(true, "");
                    step.Payload.Error = "";
                    yield break;
                }

                Debug.Log($"Step {step} failed. Retries remaining: {maxRetries}");

                if (--maxRetries >= 0)
                {
                    Thread.Sleep(coolDown);
                    coolDown *= 2;
                }
            }

            completionCallback(false, $"Failed all retries for step {step}. Last error: {lastError}");
        }
    }
}
