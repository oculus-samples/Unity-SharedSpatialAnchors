// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Exit Games GmbH"/>
// <summary>Demo code for Photon Chat in Unity.</summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------


using UnityEngine;

#if PHOTON_UNITY_NETWORKING
using UnityEngine.UI;
using Photon.Pun;

namespace Photon.Chat.Demo
{
    /// <summary>
    /// This is used in the Editor Splash to properly inform the developer about the chat AppId requirement.
    /// </summary>
    [ExecuteInEditMode]
    public class ChatAppIdCheckerUI : MonoBehaviour
    {
        public Text Description;

        public void Update()
        {
            if (string.IsNullOrEmpty(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdChat))
            {
                if (this.Description != null)
                {
                    this.Description.text = "<Color=Red>WARNING:</Color>\nPlease setup a Chat AppId in the PhotonServerSettings file.";
                }
            }
            else
            {
                if (this.Description != null)
                {
                    this.Description.text = string.Empty;
                }
            }
        }
    }
}

#else

namespace Photon.Chat.Demo
{
    public class ChatAppIdCheckerUI : MonoBehaviour
    {
        // empty class. if PUN is not present, we currently don't check Chat-AppId "presence".
    }
}

#endif
