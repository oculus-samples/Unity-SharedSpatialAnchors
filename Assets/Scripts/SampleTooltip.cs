// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using TMPro;

using UnityEngine;

public class SampleTooltip : MonoBehaviour
{
    //
    // Static interface

    public static void Show(string text)
    {
        if (!s_Current)
            return;
        s_Current.ShowTooltip(text, append: false);
    }

    public static void Append(string text)
    {
        if (!s_Current)
            return;
        s_Current.ShowTooltip(text, append: true);
    }

    public static void Hide()
    {
        if (!s_Current)
            return;
        s_Current.HideTooltip();
    }


    static SampleTooltip s_Current;

    //
    // Component instance

    [SerializeField]
    TMP_Text m_Tooltip;
    [SerializeField]
    GameObject m_ToggleRoot;
    [SerializeField]
    bool m_Preview = true;

    [SerializeField]
    UnityEngine.UI.LayoutElement m_Layout;


    bool IsValid => m_ToggleRoot && m_Tooltip;


    void Reset()
    {
        var finds = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var maybe in finds)
        {
            if (!maybe.name.StartsWith("Tooltip"))
                continue;
            m_Tooltip = maybe;
            m_ToggleRoot = m_Tooltip.gameObject;
            m_Preview = m_ToggleRoot.activeSelf;
            m_Layout = m_Tooltip.GetComponent<UnityEngine.UI.LayoutElement>();
            Debug.Log($"Automatically found reference to tooltip text component \"{m_Tooltip}\". You should check that it's the right one.", this);
            return;
        }
    }

    void OnValidate()
    {
        if (m_Tooltip)
        {
            if (!m_ToggleRoot)
            {
                m_ToggleRoot = m_Tooltip.gameObject;
                m_Preview = m_ToggleRoot.activeSelf;
            }
            updatePreview();
            return;
        }

        m_Preview = false;

        if (!m_ToggleRoot)
            return;

        m_Tooltip = m_ToggleRoot.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (m_Tooltip)
        {
            Debug.Log($"Automatically found reference to tooltip text component \"{m_Tooltip}\". You should check that it's the right one.", this);
            updatePreview();
        }

        void updatePreview()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (m_ToggleRoot)
                    m_ToggleRoot.SetActive(false);
                else
                    m_Preview = false;
                if (!m_Tooltip)
                    return;
                if (m_Preview)
                    ShowTooltip(m_Tooltip.text, append: false);
            };
#endif
        }
    }

    void OnEnable()
    {
        if (!IsValid)
        {
            Debug.LogWarning($"No tooltip text reference assigned! Tooltips will not show up.", this);
            enabled = false;
            return;
        }

        HideTooltip();

        // lazy singleton impl.
        if (s_Current)
            Destroy(s_Current);

        s_Current = this;
    }

    void OnDisable()
    {
        HideTooltip();
        if (s_Current == this)
            s_Current = null;
    }

    void ShowTooltip(string text, bool append)
    {
        if (!IsValid)
        {
            Debug.LogWarning($"Unable to show tooltip: {text}");
            enabled = false;
            return;
        }

        if (!append && string.IsNullOrEmpty(text))
        {
            HideTooltip();
            return;
        }

        m_Tooltip.text = append ? $"{m_Tooltip.text}\n{text}"
                                : text;

        m_ToggleRoot.SetActive(true);

        if (!m_Layout)
            return;

        m_Layout.minHeight = m_Tooltip.preferredHeight;
    }

    void HideTooltip()
    {
        if (!m_ToggleRoot)
            return;

        m_ToggleRoot.SetActive(false);
    }

} // end class SampleTooltip
