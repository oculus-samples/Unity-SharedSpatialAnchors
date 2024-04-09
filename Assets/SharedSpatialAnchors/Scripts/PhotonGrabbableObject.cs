/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;
using Photon.Pun;
using Oculus.Interaction;

public class PhotonGrabbableObject : MonoBehaviour
{
    protected Grabbable _grabbable;
    private PhotonView _photonView;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _photonView = GetComponent<PhotonView>();
    }

    private void OnEnable()
    {
        _grabbable.WhenPointerEventRaised += OnPointerEventRaised;
    }

    private void OnDisable()
    {
        _grabbable.WhenPointerEventRaised -= OnPointerEventRaised;
    }

    virtual public void OnPointerEventRaised(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                if (_grabbable.SelectingPointsCount == 1)
                {
                    if(Debug.isDebugBuild)
                        Debug.Log("Grabbable object grabbed");
                    
                    TransferOwnershipToLocalPlayer();
                }
                break;
            case PointerEventType.Unselect:
                if (_grabbable.SelectingPointsCount == 0)
                {
                    if (Debug.isDebugBuild)
                        Debug.Log("Grabbable object ungrabbed");
                }
                break;
        }
    }

    public void TransferOwnershipToLocalPlayer()
    {
        if (_photonView.Owner != PhotonNetwork.LocalPlayer)
        {
            SampleController.Instance.Log("TransferOwnershipToLocalPlayer: changing photon ownership of " + gameObject.name + " to local player.");
            
            _photonView.TransferOwnership(PhotonNetwork.LocalPlayer);
        }
    }
}
