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

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class AlignPlayer : MonoBehaviour
{
    [SerializeField]
    public UnityEvent onAlign;

    [SerializeField]
    private Transform player;
    [SerializeField]
    private Transform playerHands;

    public static AlignPlayer Instance;
    private SharedAnchor _currentAlignmentAnchor;
    private CachedSharedAnchor _currentCachedAlignmentAnchor;
    private Coroutine _realignCoroutine;

    private void Awake()
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

    public void AlignToCachedAnchor(CachedSharedAnchor anchor)
    {
        if (_realignCoroutine != null)
        {
            StopCoroutine(_realignCoroutine);
        }

        if(anchor)
            _realignCoroutine = StartCoroutine(AlignToCachedAnchorRoutine(anchor));
    }

    private IEnumerator AlignToCachedAnchorRoutine(CachedSharedAnchor anchor)
    {
        if (_currentCachedAlignmentAnchor != null)
        {
            player.position = Vector3.zero;
            player.eulerAngles = Vector3.zero;

            yield return null;
        }

        var anchorTransform = anchor.transform;

        if (player)
        {
            player.position = anchorTransform.InverseTransformPoint(Vector3.zero);
            player.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);
        }

        if (playerHands)
        {
            playerHands.localPosition = -player.position;
            playerHands.localEulerAngles = -player.eulerAngles;
        }

        _currentCachedAlignmentAnchor = anchor;

        SampleController.Instance.Log("AlignToCachedAnchorRoutine: finished alignment!");

        onAlign?.Invoke();
    }

    public void SetAlignmentAnchor(SharedAnchor anchor)
    {
        if (_realignCoroutine != null)
        {
            StopCoroutine(_realignCoroutine);
        }

        if(anchor)
            _realignCoroutine = StartCoroutine(RealignRoutine(anchor));
    }

    private IEnumerator RealignRoutine(SharedAnchor anchor)
    {
        if (_currentAlignmentAnchor != null)
        {
            _currentAlignmentAnchor.IsSelectedForAlign = false;

            player.position = Vector3.zero;
            player.eulerAngles = Vector3.zero;

            yield return null;
        }

        var anchorTransform = anchor.transform;

        if (player)
        {
            player.position = anchorTransform.InverseTransformPoint(Vector3.zero);
            player.eulerAngles = new Vector3(0, -anchorTransform.eulerAngles.y, 0);
        }

        if (playerHands)
        {
            playerHands.localPosition = -player.position;
            playerHands.localEulerAngles = -player.eulerAngles;
        }

        anchor.IsSelectedForAlign = true;
        _currentAlignmentAnchor = anchor;

        SampleController.Instance.Log("RealignRoutine: finished alignment!");

        onAlign?.Invoke();
    }
}
