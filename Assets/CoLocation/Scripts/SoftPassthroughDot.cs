using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoftPassthroughDot : MonoBehaviour
{
    [SerializeField] private MeshRenderer mesh;
    private float currentAlpha = 1;
    private Transform target;

    public void Init(Transform target, Transform parent)
    {
        this.target = target;
        transform.SetParent(parent);
    }

    public void UpdateAlpha(float alpha, bool delta = false)
    {
        currentAlpha = Mathf.Clamp01(delta ? currentAlpha + alpha : alpha);
        mesh.material.SetFloat("_Alpha", currentAlpha);
    }

    public void Pos(Vector3 pos)
    {
        transform.position = pos;
        transform.LookAt(target);
    }

}
