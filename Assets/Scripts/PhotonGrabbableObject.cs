// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using Oculus.Interaction;

using Photon.Pun;

using UnityEngine;

using Sampleton = SampleController; // only transitional

[RequireComponent(typeof(PhotonView), typeof(Grabbable))]
public class PhotonGrabbableObject : MonoBehaviour
{
    //
    // Public interface

    public void TransferOwnershipToLocalPlayer()
    {
        if (m_PhotonView.Owner.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
            return;

        Sampleton.Log($"{nameof(TransferOwnershipToLocalPlayer)}: {gameObject.name} -> local player ({PhotonNetwork.NickName})");
        m_PhotonView.TransferOwnership(PhotonNetwork.LocalPlayer);
    }

    //
    // Fields

    [SerializeField]
    protected PhotonView m_PhotonView;
    [SerializeField]
    protected Grabbable m_Grabbable;

    //
    // MonoBehaviour Messages

    protected virtual void OnValidate()
    {
        if (!TryGetComponent(out m_PhotonView))
            Debug.LogError($"Missing {nameof(PhotonView)} component here!", this);
        if (!TryGetComponent(out m_Grabbable))
            Debug.LogError($"Missing {nameof(Grabbable)} component here!", this);
    }

    void OnEnable()
    {
        m_Grabbable.WhenPointerEventRaised += OnPointerEventRaised;

        // Log position
        var pos = transform.position;
        if (pos.x * pos.x < Vector3.kEpsilonNormalSqrt)
            pos.x = 0f;
        if (pos.y * pos.y < Vector3.kEpsilonNormalSqrt)
            pos.y = 0f;
        if (pos.z * pos.z < Vector3.kEpsilonNormalSqrt)
            pos.z = 0f;
        Sampleton.Log($"+ {name} @ [{pos.x:g3}, {pos.y:g3}, {pos.z:g3}]");
    }

    void OnDisable()
    {
        m_Grabbable.WhenPointerEventRaised -= OnPointerEventRaised;
    }

    //
    // Virtual interface

    protected virtual void OnPointerEventRaised(PointerEvent pointerEvent)
    {
        switch (pointerEvent.Type)
        {
            case PointerEventType.Select:
                if (m_Grabbable.SelectingPointsCount == 1)
                {
                    Debug.Log($"grabbed {this}");
                    TransferOwnershipToLocalPlayer();
                }
                break;
            case PointerEventType.Unselect:
                if (m_Grabbable.SelectingPointsCount == 0)
                {
                    Debug.Log($"dropped {this}");
                }
                break;
        }
    }

}
