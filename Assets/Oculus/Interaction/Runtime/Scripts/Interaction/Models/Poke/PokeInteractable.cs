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
using UnityEngine.Assertions;
using Oculus.Interaction.Surfaces;
using System;
using UnityEngine.Serialization;

namespace Oculus.Interaction
{
    public class PokeInteractable : PointerInteractable<PokeInteractor, PokeInteractable>
    {
        [SerializeField, Interface(typeof(ISurface))]
        private MonoBehaviour _surface;
        public ISurface Surface;

        [SerializeField, Interface(typeof(IProximityField))]
        private MonoBehaviour _proximityField;
        public IProximityField ProximityField;

        [SerializeField]
        private float _maxDistance = 0.1f;
        public float MaxDistance => _maxDistance;

        [SerializeField]
        private float _enterHoverDistance = 0f;

        [SerializeField]
        private float _releaseDistance = 0.25f;

        [SerializeField, Optional]
        private int _tiebreakerScore = 0;

        [SerializeField, Optional]
        private Collider _volumeMask = null;
        public Collider VolumeMask { get => _volumeMask; }

        [Serializable]
        public class DragThresholdingConfig
        {
            public bool Enabled;
            public float SurfaceThreshold;
            public float ZThreshold;
            public ProgressCurve DragEaseCurve;
        }

        [SerializeField]
        private DragThresholdingConfig _dragThresholding =
            new DragThresholdingConfig()
            {
                Enabled = true,
                SurfaceThreshold = 0.01f,
                ZThreshold = 0.01f,
                DragEaseCurve = new ProgressCurve(AnimationCurve.EaseInOut(0,0,1,1), 0.05f)
            };

        [Serializable]
        public class PositionPinningConfig
        {
            public bool Enabled;
            public float MaxPinDistance;
        }

        [SerializeField]
        private PositionPinningConfig _positionPinning =
            new PositionPinningConfig()
            {
                Enabled = false,
                MaxPinDistance = 0f
            };

        #region Properties
        public float EnterHoverDistance => _enterHoverDistance;

        public float ReleaseDistance => _releaseDistance;

        public int TiebreakerScore
        {
            get
            {
                return _tiebreakerScore;
            }
            set
            {
                _tiebreakerScore = value;
            }
        }

        public DragThresholdingConfig DragThresholding
        {
            get
            {
                return _dragThresholding;
            }

            set
            {
                _dragThresholding = value;
            }
        }

        public PositionPinningConfig PositionPinning
        {
            get
            {
                return _positionPinning;
            }

            set
            {
                _positionPinning = value;
            }
        }

        #endregion

        protected override void Awake()
        {
            base.Awake();
            ProximityField = _proximityField as IProximityField;
            Surface = _surface as ISurface;
        }

        protected override void Start()
        {
            this.BeginStart(ref _started, () => base.Start());
            Assert.IsNotNull(ProximityField);
            Assert.IsNotNull(Surface);
            if (_enterHoverDistance > 0f)
            {
                _enterHoverDistance = Mathf.Min(_enterHoverDistance, _maxDistance);
            }
            this.EndStart(ref _started);
        }

        public Vector3 ComputeClosestPoint(Vector3 point)
        {
            Vector3 proximityFieldPoint = ProximityField.ComputeClosestPoint(point);
            Surface.ClosestSurfacePoint(proximityFieldPoint, out SurfaceHit hit);
            return hit.Point;
        }

        public Vector3 ClosestSurfacePoint(Vector3 point)
        {
            Surface.ClosestSurfacePoint(point, out SurfaceHit hit);
            return hit.Point;
        }

        public Vector3 ClosestSurfaceNormal(Vector3 point)
        {
            Surface.ClosestSurfacePoint(point, out SurfaceHit hit);
            return hit.Normal;
        }

        #region Inject

        public void InjectAllPokeInteractable(ISurface surface,
                                              IProximityField proximityField)
        {
            InjectSurface(surface);
            InjectProximityField(proximityField);
        }

        public void InjectSurface(ISurface surface)
        {
            _surface = surface as MonoBehaviour;
            Surface = surface;
        }

        public void InjectProximityField(IProximityField proximityField)
        {
            _proximityField = proximityField as MonoBehaviour;
            ProximityField = proximityField;
        }

        public void InjectOptionalMaxDistance(float maxDistance)
        {
            _maxDistance = maxDistance;
        }

        public void InjectOptionalReleaseDistance(float releaseDistance)
        {
            _releaseDistance = releaseDistance;
        }

        public void InjectOptionalEnterHoverDistance(float enterHoverDistance)
        {
            _enterHoverDistance = enterHoverDistance;
        }

        public void InjectOptionalVolumeMask(Collider volumeMask)
        {
            _volumeMask = volumeMask;
        }

        #endregion
    }
}
