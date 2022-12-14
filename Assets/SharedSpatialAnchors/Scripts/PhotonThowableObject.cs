using System.Collections;
using System.Collections.Generic;
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
