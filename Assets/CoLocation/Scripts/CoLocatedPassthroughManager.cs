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
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public enum CoLocAvatarVisualization
{
    AlwaysPassthrough,
    DistancePassthrough,
    MomentumPassthrough,
    None
}

public class CoLocatedPassthroughManager : MonoBehaviour
{
    public static CoLocatedPassthroughManager Instance;
    [Space]
    [Header("Local User")]
    public GameObject passthroughSphere;
    public Transform localHead;
    public Transform localLeft;
    public Transform localRight;
    private Vector3 prevHeadPos, prevRightPos, prevLeftPos;
    [HideInInspector]
    public float localMomentum;
    [HideInInspector]
    public bool localized = false;
    [Space]
    [Tooltip("The inner distance at which the user will always be visible if not using momentum")]
    public float distNear = 0.5f;
    [Tooltip("The outer distance at which the user will never be visible if not using momentum")]
    public float distFar = 2f;
    [Tooltip("How should the other users be shown?")]
    public CoLocAvatarVisualization visualization = CoLocAvatarVisualization.MomentumPassthrough;
    [Space]
    [Header("CoLocated User Object")]
    [SerializeField] private AvatarPassthrough avatarPrefab;
    public string location = "A";
    public DirectionalPassthrough directionalPassthroughPrefab;
    public bool directional = false;
    [HideInInspector]
    public float centerAngle = 20f, wideAngle = 150f, nearDistance = 1f, farDistance = 1.5f, multiplier = 2.8f, feather = 0.3f;

    private List<AvatarPassthrough> localPassthroughCutouts = new List<AvatarPassthrough>();

    private void Awake()
    {
        Instance = this;
        localized = false;
    }

    public AvatarPassthrough AddCoLocalUser(Transform head, Transform left, Transform right)
    {
        AvatarPassthrough newAvatar = Instantiate(avatarPrefab);
        newAvatar.SetTrackedObjects(head, left, right, this);
        localPassthroughCutouts.Add(newAvatar);
        DirectionalPassthrough newDirectional = Instantiate(directionalPassthroughPrefab);
        newDirectional.Init(head, left, right);
        return newAvatar;
    }

    public void RemoveCoLocalUser(Transform head)
    {
        for (int i = 0; i < localPassthroughCutouts.Count; i++)
        {
            if (localPassthroughCutouts[i] && localPassthroughCutouts[i].CheckHead(head))
            {
                Destroy(localPassthroughCutouts[i].gameObject);
                localPassthroughCutouts.Remove(localPassthroughCutouts[i]);
            }
        }
    }

    public void InitSelf(Transform head, Transform left, Transform right)
    {
        this.localHead = head;
        this.localLeft = left;
        this.localRight = right;
    }

    void Update()
    {
        UpdateMomentum();
    }

    private void UpdateMomentum()
    {
        localMomentum *= 0.975f;
        localMomentum += (localHead.position - prevHeadPos).magnitude * Time.deltaTime * 2;
        prevHeadPos = localHead.position;
        localMomentum += (localRight.position - prevRightPos).magnitude * Time.deltaTime;
        prevRightPos = localRight.position;
        localMomentum += (localLeft.position - prevLeftPos).magnitude * Time.deltaTime;
        prevLeftPos = localLeft.position;
    }

    public void SessionStart()
    {
        passthroughSphere.SetActive(false);
        localized = true;
        for (int i = 0; i < localPassthroughCutouts.Count; i++)
        {
            localPassthroughCutouts[i].localized = true;
        }
    }

    public void NextVisualization()
    {
        int i = (int)visualization;
        i++;
        if (i > 2)
        {
            i = 0;
        }
        visualization = (CoLocAvatarVisualization)i;
    }
}
