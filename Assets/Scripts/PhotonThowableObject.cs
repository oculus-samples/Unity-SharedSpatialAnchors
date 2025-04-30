// Copyright (c) Meta Platforms, Inc. and affiliates.

using Oculus.Interaction;

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhotonThowableObject : PhotonGrabbableObject
{
    [SerializeField]
    Rigidbody m_Physics;

    Transform m_TrackingSpace;


    protected override void OnValidate()
    {
        base.OnValidate();
        if (!TryGetComponent(out m_Physics))
            Debug.LogError($"Missing {nameof(Rigidbody)} component here!", this);
    }

    void Start()
    {
        GameObject trackingSpaceObj = GameObject.Find("TrackingSpace");
        if (trackingSpaceObj)
            m_TrackingSpace = trackingSpaceObj.transform;
    }

    protected override void OnPointerEventRaised(PointerEvent pointerEvent)
    {
        base.OnPointerEventRaised(pointerEvent);

        if (pointerEvent.Type != PointerEventType.Unselect || m_Grabbable.SelectingPointsCount > 0 || !m_TrackingSpace)
            return;

        var vRightVelocity = m_TrackingSpace.rotation * OVRInput.GetLocalControllerVelocity(OVRInput.Controller.RTouch);
        var vLeftVelocity = m_TrackingSpace.rotation * OVRInput.GetLocalControllerVelocity(OVRInput.Controller.LTouch);

        if (vRightVelocity.sqrMagnitude > vLeftVelocity.sqrMagnitude)
        {
            m_Physics.velocity = vRightVelocity;
            m_Physics.angularVelocity = OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.RTouch);
        }
        else
        {
            m_Physics.velocity = vLeftVelocity;
            m_Physics.angularVelocity = OVRInput.GetLocalControllerAngularVelocity(OVRInput.Controller.LTouch);
        }
    }
}
