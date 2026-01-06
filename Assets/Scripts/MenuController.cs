// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

using Sampleton = SampleController;


[MetaCodeSample("SharedSpatialAnchors")]
public class MenuController : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("referencePoint")]
    Transform m_MenuAnchor;


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
    }

    void Start()
    {
        transform.parent = m_MenuAnchor;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        SampleStatus.DoBuildInfo(order: 10);
        SampleStatus.DoWallClock(order: 1);
    }


    public void OnLoadDemoScene(int iSceneIndex)
    {
        Debug.Log($"{nameof(OnLoadDemoScene)}:{iSceneIndex}");
        SceneManager.LoadScene(iSceneIndex);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Edit/Clear Local Save Data", priority = 280)]
#endif
    public static void ClearLocalSaveData()
    {
        PlayerPrefs.DeleteAll();
        Sampleton.Log($"{nameof(ClearLocalSaveData)}: PlayerPrefs cleared.");
        LocallySaved.DeleteAll(wipeDisk: true);
        Sampleton.Log($"{nameof(ClearLocalSaveData)}: LocallySaved (serialized anchors) cleared.");
    }

    public static void ExitAppOrPlaymode()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
