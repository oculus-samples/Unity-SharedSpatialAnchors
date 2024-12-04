// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using TMPro;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;


public class MenuController : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("referencePoint")]
    Transform m_MenuAnchor;
    [SerializeField]
    TMP_Text m_StatusLabel; // TODO this should be encapsulated separately so any scene can call SetStatusText

    readonly List<(int, string)> m_StatusLines = new();


    private void OnValidate()
    {
        if (!m_MenuAnchor)
        {
            var find = GameObject.Find("Ref Point");
            if (find)
                m_MenuAnchor = find.transform;
        }

        if (gameObject.scene.IsValid() && !m_MenuAnchor) // avoids erroring in prefab view
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up. (no anchor for canvas)", this);
        }

        var child = transform.FindChildRecursive("Text: Status Text");
        if (!child || !child.TryGetComponent(out m_StatusLabel))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up. (no status text)", this);
        }
    }

    IEnumerator Start()
    {
        transform.parent = m_MenuAnchor;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        yield return null;

        var buildInfoRequest = Resources.LoadAsync<TextAsset>("buildInfo");

        yield return buildInfoRequest;

        const int kOrder = 10;
        if (buildInfoRequest.asset is not TextAsset textAsset || textAsset.dataSize < 10)
        {
            SetStatusText("<build unknown>", kOrder);
        }
        else
        {
            string text = textAsset.text;
            int hash = text.IndexOf('#');
            if (hash > 0)
                text = $"rev {text.Substring(hash)}\nbuilt {text.Remove(hash - 1)}";
            SetStatusText(text, kOrder);
        }

        var timeUpdateInterval = new WaitForSecondsRealtime(1f);

        while (this)
        {
            UpdateNowStatus();
            yield return timeUpdateInterval;
        }
    }


    void UpdateNowStatus()
    {
        int kOrder = 1;
        var now = DateTime.Now;
        SetStatusText($"Today: {now:f}", kOrder);
    }

    void SetStatusText(string text, int order)
    {
        Action<int> replace;
        Func<int, bool> insert;
        if (string.IsNullOrEmpty(text))
        {
            replace = i => m_StatusLines.RemoveAt(i);
            insert = _ => false;
        }
        else
        {
            replace = i => m_StatusLines[i] = (order, text);
            insert = i => { m_StatusLines.Insert(i, (order, text)); return true; };
        }

        int i = m_StatusLines.Count;
        while (i-- > 0)
        {
            var current = m_StatusLines[i].Item1;
            if (order == current)
            {
                replace(i);
                goto Apply;
            }
            if (order > current && insert(i + 1))
            {
                goto Apply;
            }
        }

        insert(0);
        Apply:
        m_StatusLabel.text = string.Join("\n\n", m_StatusLines.Select(p => p.Item2));
    }


    public void OnLoadDemoScene(int iSceneIndex)
    {
        Debug.Log($"{nameof(OnLoadDemoScene)}:{iSceneIndex}");
        SceneManager.LoadScene(iSceneIndex);
    }

    public void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
    }

    public void ExitAppOrPlaymode() // copied from SampleKit.SampleState
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
