// Copyright (c) Meta Platforms, Inc. and affiliates.

using JetBrains.Annotations;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using TMPro;

using UnityEngine;


[RequireComponent(typeof(TMP_Text))]
public class SampleStatus : MonoBehaviour
{
    [PublicAPI]
    public static void SetLine([CanBeNull] string text, int order)
    {
        Action<int> replace;
        Func<int, bool> insert;

        if (text is null) // remove any entry at this exact order
        {
            replace = i => s_StatusLines.RemoveAt(i);
            insert = _ => false;
        }
        else // insert or replace an ordered entry's text
        {
            replace = i => s_StatusLines[i] = (order, text);
            insert = i => { s_StatusLines.Insert(i, (order, text)); return true; };
        }

        int i = s_StatusLines.Count;
        while (i-- > 0)
        {
            int current = s_StatusLines[i].order;
            if (order == current)
            {
                replace(i);
                Rebuild();
                return;
            }

            if (order < current)
                continue;

            if (insert(i + 1))
            {
                Rebuild();
                return;
            }

            // The insert delegate only returns false if text is null,
            // meaning the caller is trying to *remove* the status line.

            // We can break early since s_StatusLines is implicitly sorted,
            // so subsequent 'current' values will never be equal to 'order'.
            break;
        }

        // fallthrough == the given order value is the lowest so far,
        //                so we insert the entry at the beginning:
        if (insert(0)) // (unless we were intending to remove the entry)
            Rebuild(); // ((if so, it didn't exist in the first place, so no need to rebuild))
    }

    [PublicAPI]
    public static void Clear()
    {
        s_StatusLines.Clear();
        if (s_Instance)
        {
            s_Instance.StopAllCoroutines();
            s_Instance.m_Coroutines.Clear();
            Rebuild();
        }
    }

    [PublicAPI]
    public static void DoBuildInfo(int order)
    {
        DoRoutine(nameof(DoBuildInfo), coroutine(order));

        return;

        // local func
        static IEnumerator coroutine(int order)
        {
            var buildInfoRequest = Resources.LoadAsync<TextAsset>("buildInfo");

            yield return buildInfoRequest;

            if (buildInfoRequest.asset is not TextAsset textAsset || textAsset.dataSize < 10)
            {
                SetLine("<build unknown>", order);
            }
            else
            {
                string text = textAsset.text;
                int hash = text.IndexOf('#');
                if (hash > 0)
                    text = $"rev {text.Substring(hash)}\nbuilt {text.Remove(hash - 1)}";
                SetLine(text, order);
            }
        }
    }

    [PublicAPI]
    public static void DoWallClock(int order, bool utc = true)
    {
        DoRoutine(nameof(DoWallClock), coroutine(order, utc));

        return;

        // local func
        static IEnumerator coroutine(int order, bool utc)
        {
            var now = utc ? DateTime.UtcNow : DateTime.Now;
            var interval = new WaitForSecondsRealtime(1f);
            while (true)
            {
                SetLine($"UtcNow: {now:f}", order);
                yield return interval;
                now = utc ? DateTime.UtcNow : DateTime.Now;
            }
        }
    }

    //
    // private impl.

    static SampleStatus s_Instance;
    static readonly List<(int order, string line)> s_StatusLines = new();
    static readonly StringBuilder s_Stringer = new();

    static void Rebuild()
    {
        s_Stringer.Clear();

        if (!s_Instance) // OK to rebuild later; eager entries are preserved in s_StatusLines
            return;

        int newlineCount = s_Instance.m_NewlineCount;
        var statusLabel = s_Instance.m_StatusLabel;

        for (int i = 0; i < s_StatusLines.Count; ++i)
        {
            if (i > 0)
                s_Stringer.Append('\n', newlineCount);
            s_Stringer.Append(s_StatusLines[i].line);
        }

        statusLabel.SetText(s_Stringer);
    }

    static void DoRoutine(string key, IEnumerator routine)
    {
        if (!s_Instance)
        {
            // routine.MoveNext();
            return;
        }

        if (s_Instance.m_Coroutines.Remove(key, out var prev) && prev is not null)
        {
            s_Instance.StopCoroutine(prev);
        }

        s_Instance.m_Coroutines[key] = s_Instance.StartCoroutine(routine);
    }

    // instance

    [SerializeField]
    TMP_Text m_StatusLabel;
    [SerializeField, Range(0, 5)]
    int m_NewlineCount = 1;
    [SerializeField]
    bool m_ClearOnDestroy = true;

    readonly Dictionary<string, Coroutine> m_Coroutines = new();

    void OnValidate()
    {
        if (!m_StatusLabel)
            m_StatusLabel = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        if (s_Instance && s_Instance != this)
        {
            s_Instance.OnDestroy();
            Destroy(s_Instance.gameObject);
        }

        s_Instance = this;

        m_Coroutines.Clear();

        Rebuild();
    }

    void OnDestroy()
    {
        if (s_Instance != this)
            return;

        s_Instance = null;

        if (m_ClearOnDestroy)
            Clear();
    }

} // end MonoBehaviour SampleStatus
