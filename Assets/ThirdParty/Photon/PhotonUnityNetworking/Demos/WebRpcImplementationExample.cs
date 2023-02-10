// ----------------------------------------------------------------------------
// <copyright file="WebRpcImplementationExample.cs" company="Exit Games GmbH">
//   PhotonNetwork Framework for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
//  Sample of best practices when implementing & handling WebRPCs.
// </summary>
// <author>developer@exitgames.com</author>
// ----------------------------------------------------------------------------

using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEngine;

namespace Photon.Pun.Demo
{

    /// <summary>
    /// This class is a sample of how to implement WebRPCs calling & callbacks.
    /// </summary>
    public class WebRpcImplementationExample : MonoBehaviour, IWebRpcCallback
    {
        /// <summary>
        /// example of WebRPC method name, add yours as enum or constants to avoid typos and have them in one place
        /// </summary>
        public const string GetGameListWebRpcMethodName = "GetGameList";

        public void OnWebRpcResponse(OperationResponse response)
        {
            Debug.LogFormat("WebRPC operation response {0}", response.ToStringFull());
            switch (response.ReturnCode)
            {
                case ErrorCode.Ok:
                    WebRpcResponse webRpcResponse = new WebRpcResponse(response);
                    Debug.LogFormat("Parsed WebRPC response {0}", response.ToStringFull());
                    if (string.IsNullOrEmpty(webRpcResponse.Name))
                    {
                        Debug.LogError("Unexpected: WebRPC response did not contain WebRPC method name");
                    }
                    if (webRpcResponse.ResultCode == 0) // success
                    {
                        switch (webRpcResponse.Name)
                        {
                            // todo: add your code here
                            case GetGameListWebRpcMethodName: // example
                                // ... 
                                break;
                        }
                    }
                    else if (webRpcResponse.ResultCode == -1)
                    {
                        Debug.LogErrorFormat("Web server did not return ResultCode for WebRPC method=\"{0}\", Message={1}", webRpcResponse.Name, webRpcResponse.Message);
                    }
                    else
                    {
                        Debug.LogErrorFormat("Web server returned ResultCode={0} for WebRPC method=\"{1}\", Message={2}", webRpcResponse.ResultCode, webRpcResponse.Name, webRpcResponse.Message);
                    }
                    break;
                case ErrorCode.ExternalHttpCallFailed: // web service unreachable
                    Debug.LogErrorFormat("WebRPC call failed as request could not be sent to the server. {0}", response.DebugMessage);
                    break;
                case ErrorCode.HttpLimitReached: // too many WebRPCs in a short period of time
                                                 // the debug message should contain the limit exceeded
                    Debug.LogErrorFormat("WebRPCs rate limit exceeded: {0}", response.DebugMessage);
                    break;
                case ErrorCode.InvalidOperation: // WebRPC not configured at all OR not configured properly OR trying to send on name server
                    if (PhotonNetwork.Server == ServerConnection.NameServer)
                    {
                        Debug.LogErrorFormat("WebRPC not supported on NameServer. {0}", response.DebugMessage);
                    }
                    else
                    {
                        Debug.LogErrorFormat("WebRPC not properly configured or not configured at all. {0}", response.DebugMessage);
                    }
                    break;
                default:
                    // other unknown error, unexpected
                    Debug.LogErrorFormat("Unexpected error, {0} {1}", response.ReturnCode, response.DebugMessage);
                    break;
            }
        }

        public void WebRpcExampleCall()
        {
            WebRpcCall(GetGameListWebRpcMethodName);
        }

        public static void WebRpcCall(string methodName, object parameters = null, bool sendAuthCookieIfAny = false)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                Debug.LogError("WebRpc method name must not be null nor empty");
                return;
            }
            if (!PhotonNetwork.WebRpc(methodName, parameters, sendAuthCookieIfAny))
            {
                Debug.LogErrorFormat("Error sending WebRPC \"{0}\" (\"{1}\") request, check the previous error logs for more details", methodName, parameters);
            }
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }
    }
}
