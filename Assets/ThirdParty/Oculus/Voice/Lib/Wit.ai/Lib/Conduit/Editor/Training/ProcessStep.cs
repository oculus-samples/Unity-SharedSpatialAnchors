/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;

namespace Meta.Conduit.Editor.Training
{
    internal abstract class ProcessStep : IProcessStep
    {
        protected readonly IWitHttp WitHttp;
        protected readonly Manifest Manifest;

        public string StepName { get; }

        /// <summary>
        /// Executes the step asynchronously and provide progress updates via the callback. When done will call the completion callback.
        /// </summary>
        /// <param name="updateProgress">Will be called with status updates. The first parameter will hold the text status and second will hold the completion percentage between 0 and 1.</param>
        /// <param name="completionCallback">Called when this step concludes.</param>
        /// <returns></returns>
        public abstract IEnumerator Run(Action<String, float> updateProgress, StepResult completionCallback);
        public Payload Payload { get; }

        protected ProcessStep(string stepName, IWitHttp witHttp, Manifest manifest, Payload payload)
        {
            this.StepName = stepName;
            this.WitHttp = witHttp;
            this.Manifest = manifest;
            this.Payload = payload;
        }
    }
}
