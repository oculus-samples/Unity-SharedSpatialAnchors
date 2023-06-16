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
using UnityEngine;

namespace Common {
    [Serializable]
    public class Scene {
        public Plane[] walls;
        public Plane floor;
        public Plane ceiling;
        public Obstacle[] obstacles;
    }

    [Serializable]
    public class Plane {
        public Vector3 position;
        public Quaternion rotation;
        public Rect rect;
    }

    [Serializable]
    public enum ObstacleType {
        Couch, Desk, Door, Window, Storage, Other, Table
    }

    [Serializable]
    public class Obstacle {
        public Vector3 position;
        public Quaternion rotation;
        public Bounds boundingBox;
        public ObstacleType type;
    }
}
