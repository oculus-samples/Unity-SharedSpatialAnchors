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
using Oculus.Interaction;

public class PhotonThowableObject : PhotonGrabbableObject
{
    private Transform trackingSpace;

    private void Start()
    {
        GameObject trackingSpaceObj = GameObject.Find("TrackingSpace");
        if (trackingSpaceObj)
            trackingSpace = trackingSpaceObj.transform;
    }

    override public void OnPointerEventRaised(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                if (_grabbable.SelectingPointsCount == 1)
                {
                    SampleController.Instance.Log("Grabbable object grabbed");

                    TransferOwnershipToLocalPlayer();
                }
                break;
            case PointerEventType.Unselect:
                if (_grabbable.SelectingPointsCount == 0)
                {
                    SampleController.Instance.Log("Grabbable object ungrabbed");

                    if (trackingSpace != null)
                    {
                        Rigidbody objectRigidbody = GetComponent<Rigidbody>();

                        Vector3 vRightVelocity = trackingSpace.rotation * OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch);
                        Vector3 vLeftVelocity = trackingSpace.rotation * OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch);

                        if (vRightVelocity.magnitude > vLeftVelocity.magnitude)
                        {
                            objectRigidbody.velocity = vRightVelocity;
                            objectRigidbody.angularVelocity = OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.RTouch);
                        }
                        else
                        {
                            objectRigidbody.velocity = vLeftVelocity;
                            objectRigidbody.angularVelocity = OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.LTouch);
                        }
                    }
                }
                break;
        }
    }
}
