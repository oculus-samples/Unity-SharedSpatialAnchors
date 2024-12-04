// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

using Sampleton = SampleController; // only transitional

public class PoseOrigin : MonoBehaviour
{
    public enum DisplayMode
    {
        Undirected = 0,
        Directed = 1,
        Coords = 2,
    }


    public static bool UseLocalCoords
    {
        get => s_UseLocalCoords;
        set
        {
            if (s_UseLocalCoords == value)
                return;

            Sampleton.Log($"{nameof(PoseOrigin)}.{nameof(UseLocalCoords)}: {s_UseLocalCoords} --> {value}");

            s_UseLocalCoords = value;

            foreach (var poe in s_Instances)
            {
                poe.UpdateCoordsNow();
            }
        }
    }
    static bool s_UseLocalCoords;


    public DisplayMode Mode
    {
        get => m_Mode;
        set
        {
            // these will throw warnings from OnValidate, but it's fine.
            m_X.enabled = value == DisplayMode.Directed;
            m_Y.enabled = value == DisplayMode.Directed;
            m_Z.enabled = value == DisplayMode.Directed;
            m_XCoords.enabled = value == DisplayMode.Coords;
            m_YCoords.enabled = value == DisplayMode.Coords;
            m_ZCoords.enabled = value == DisplayMode.Coords;

            m_Mode = value;
        }
    }


    public void TweenTo(Vector3 pos, Quaternion rot, float overSec = 3.5f)
    {
        IEnumerator doTween()
        {
            var startPos = transform.position;
            var startRot = transform.rotation;

            float start = Time.time;
            float t = 0f;

            while (t < 1f)
            {
                t = (Time.time - start) / overSec;

                transform.SetPositionAndRotation(
                    position: Vector3.LerpUnclamped(startPos, pos, t),
                    rotation: Quaternion.SlerpUnclamped(startRot, rot, t)
                );

                if (m_UpdateCoords is null && Time.frameCount % 2 == 0)
                    UpdateCoordsNow();

                yield return null;
            }

            transform.SetPositionAndRotation(pos, rot);
            m_CurrentTween = null;

            if (m_UpdateCoords is null)
                UpdateCoordsNow();
        }

        if (m_CurrentTween != null)
            StopCoroutine(m_CurrentTween);
        m_CurrentTween = StartCoroutine(doTween());
    }

    public void UpdateCoordsNow()
    {
        Vector3 pos, rot;
        if (s_UseLocalCoords)
        {
            pos = transform.localPosition;
            rot = transform.localRotation.eulerAngles;
        }
        else
        {
            pos = transform.position;
            rot = transform.rotation.eulerAngles;
        }

        m_XCoords.text = $"{pos.x:F2}\n{rot.x:F0}°";
        m_YCoords.text = $"{pos.y:F2}\n{rot.y:F0}°";
        m_ZCoords.text = $"{pos.z:F2}\n{rot.z:F0}°";
    }

    public void StartUpdatingCoords(float everySec = 0.15f)
    {
        if (m_Mode != DisplayMode.Coords || !Application.IsPlaying(this))
            return;

        IEnumerator doUpdateLoop()
        {
            var interval = new WaitForSeconds(everySec);
            while (m_Mode == DisplayMode.Coords)
            {
                UpdateCoordsNow();
                yield return interval;
            }
            m_UpdateCoords = null;
        }

        if (m_UpdateCoords != null)
            StopCoroutine(m_UpdateCoords);
        m_UpdateCoords = StartCoroutine(doUpdateLoop());
    }


    [SerializeField]
    DisplayMode m_Mode;

    [Header("[ReadOnly] - no need to touch these manually:")]
    [SerializeField]
    TMP_Text m_X;
    [SerializeField]
    TMP_Text m_Y;
    [SerializeField]
    TMP_Text m_Z;
    [SerializeField]
    TMP_Text m_XCoords;
    [SerializeField]
    TMP_Text m_YCoords;
    [SerializeField]
    TMP_Text m_ZCoords;


    Coroutine m_UpdateCoords, m_CurrentTween;


    static readonly HashSet<PoseOrigin> s_Instances = new();


    void OnValidate()
    {
        foreach (var tmp in GetComponentsInChildren<TMP_Text>())
        {
            switch (tmp.name)
            {
                case "Text: X":
                    m_X = tmp;
                    break;
                case "Text: Y":
                    m_Y = tmp;
                    break;
                case "Text: Z":
                    m_Z = tmp;
                    break;
                case "Text: X-coords":
                    m_XCoords = tmp;
                    break;
                case "Text: Y-coords":
                    m_YCoords = tmp;
                    break;
                case "Text: Z-coords":
                    m_ZCoords = tmp;
                    break;
            }
        }

        Mode = m_Mode;
    }


    void OnEnable()
    {
        s_Instances.Add(this);
    }

    void OnDisable()
    {
        s_Instances.Remove(this);
    }

}
