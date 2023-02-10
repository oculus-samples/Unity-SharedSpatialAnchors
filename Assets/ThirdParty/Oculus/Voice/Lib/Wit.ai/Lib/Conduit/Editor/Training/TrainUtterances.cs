/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Net;
using Meta.Conduit.Editor.Training;

namespace Meta.Conduit.Editor
{
    internal class TrainUtterances : ProcessStep
    {
        public TrainUtterances(WitHttp witHttp, Manifest manifest, Payload payload) : base("Train utterances", witHttp, manifest, payload)
        {
        }

        public override IEnumerator Run(Action<String, float> updateProgress, StepResult completionCallback)
        {
            Payload.Error = "";
            yield return this.WitHttp.MakeUnityWebRequest("/utterances", WebRequestMethods.Http.Post, Payload.Data, completionCallback);
        }
    }
}
