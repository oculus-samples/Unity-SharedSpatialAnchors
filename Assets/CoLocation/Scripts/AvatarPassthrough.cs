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

public class AvatarPassthrough : MonoBehaviour
{
    public string location;
    [SerializeField] private SoftPassthroughDot passthroughDotPrefab;
    private List<SoftPassthroughDot> passthroughDots;
    public Transform head, left, right;
    private CoLocatedPassthroughManager manager;

    public float momentum = 0;
    public float updatePassthroughAlpha = 0;
    private Vector3 prevHeadPos, prevRightPos, prevLeftPos;
    private int bodyPoints = 8;
    public bool IsMine = false;
    public bool dots = false;
    private float inflation = 0.5f;
    private float darken = 0.5f;
    public bool localized = false;

    public Material mat;

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private int[] tris = new int[(3 + 5 * 2) * 6];
    private Vector3[] verts = new Vector3[10];
    private Vector2[] uvs = new Vector2[10];
    private Transform cam;

    private void Awake()
    {
        if(meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            mesh = new Mesh();
            meshFilter.mesh = mesh;
            cam = Camera.main.transform;
            meshRenderer.material = mat;
        }
        localized = false;
    }

    public void SetTrackedObjects(Transform head, Transform left, Transform right, CoLocatedPassthroughManager manager)
    {
        this.head = head;
        this.left = left;
        this.right = right;
        this.manager = manager;
        transform.SetParent(manager.transform);
        InitDots();
    }

    public bool CheckHead(Transform checkHead)
    {
        return head == checkHead;
    }

    private void InitDots()
    {
        passthroughDots = new List<SoftPassthroughDot>();
        for (int i = 0; i < 7 + bodyPoints; i++)
        {
            passthroughDots.Add(Instantiate(passthroughDotPrefab));
            passthroughDots[i].Init(manager.localHead, transform);
        }
        Update();
    }

    private void Update()
    {
        if (CheckStatus())
        {
            if(dots)
            {
                SetDotPositions();
            }
            else
            {
                SetQuadPos();
            }
            SetAlpha();
        }
    }

    private bool CheckStatus()
    {
        if (head == null || right == null || left == null)
        {
            //the user object that we are trying to follow has been destroyed.  We should assume that the user has disconnected
            Destroy(gameObject);
            return false;
        }
        if (location != CoLocatedPassthroughManager.Instance.location || IsMine || !localized)
        {
            //the user is local
            SetValue(0);
            return false;
        }
        return true;
    }

    private void SetDotPositions()
    {
        //head
        passthroughDots[0].Pos(head.position + head.up * 0.1f);
        //right shoulder
        Vector3 rightShoulder = head.position - head.up * 0.15f + head.right * 0.2f;
        passthroughDots[1].Pos(rightShoulder);
        //left shoulder
        Vector3 leftShoulder = head.position - head.up * 0.15f - head.right * 0.2f;
        passthroughDots[2].Pos(leftShoulder);
        //right hand
        passthroughDots[3].Pos(right.position);
        //left hand
        passthroughDots[4].Pos(left.position);
        //right elbow
        float dist = Vector3.Distance(right.position, rightShoulder);
        passthroughDots[5].Pos((right.position + rightShoulder) / 2 - Vector3.up * Mathf.Max(0, ((0.6f - dist) / 2)));
        //left elbow
        dist = Vector3.Distance(left.position, leftShoulder);
        passthroughDots[6].Pos((left.position + leftShoulder) / 2 - Vector3.up * Mathf.Max(0, ((0.6f - dist) / 2)));
        //torso to floor
        Vector3 floorPos = new Vector3(head.position.x, 0, head.position.z);
        for (int i = 0; i < bodyPoints; i += 2)
        {
            float height = Mathf.Max(0, (head.position.y - 0.1f) / 8 * i);
            passthroughDots[7 + i].Pos(floorPos + Vector3.up * height + head.right * 0.15f);
            passthroughDots[8 + i].Pos(floorPos + Vector3.up * height - head.right * 0.15f);
        }
    }

    private void SetAlpha()
    {
        if (manager.visualization == CoLocAvatarVisualization.None)
        {
            SetValue(0);
        }
        else
        {
            float dist = Vector3.Distance(
                new Vector3(manager.localHead.position.x, 0, manager.localHead.position.z),
                new Vector3(head.position.x, 0, head.position.z));
            updatePassthroughAlpha = manager.visualization == CoLocAvatarVisualization.MomentumPassthrough ?
                (((momentum + manager.localMomentum) * 25) / dist / dist) :
                Mathf.Clamp01(1 + (manager.distNear - dist) / (manager.distFar - manager.distNear));
            updatePassthroughAlpha = manager.visualization == CoLocAvatarVisualization.AlwaysPassthrough ? 0.75f : updatePassthroughAlpha;
            SetValue(updatePassthroughAlpha);
        }
        momentum *= 0.975f;
        momentum += (head.position - prevHeadPos).magnitude * Time.deltaTime * 2;
        prevHeadPos = head.position;
        momentum += (right.position - prevRightPos).magnitude * Time.deltaTime;
        prevRightPos = right.position;
        momentum += (left.position - prevLeftPos).magnitude * Time.deltaTime;
        prevLeftPos = left.position;
    }

    private void SetValue(float alpha)
    {
        if(dots)
        {
            SetDotValue(alpha);
        }
        else
        {
            SetQuadAlpha(alpha);
        }
    }

    private void SetDotValue(float alpha)
    {
        foreach (var dot in passthroughDots)
        {
            dot.UpdateAlpha(alpha, false);
        }
    }

    private void SetQuadPos()
    {
        Vector3 foot1 = new Vector3(head.position.x, 0, head.position.z) + new Vector3(cam.right.x, 0, cam.right.z).normalized * 0.1f;
        Vector3 foot2 = new Vector3(head.position.x, 0, head.position.z) - new Vector3(cam.right.x, 0, cam.right.z).normalized * 0.1f;

        Vector3 chestR = new Vector3(head.position.x, right.position.y, head.position.z);
        Vector3 chestL = new Vector3(head.position.x, left.position.y, head.position.z);

        //inner points
        verts[0] = foot1;
        verts[1] = foot2;
        verts[2] = head.position;
        verts[3] = right.position;
        verts[4] = left.position;

        for(int j = 0; j < 10; j++)
        {
            uvs[j] = j >= 5 ? Vector2.zero : Vector2.one * darken;
        }

        //outer points
        verts[5] = verts[0] + (- Vector3.up + new Vector3(cam.right.x, 0, cam.right.z).normalized).normalized * inflation;
        verts[6] = verts[1] + (- Vector3.up - new Vector3(cam.right.x, 0, cam.right.z).normalized).normalized * inflation;
        verts[7] = verts[2] + Vector3.up * inflation;
        verts[8] = verts[3] + (right.position - chestR).normalized * inflation;
        verts[9] = verts[4] + (left.position - chestL).normalized * inflation;

        //front face
        //torso
        int i = 0;
        AddTri(ref i, 0, 1, 2);
        AddTri(ref i, 2, 3, 0);
        AddTri(ref i, 4, 2, 1);

        AddQuad(ref i, 0, 1);
        AddQuad(ref i, 4, 1);
        AddQuad(ref i, 4, 2);
        AddQuad(ref i, 2, 3);
        AddQuad(ref i, 0, 3);

        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
    }

    private void AddTri(ref int i, int a, int b, int c)
    {
        //front
        tris[i] = a;
        i++;
        tris[i] = b;
        i++;
        tris[i] = c;
        i++;

        //back
        tris[i] = b;
        i++;
        tris[i] = a;
        i++;
        tris[i] = c;
        i++;
    }

    private void AddQuad(ref int i, int a, int b)
    {
        AddTri(ref i, a, a + 5, b);
        AddTri(ref i, b, a + 5, b + 5);
    }

    private void SetQuadAlpha(float alpha)
    {
        meshRenderer.material.SetFloat("_Alpha", alpha);
    }
}
