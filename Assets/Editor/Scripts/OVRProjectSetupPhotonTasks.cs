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
            } else {
                photonConfigFile.AppSettings.AppIdRealtime = _appIdInputFieldText;
                EditorUtility.SetDirty(photonConfigFile);
                AssetDatabase.SaveAssets();
            }

            Close();
        }
    }
}
