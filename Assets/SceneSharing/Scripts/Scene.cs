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
        Couch, Desk, Misc
    }

    [Serializable]
    public class Obstacle {
        public Vector3 position;
        public Quaternion rotation;
        public Bounds boundingBox;
        public ObstacleType type;
    }
}
