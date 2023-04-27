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

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

internal static class OVRMovementTool
{
	private const string k_SetupCharacterForBodyTrackingMovementToolsMenuStr = "GameObject/Movement/Setup Character for Body Tracking/";
	private const string oculusSkeletonFormat = "Format: Oculus Skeleton";

	[MenuItem(k_SetupCharacterForBodyTrackingMovementToolsMenuStr + oculusSkeletonFormat, true)]
	private static bool ValidateSetupCharacterForOculusSkeletonBodyTracking()
	{
		return Selection.activeGameObject != null;
	}

	[MenuItem(k_SetupCharacterForBodyTrackingMovementToolsMenuStr + oculusSkeletonFormat)]
	private static void SetupCharacterForOculusSkeletonBodyTracking()
	{
		SetUpCharacterForBodyTracking(OVRCustomSkeleton.RetargetingType.OculusSkeleton);
	}

	private static void SetUpCharacterForBodyTracking(OVRCustomSkeleton.RetargetingType retargetingType)
	{
		Undo.IncrementCurrentGroup();
		var gameObject = Selection.activeGameObject;

		var body = gameObject.GetComponent<OVRBody>();
		if (!body)
		{
			body = gameObject.AddComponent<OVRBody>();
			Undo.RegisterCreatedObjectUndo(body, "Create OVRBody component");
		}

		var skeleton = gameObject.GetComponent<OVRCustomSkeleton>();
		if (!skeleton)
		{
			skeleton = gameObject.AddComponent<OVRCustomSkeleton>();
			Undo.RegisterCreatedObjectUndo(skeleton, "Create OVRCustomSkeleton component");
		}

		Undo.RegisterFullObjectHierarchyUndo(skeleton, "Auto-map OVRCustomSkeleton bones");
		skeleton.SetSkeletonType(OVRSkeleton.SkeletonType.Body);
		skeleton.retargetingType = retargetingType;

		skeleton.AutoMapBones(retargetingType);
		EditorUtility.SetDirty(skeleton);
		EditorSceneManager.MarkSceneDirty(skeleton.gameObject.scene);

		Undo.SetCurrentGroupName($"Setup Character for {retargetingType} Body Tracking");
	}

}
