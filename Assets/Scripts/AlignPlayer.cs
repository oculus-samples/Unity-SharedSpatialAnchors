// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using System.Collections;

using UnityEngine;

using Sampleton = SampleController; // only transitional

public class AlignPlayer : MonoBehaviour
{
    public static AlignPlayer Instance { get; private set; }


    [SerializeField]
    Transform player;
    [SerializeField]
    Transform playerHands;


    SharedAnchor m_CurrentAlignmentAnchor;
    Coroutine m_AlignCoroutine;


    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }
    }

    public void SetAlignmentAnchor(SharedAnchor anchor)
    {
        if (m_AlignCoroutine != null)
        {
            StopCoroutine(m_AlignCoroutine);
            m_AlignCoroutine = null;
        }

        Sampleton.Log($"{nameof(AlignPlayer)}: setting {anchor} as the alignment anchor...");

        if (m_CurrentAlignmentAnchor)
        {
            Sampleton.Log($"{nameof(AlignPlayer)}: unset {m_CurrentAlignmentAnchor} as the alignment anchor.");
            m_CurrentAlignmentAnchor.IsSelectedForAlign = false;
        }

        m_CurrentAlignmentAnchor = null;

        if (player)
        {
            player.SetPositionAndRotation(default, Quaternion.identity);
        }

        if (!anchor || !player)
            return;

        m_AlignCoroutine = StartCoroutine(RealignRoutine(anchor));
    }

    IEnumerator RealignRoutine(SharedAnchor anchor)
    {
        yield return null;

        var anchorTransform = anchor.transform;

        player.position = anchorTransform.InverseTransformPoint(Vector3.zero);
        player.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);

        if (playerHands)
        {
            playerHands.SetLocalPositionAndRotation(
                -player.position,
                Quaternion.Inverse(player.rotation)
            );
        }

        m_CurrentAlignmentAnchor = anchor;
        anchor.IsSelectedForAlign = true;

        Sampleton.Log($"{nameof(AlignPlayer)}: finished alignment -> {anchor}");
        m_AlignCoroutine = null;
    }
}
