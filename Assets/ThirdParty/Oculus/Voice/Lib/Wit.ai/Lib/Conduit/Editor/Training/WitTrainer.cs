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

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Trains Wit.ai with data from the app.
    /// Wit.ai API documentation: https://wit.ai/docs/http/20210928/
    /// </summary>
    internal class WitTrainer
    {
        private readonly string debugOutputPath;
        private readonly string witDataPath;
        private readonly WitHttp witHttp;
        private readonly StepRunner stepRunner = new StepRunner();

        public WitTrainer(string debugOutputPath, string witDataPath, WitHttp witHttp)
        {
            this.debugOutputPath = debugOutputPath;
            this.witDataPath = witDataPath;
            this.witHttp = witHttp;
        }

        /// <summary>
        /// Train Wit.ai given the supplied manifest
        /// </summary>
        public IEnumerator Train(Manifest manifest, Action<String, float> updateProgress, bool skipAdm, bool skipWit, bool useCachedAdmResponse, StepResult completionCallback)
        {
            updateProgress("Preparing training plan", 0);
            var trainingSteps = GetTrainingSteps(manifest, skipAdm, skipWit, useCachedAdmResponse);
            yield return stepRunner.ExecuteTrainingSteps(trainingSteps, updateProgress, completionCallback);
        }

        private List<IProcessStep> GetTrainingSteps(Manifest manifest, bool skipAdm, bool skipWit, bool useCachedAdmResponse)
        {
            Payload payload = new Payload();

            var trainingSteps = new List<IProcessStep>();

            if (!skipWit)
            {
                trainingSteps.Add(new EnsureWitIntentsStep(witHttp, manifest, payload));
                trainingSteps.Add(new SyncWitEntitiesStep(witHttp, manifest, payload));
            }

            trainingSteps.Add(new ObtainIdmData(witHttp, manifest, payload, skipAdm, skipWit, useCachedAdmResponse,
                    debugOutputPath, witDataPath));

            if (!skipWit)
            {
                trainingSteps.Add(new TrainUtterances(witHttp, manifest, payload));
            }

            return trainingSteps;
        }
    }
}
