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

using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Generates a mesh that represents a plane's boundary.
/// </summary>
/// <remarks>
/// When added to a GameObject that represents a scene entity, such as a floor, ceiling, or desk, this component
/// generates a mesh from its boundary vertices.
/// </remarks>
[RequireComponent(typeof(MeshFilter))]
public unsafe class OVRScenePlaneMeshFilter : MonoBehaviour
{
    private MeshFilter _meshFilter;

    private OVRSceneAnchor _sceneAnchor;

    private Mesh _mesh;

    private JobHandle? _jobHandle;

    private bool _meshRequested;

    private NativeArray<Vector2> _previousBoundary;

    private NativeArray<int> _boundaryLength;

    NativeArray<Vector2> _boundary;

    NativeArray<int> _triangles;

    static readonly ProfilerMarker ComparePreviousBoundaryMarker =
        new ProfilerMarker("Compare previous boundary");


    private void Start()
    {
        _mesh = new Mesh();
        _meshFilter = GetComponent<MeshFilter>();
        _meshFilter.sharedMesh = _mesh;

        _sceneAnchor = GetComponent<OVRSceneAnchor>();
        if (_sceneAnchor)
        {
            _mesh.name = $"{nameof(OVRScenePlaneMeshFilter)} {_sceneAnchor.Uuid}";
            RequestMeshGeneration();
        }
    }

    internal void ScheduleMeshGeneration()
    {
        if (_jobHandle != null) return;

        using (new OVRProfilerScope("Schedule " + nameof(GetBoundaryLengthJob)))
        {
            _boundaryLength = new NativeArray<int>(1, Allocator.TempJob);
            _jobHandle = new GetBoundaryLengthJob
            {
                Space = _sceneAnchor.Space,
                Length = _boundaryLength,
            }.Schedule();
            _meshRequested = false;
        }
    }

    private void Update()
    {
        if (_meshRequested)
        {
            ScheduleMeshGeneration();
        }

        if (_jobHandle?.IsCompleted == true)
        {
            // Even though the job is complete, we have to call Complete() in order
            // to mark the shared arrays as safe to read from.
            _jobHandle.Value.Complete();
            _jobHandle = null;
        }
        else
        {
            return;
        }

        if (_boundaryLength.IsCreated)
        {
            var length = _boundaryLength[0];
            _boundaryLength.Dispose();

            if (length >= 3)
            {
                _boundary = new NativeArray<Vector2>(length, Allocator.TempJob);
                _triangles = new NativeArray<int>((length - 2) * 3, Allocator.TempJob);
                if (!_previousBoundary.IsCreated)
                {
                    _previousBoundary = new NativeArray<Vector2>(length, Allocator.Persistent);
                }

                using (new OVRProfilerScope("Schedule Boundary Jobs"))
                {
                    _jobHandle = new GetBoundaryJob
                    {
                        Space = _sceneAnchor.Space,
                        Boundary = _boundary,
                        PreviousBoundary = _previousBoundary,
                    }.Schedule();

                    _jobHandle = new TriangulateBoundaryJob
                    {
                        Boundary = _boundary,
                        Triangles = _triangles,
                    }.Schedule(_jobHandle.Value);
                }
            }
        }
        else if (_boundary.IsCreated && _triangles.IsCreated)
        {
            try
            {
                if (_triangles[0] == 0 &&
                    _triangles[1] == 0 &&
                    _triangles[2] == 0)
                {
                    return;
                }

                using (new OVRProfilerScope("Copy boundary"))
                {
                    if (_previousBoundary.Length == 0 || float.IsNaN(_previousBoundary[0].x))
                    {
                        if (_previousBoundary.IsCreated)
                        {
                            _previousBoundary.Dispose();
                        }

                        _previousBoundary = new NativeArray<Vector2>(_boundary.Length,
                            Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                        _previousBoundary.CopyFrom(_boundary);
                    }
                }

                using (new OVRProfilerScope("Update mesh"))
                {
                    var vertices = new NativeArray<Vector3>(_boundary.Length, Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    var normals = new NativeArray<Vector3>(_boundary.Length, Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);
                    var uvs = new NativeArray<Vector2>(_boundary.Length, Allocator.Temp,
                        NativeArrayOptions.UninitializedMemory);

                    using (new OVRProfilerScope("Prepare mesh data"))
                    {
                        for (var i = 0; i < _boundary.Length; i++)
                        {
                            var point = _boundary[i];
                            vertices[i] = new Vector3(-point.x, point.y, 0);
                            normals[i] = new Vector3(0, 0, 1);
                            uvs[i] = new Vector2(-point.x, point.y);
                        }
                    }

                    using (vertices)
                    using (normals)
                    using (uvs)
                    using (new OVRProfilerScope("Set mesh data"))
                    {
                        _mesh.Clear();
                        _mesh.SetVertices(vertices);
                        _mesh.SetIndices(_triangles, MeshTopology.Triangles, 0, calculateBounds: true);
                        _mesh.SetNormals(normals);
                        _mesh.SetUVs(0, uvs);
                    }
                }
            }
            finally
            {
                _boundary.Dispose();
                _triangles.Dispose();
            }
        }
    }

    internal void RequestMeshGeneration()
    {
        _meshRequested = true;
        if (enabled)
        {
            ScheduleMeshGeneration();
        }
    }

    void OnDisable()
    {
        // Job completed but we may not yet have consumed the data
        if (_boundary.IsCreated) _boundary.Dispose(_jobHandle ?? default);
        if (_triangles.IsCreated) _triangles.Dispose(_jobHandle ?? default);
        if (_boundaryLength.IsCreated) _boundaryLength.Dispose(_jobHandle ?? default);
        if (_previousBoundary.IsCreated) _previousBoundary.Dispose(_jobHandle ?? default);

        _boundary = default;
        _triangles = default;
        _boundaryLength = default;
        _jobHandle = null;
    }

    private struct GetBoundaryLengthJob : IJob
    {
        public OVRSpace Space;

        [WriteOnly]
        public NativeArray<int> Length;

        public void Execute() => Length[0] = OVRPlugin.GetSpaceBoundary2DCount(Space, out var count)
            ? count
            : 0;
    }

    private struct GetBoundaryJob : IJob
    {
        public OVRSpace Space;

        public NativeArray<Vector2> Boundary;

        public NativeArray<Vector2> PreviousBoundary;

        private bool HasBoundaryChanged()
        {
            using var marker = ComparePreviousBoundaryMarker.Auto();

            if (!PreviousBoundary.IsCreated) return true;
            if (Boundary.Length != PreviousBoundary.Length) return true;

            var length = Boundary.Length;
            for (var i = 0; i < length; i++)
            {
                if (Vector2.SqrMagnitude(Boundary[i] - PreviousBoundary[i]) > 1e-6f) return true;
            }

            return false;
        }

        static void SetNaN(NativeArray<Vector2> array)
        {
            // Set a NaN to indicate failure
            if (array.Length > 0)
            {
                array[0] = new Vector2(float.NaN, float.NaN);
            }
        }

        public void Execute()
        {
            if (OVRPlugin.GetSpaceBoundary2D(Space, Boundary) && HasBoundaryChanged())
            {
                // Invalid old boundary
                SetNaN(PreviousBoundary);
            }
            else
            {
                // Invalid bounday
                SetNaN(Boundary);
            }
        }
    }

    private struct TriangulateBoundaryJob : IJob
    {
        [ReadOnly]
        public NativeArray<Vector2> Boundary;

        [WriteOnly]
        public NativeArray<int> Triangles;

        struct NList : IDisposable
        {
            public int Count { get; private set; }

            NativeArray<int> _data;

            public NList(int capacity, Allocator allocator)
            {
                Count = capacity;
                _data = new NativeArray<int>(capacity, allocator);
                for (var i = 0; i < capacity; i++)
                {
                    _data[i] = i;
                }
            }

            public void RemoveAt(int index)
            {
                --Count;
                for (var i = index; i < Count; i++)
                {
                    _data[i] = _data[i + 1];
                }
            }

            public int GetAt(int index)
            {
                if (index >= Count)
                    return _data[index % Count];

                if (index < 0)
                    return _data[index % Count + Count];

                return _data[index];
            }

            public int this[int index] => _data[index];

            public void Dispose() => _data.Dispose();
        }

        public void Execute()
        {
            if (Boundary.Length == 0 || float.IsNaN(Boundary[0].x)) return;

            var indexList = new NList(Boundary.Length, Allocator.Temp);
            using var disposer = indexList;

            var indexListChanged = true;

            // Find a valid triangle.
            // Checks:
            // 1. Connected edges do not form a co-linear or reflex angle.
            // 2. There's no vertices inside the selected triangle area.
            var triangleCount = 0;
            while (indexList.Count > 3)
            {
                if (!indexListChanged)
                {
                    Debug.LogError(
                        $"[{nameof(OVRScenePlaneMeshFilter)}] Infinite loop while triangulating boundary mesh.");
                    break;
                }

                indexListChanged = false;

                for (var i = 0; i < indexList.Count; i++)
                {
                    var a = indexList[i];
                    var b = indexList.GetAt(i - 1);
                    var c = indexList.GetAt(i + 1);

                    var va = Boundary[a];
                    var vb = Boundary[b];
                    var vc = Boundary[c];

                    var atob = vb - va;
                    var atoc = vc - va;

                    // reflex angle check
                    if (Cross(atob, atoc) >= 0) continue;

                    var validTriangle = true;
                    for (var j = 0; j < Boundary.Length; j++)
                    {
                        if (j == a || j == b || j == c) continue;

                        if (PointInTriangle(Boundary[j], va, vb, vc))
                        {
                            validTriangle = false;
                            break;
                        }
                    }

                    // add indices to triangle list
                    if (!validTriangle) continue;

                    Triangles[triangleCount++] = c;
                    Triangles[triangleCount++] = a;
                    Triangles[triangleCount++] = b;

                    indexList.RemoveAt(i);
                    indexListChanged = true;
                    break;
                }
            }

            Triangles[triangleCount++] = indexList[2];
            Triangles[triangleCount++] = indexList[1];
            Triangles[triangleCount] = indexList[0];
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(b - a, p - a) < 0 &&
            Cross(c - b, p - b) < 0 &&
            Cross(a - c, p - c) < 0;
    }
}
