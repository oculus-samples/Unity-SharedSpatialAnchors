using System;
using UnityEngine;

namespace Common {
    public abstract class SceneCaptureStrategy : MonoBehaviour {
        public abstract void Start();
        
        public abstract void CaptureScene(Action<Scene> onComplete);
    }
}
