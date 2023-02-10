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

using Oculus.Interaction.DistanceReticles;
using Oculus.Interaction.Input;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oculus.Interaction.Locomotion
{
    public class TeleportProceduralArcVisual : MonoBehaviour
    {
        [SerializeField]
        private TeleportInteractor _interactor;

        [SerializeField, Optional, Interface(typeof(IAxis1D))]
        private MonoBehaviour _progress;
        private IAxis1D Progress;

        [SerializeField]
        private MeshFilter _arcFilter;
        [SerializeField]
        private MeshRenderer _renderer;

        [SerializeField, Min(2)]
        private int _arcPointsCount = 30;
        public int ArcPointsCount
        {
            get
            {
                return _arcPointsCount;
            }
            set
            {
                _arcPointsCount = value;
            }
        }

        [SerializeField]
        private int _divisions = 6;
        public int Divisions
        {
            get
            {
                return _divisions;
            }
            set
            {
                _divisions = value;
            }
        }

        [SerializeField]
        private float _radius = 0.005f;
        public float Radius
        {
            get
            {
                return _radius;
            }
            set
            {
                _radius = value;
            }
        }

        [SerializeField]
        private Gradient _gradient;
        public Gradient Gradient
        {
            get
            {
                return _gradient;
            }
            set
            {
                _gradient = value;
            }
        }

        [SerializeField]
        private Color _noDestinationTint = Color.red;
        public Color NoDestinationTint
        {
            get
            {
                return _noDestinationTint;
            }
            set
            {
                _noDestinationTint = value;
            }
        }

        [SerializeField, Range(0f, 1f)]
        private float _progressFade = 0.2f;
        public float ProgressFade
        {
            get
            {
                return _progressFade;
            }
            set
            {
                _progressFade = value;
            }
        }

        [SerializeField, Range(0f, 1f)]
        private float _endFadeThresold = 0.2f;
        public float EndFadeThresold
        {
            get
            {
                return _endFadeThresold;
            }
            set
            {
                _endFadeThresold = value;
            }
        }

        [SerializeField]
        private bool _mirrorTexture;
        public bool MirrorTexture
        {
            get
            {
                return _mirrorTexture;
            }
            set
            {
                _mirrorTexture = value;
            }
        }

        private ArcPoint[] _arcPoints;

        [StructLayout(LayoutKind.Sequential)]
        private struct VertexLayout
        {
            public Vector3 pos;
            public Color32 color;
            public Vector2 uv;
        }

        private static float[,] MIDPOINT_FACTOR = new float[,]
        {
            { -0.984807753f, 2.86f },
            { -0.8660254038f, 1.02f },
            { -0.6427876097f, 0.66f },
            { -0.3420201433f, 0.54f },
            { 0f, 0.5f },
            { 0.3420201433f, 0.53f },
            { 0.6427876097f, 0.65f },
            { 0.8660254038f, 1f },
            { 0.9396926208f, 1.45f },
            { 0.984807753f, 2.86f },
            { 0.9961946981f, 5.7f },
            { 0.9975640503f, 7.2f },
            { 0.9986295348f, 9.55f },
            { 0.999390827f, 14.31f },
            { 0.9998476952f, 28.65f }
        };

        private VertexAttributeDescriptor[] _dataLayout;
        private NativeArray<VertexLayout> _vertsData;
        private VertexLayout _layout = new VertexLayout();
        private Mesh _mesh;
        private int[] _tris;

        private IReticleData _reticleData;

        protected bool _started;

        protected virtual void Awake()
        {
            Progress = _progress as IAxis1D;
        }

        protected virtual void Start()
        {
            this.BeginStart(ref _started);
            InitializeMeshData();
            this.EndStart(ref _started);
        }

        protected virtual void OnDestroy()
        {
            if (_started)
            {
                _vertsData.Dispose();
            }
        }

        protected virtual void OnEnable()
        {
            if (_started)
            {
                _interactor.WhenPostprocessed += HandleInteractorPostProcessed;
                _interactor.WhenStateChanged += HandleInteractorStateChanged;
                _interactor.WhenInteractableSet.Action += HandleInteractableSet;
                _interactor.WhenInteractableUnset.Action += HandleInteractableUnset;
            }
        }

        protected virtual void OnDisable()
        {
            if (_started)
            {
                _interactor.WhenPostprocessed -= HandleInteractorPostProcessed;
                _interactor.WhenStateChanged -= HandleInteractorStateChanged;
                _interactor.WhenInteractableSet.Action -= HandleInteractableSet;
                _interactor.WhenInteractableUnset.Action -= HandleInteractableUnset;
            }
        }

        private void HandleInteractableSet(TeleportInteractable interactable)
        {
            if (interactable != null)
            {
                _reticleData = interactable.GetComponent<IReticleData>();
            }
        }

        private void HandleInteractableUnset(TeleportInteractable obj)
        {
            _reticleData = null;
        }

        private void HandleInteractorStateChanged(InteractorStateChangeArgs stateChange)
        {
            if (stateChange.NewState == InteractorState.Disabled)
            {
                _renderer.enabled = false;
            }
            else
            {
                _renderer.enabled = true;
            }
        }

        private void HandleInteractorPostProcessed()
        {
            if (_interactor.State == InteractorState.Disabled)
            {
                return;
            }

            Color tint = Color.white;
            if (_interactor.Interactable == null
                || !_interactor.Interactable.AllowTeleport)
            {
                tint = _noDestinationTint;
            }

            Vector3 target = _reticleData != null ?
                _reticleData.ProcessHitPoint(_interactor.ArcEnd.Point) :
                _interactor.ArcEnd.Point;

            UpdateVisualArcPoints(_interactor.ArcOrigin, target);
            UpdateMeshData(_arcPoints, Divisions, Radius, tint);
        }

        private void InitializeMeshData()
        {
            _dataLayout = new VertexAttributeDescriptor[]
            {
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2),
            };

            int steps = _interactor.TeleportArc.ArcPointsCount;
            int vertsCount = SetVertexCount(steps, _divisions);
            _vertsData = new NativeArray<VertexLayout>(vertsCount, Allocator.Persistent);

            _mesh = new Mesh();
            _mesh.SetVertexBufferParams(vertsCount, _dataLayout);
            _mesh.SetTriangles(_tris, 0);
            _arcFilter.mesh = _mesh;
        }

        private ArcPoint[] UpdateVisualArcPoints(Pose origin, Vector3 target)
        {
            float maxDistance = _interactor.TeleportArc.MaxDistance;
            if (_arcPoints == null
                || _arcPoints.Length != ArcPointsCount)
            {
                _arcPoints = new ArcPoint[ArcPointsCount];
            }

            float pitchDot = Vector3.Dot(origin.forward, Vector3.up);
            float controlPointFactor = CalculateMidpointFactor(pitchDot);
            float distance = Vector3.ProjectOnPlane(target - origin.position, Vector3.up).magnitude;
            Vector3 midPoint = origin.position + origin.forward * distance * controlPointFactor;

            Vector3 prevPosition = origin.position - origin.forward;
            Vector3 inverseScale = new Vector3(1f / this.transform.lossyScale.x,
                1f / this.transform.lossyScale.y,
                1f / this.transform.lossyScale.z);
            for (int i = 0; i < ArcPointsCount; i++)
            {
                float t = i / (ArcPointsCount - 1f);
                Vector3 position = EvaluateBezierArc(origin.position, midPoint, target, t);
                Vector3 difference = (position - prevPosition);
                _arcPoints[i].position = Vector3.Scale(position, inverseScale);
                _arcPoints[i].direction = difference.normalized;
                _arcPoints[i].relativeLength = i == 0 ? 0f
                    : _arcPoints[i - 1].relativeLength + (difference.magnitude / maxDistance);
                prevPosition = position;
            }

            return _arcPoints;
        }

        private static Vector3 EvaluateBezierArc(Vector3 start, Vector3 middle, Vector3 end, float t)
        {
            t = Mathf.Clamp01(t);
            float oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * start)
                + (2f * oneMinusT * t * middle)
                + (t * t * end);
        }

        private static float CalculateMidpointFactor(float pitchDot)
        {
            int lastSample = MIDPOINT_FACTOR.GetLength(0) - 1;
            for (int i = 0; i < lastSample; i++)
            {
                if (MIDPOINT_FACTOR[i, 0] <= pitchDot
                    && MIDPOINT_FACTOR[i + 1, 0] > pitchDot)
                {
                    return Interpolate(pitchDot, i, i + 1);
                }
            }

            if (MIDPOINT_FACTOR[0, 0] < pitchDot)
            {
                return Interpolate(pitchDot, 0, 1);
            }

            if (MIDPOINT_FACTOR[lastSample, 0] < pitchDot)
            {
                return Interpolate(pitchDot, lastSample - 1, lastSample);
            }

            float Interpolate(float angle, int fromIndex, int toIndex)
            {
                float t = Mathf.InverseLerp(MIDPOINT_FACTOR[fromIndex, 0], MIDPOINT_FACTOR[toIndex, 0], angle);
                return Mathf.LerpUnclamped(MIDPOINT_FACTOR[fromIndex, 1], MIDPOINT_FACTOR[toIndex, 1], t);
            }

            return 0.5f;
        }

        private void UpdateMeshData(ArcPoint[] points, int divisions, float width, Color tint)
        {
            Quaternion rotation = Quaternion.identity;
            int steps = points.Length;

            float endFade = points[steps - 1].relativeLength - EndFadeThresold;
            for (int i = 0; i < steps; i++)
            {
                Vector3 point = points[i].position;
                float progress = points[i].relativeLength;
                Color color = Gradient.Evaluate(progress) * tint;
                if (Progress != null
                    && i / (steps - 1f) < Progress.Value())
                {
                    color.a *= ProgressFade;
                }
                else if (progress > endFade)
                {
                    float dif = 1f - ((progress - endFade) / EndFadeThresold);
                    color.a *= dif;
                }
                _layout.color = color;

                if (i < steps - 1)
                {
                    rotation = Quaternion.LookRotation(points[i].direction);
                }

                for (int j = 0; j <= divisions; j++)
                {
                    float radius = 2 * Mathf.PI * j / divisions;
                    Vector3 circle = new Vector3(Mathf.Sin(radius), Mathf.Cos(radius), 0);
                    Vector3 normal = rotation * circle;

                    _layout.pos = point + normal * width;
                    if (_mirrorTexture)
                    {
                        float x = (j / (float)divisions) * 2f;
                        if (j >= divisions * 0.5f)
                        {
                            x = 2 - x;
                        }
                        _layout.uv = new Vector2(x, progress);
                    }
                    else
                    {
                        _layout.uv = new Vector2(j / (float)divisions, progress);
                    }
                    int vertIndex = i * (divisions + 1) + j;
                    _vertsData[vertIndex] = _layout;
                }
            }

            _mesh.bounds = new Bounds(
                (points[0].position + points[steps - 1].position) * 0.5f,
                points[steps - 1].position - points[0].position);
            _mesh.SetVertexBufferData(_vertsData, 0, 0, _vertsData.Length, 0, MeshUpdateFlags.DontRecalculateBounds);
        }

        private int SetVertexCount(int positionCount, int divisions)
        {
            int vertsPerPosition = divisions + 1;
            int vertCount = positionCount * vertsPerPosition;

            int tubeTriangles = (positionCount - 1) * divisions * 6;
            int capTriangles = (divisions - 2) * 3;
            _tris = new int[tubeTriangles + capTriangles * 2];

            // handle triangulation
            for (int i = 0; i < positionCount - 1; i++)
            {
                // add faces
                for (int j = 0; j < divisions; j++)
                {
                    int vert0 = i * vertsPerPosition + j;
                    int vert1 = (i + 1) * vertsPerPosition + j;
                    int t = (i * divisions + j) * 6;
                    _tris[t] = vert0;
                    _tris[t + 1] = _tris[t + 4] = vert1;
                    _tris[t + 2] = _tris[t + 3] = vert0 + 1;
                    _tris[t + 5] = vert1 + 1;
                }
            }

            // triangulate the ends
            Cap(tubeTriangles, 0, divisions - 1, true);
            Cap(tubeTriangles + capTriangles, vertCount - divisions, vertCount - 1);

            void Cap(int t, int firstVert, int lastVert, bool clockwise = false)
            {
                for (int i = firstVert + 1; i < lastVert; i++)
                {
                    _tris[t++] = firstVert;
                    _tris[t++] = clockwise ? i : i + 1;
                    _tris[t++] = clockwise ? i + 1 : i;
                }
            }

            return vertCount;
        }

        #region Inject

        public void InjectAllTeleportProceduralArcVisual(TeleportInteractor interactor,
            MeshFilter arcFilter)
        {
            InjectTeleportInteractor(interactor);
            InjectArcFilter(arcFilter);
        }

        public void InjectTeleportInteractor(TeleportInteractor interactor)
        {
            _interactor = interactor;
        }

        public void InjectArcFilter(MeshFilter arcFilter)
        {
            _arcFilter = arcFilter;
        }

        public void InjectOptionalProgress(IAxis1D progress)
        {
            _progress = progress as MonoBehaviour;
            Progress = progress;
        }
        #endregion
    }
}
