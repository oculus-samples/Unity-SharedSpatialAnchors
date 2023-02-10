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
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace Meta.Conduit.Editor
{
    [Obsolete("Use VRequest instead")]
    internal interface IWitHttp
    {
        HttpWebRequest CreateWebRequest(string uriSection, string method, string body);
        HttpWebRequest CreateWebRequest(string uriSection, string method);
        bool TryGetHttpResponse(HttpWebRequest httpWebRequest, out string response,  [CallerMemberName] string memberName = "");
        UnityWebRequest CreateUnityWebRequest(string uriSection, string method);

        /// <summary>
        /// Makes a Unity web request.
        /// </summary>
        /// <param name="uriSection"></param>
        /// <param name="method"></param>
        /// <param name="completionCallback">First parameter is success or failure and second is response text</param>
        /// <returns>A Coroutine enumerator</returns>
        IEnumerator MakeUnityWebRequest(string uriSection, string method, StepResult completionCallback);

        /// <summary>
        /// Makes a Unity web request.
        /// </summary>
        /// <param name="uriSection"></param>
        /// <param name="method"></param>
        /// <param name="body"></param>
        /// <param name="completionCallback">First parameter is success or failure and second is response text</param>
        /// <returns>A Coroutine enumerator</returns>
        IEnumerator MakeUnityWebRequest(string uriSection, string method, string body, StepResult completionCallback);
    }
}
