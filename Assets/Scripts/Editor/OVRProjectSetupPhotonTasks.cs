// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class OVRProjectSetupPhotonTasks
{
    private const OVRProjectSetup.TaskGroup TaskGroup = OVRProjectSetup.TaskGroup.Miscellaneous;

    public static T FindScriptableObjectInProject<T>() where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

        if (guids.Length == 0)
        {
            return null;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }

    static OVRProjectSetupPhotonTasks()
    {
        var photonConfigFile = FindScriptableObjectInProject<Photon.Pun.ServerSettings>();

        OVRProjectSetup.AddTask(
            conditionalValidity: buildTargetGroup => photonConfigFile != null,
            conditionalLevel: buildTargetGroup => OVRProjectSetup.TaskLevel.Recommended,
            group: TaskGroup,
            isDone: buildTargetGroup => photonConfigFile == null || !string.IsNullOrEmpty(photonConfigFile.AppSettings.AppIdRealtime),
            message: "Photon SDK requires an account to be setup in order for it to function",
            fix: buildTargetGroup => PhotonAppIdPopupWindow.ShowWindow(),
            fixMessage: "Setup Photon SDK account"
        );
    }
}

internal class PhotonAppIdPopupWindow : EditorWindow
{
    private string _appIdInputFieldText = "";

    public static void ShowWindow()
    {
        var window = GetWindow<PhotonAppIdPopupWindow>("Custom Popup Window");
        var size = new Vector2(300, 125);
        window.minSize = size;
        window.maxSize = size;
    }

    private void OnGUI()
    {
        GUILayout.Space(5);

        GUILayout.Label("1) Create a Photon App to get an App Id", EditorStyles.boldLabel);
        if (GUILayout.Button("Go to the Photon Dashboard"))
        {
            const string url = "https://dashboard.photonengine.com/en-US/PublicCloud";
            Application.OpenURL(url);
        }

        GUILayout.Space(20);

        GUILayout.Label("2) Enter your Photon App Id:", EditorStyles.boldLabel);
        _appIdInputFieldText = EditorGUILayout.TextField(_appIdInputFieldText);

        if (GUILayout.Button("Record App Id"))
        {
            var photonConfigFile = OVRProjectSetupPhotonTasks.FindScriptableObjectInProject<Photon.Pun.ServerSettings>();

            if (photonConfigFile == null)
            {
                Debug.LogError("No Photon config file found");
            }
            else
            {
                photonConfigFile.AppSettings.AppIdRealtime = _appIdInputFieldText;
                EditorUtility.SetDirty(photonConfigFile);
                AssetDatabase.SaveAssets();
            }

            Close();
        }
    }
}
