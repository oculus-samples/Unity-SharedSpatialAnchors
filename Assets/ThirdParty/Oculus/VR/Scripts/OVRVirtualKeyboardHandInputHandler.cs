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

using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(OVRHand))]
[RequireComponent(typeof(OVRSkeleton))]
public class OVRVirtualKeyboardHandInputHandler : OVRVirtualKeyboard.OVRVirtualKeyboardInput
{
	public override bool PositionValid =>
		OVRInput.IsControllerConnected(InteractionDevice) &&
		(OVRVirtualKeyboard.InputMode == OVRVirtualKeyboard.KeyboardInputMode.Direct ?
			skeleton.IsDataValid && handIndexTip != null :
			hand.IsPointerPoseValid);

	public override bool IsPressed => OVRInput.Get(
		OVRInput.Button.One, // hand pinch
		InteractionDevice);

	public override OVRPlugin.Posef InputPose
	{
		get
		{
			Transform inputTransform =
				OVRVirtualKeyboard.InputMode == OVRVirtualKeyboard.KeyboardInputMode.Direct ?
				handIndexTip.Transform :
				hand.PointerPose;
			return new OVRPlugin.Posef()
			{
				Position = inputTransform.position.ToFlippedZVector3f(),
				// Rotation on the finger tip transform is sideways, use this instead
				Orientation = hand.PointerPose.rotation.ToFlippedZQuatf(),
			};
		}
	}

	public OVRVirtualKeyboard OVRVirtualKeyboard;

	private OVRHand hand;
	private OVRSkeleton skeleton;

	private OVRBone handIndexTip;

	// Poke limiting state
	private bool pendingApply;
	private bool pendingRevert;
	private OVRPlugin.Posef originalInteractorRootPose;
	private OVRPlugin.Posef newInteractorRootPose;

	private void Start()
	{
		hand = GetComponent<OVRHand>();
		skeleton = GetComponent<OVRSkeleton>();
	}

	private void Update()
	{
		if (handIndexTip == null && skeleton.IsDataValid)
		{
			handIndexTip = GetSkeletonIndexTip(skeleton);
		}
	}

	private static OVRBone GetSkeletonIndexTip(OVRSkeleton skeleton)
	{
		return skeleton.Bones.First(b => b.Id == OVRSkeleton.BoneId.Hand_IndexTip);
	}

	//
	// Poke limiting logic
	//

	private void LateUpdate()
	{
		ApplyPendingWristRoot();
		StartCoroutine(RevertWristRoot());
	}

	private OVRBone wristRoot
	{
		get => limitingReady ? skeleton.Bones[(int)OVRSkeleton.BoneId.Hand_WristRoot] : null;
	}

	private bool limitingReady
	{
		get => hand.IsTracked && hand.IsDataValid;
	}

	public override OVRPlugin.Posef InteractorRootPose
	{
		get
		{
			if (limitingReady)
			{
				return new OVRPlugin.Posef()
				{
					Position = wristRoot.Transform.position.ToFlippedZVector3f(),
					Orientation = wristRoot.Transform.rotation.ToFlippedZQuatf(),
				};
			}

			return OVRPlugin.Posef.identity;
		}
	}

	public override void ModifyInteractorRoot(OVRPlugin.Posef interactorRootPose)
	{
		if (!limitingReady || (wristRoot.Transform.position == interactorRootPose.Position.FromFlippedZVector3f() &&
		                       wristRoot.Transform.rotation == interactorRootPose.Orientation.FromFlippedZQuatf()))
		{
			// hands are not tracking or if the position or rotation has not actually changed do nothing
			return;
		}
		// if the new pose has changed, mark the apply as pending.
		pendingApply = true;
		newInteractorRootPose = interactorRootPose;
	}

	private void ApplyPendingWristRoot()
	{
		if (!pendingApply)
		{
			return;
		}
		pendingApply = false;
		pendingRevert = true;
		originalInteractorRootPose = InteractorRootPose;
		wristRoot.Transform.position = newInteractorRootPose.Position.FromFlippedZVector3f();
		wristRoot.Transform.rotation = newInteractorRootPose.Orientation.FromFlippedZQuatf();
	}

	private IEnumerator RevertWristRoot()
	{
		if (!pendingRevert)
		{
			yield break;
		}
		yield return new WaitForEndOfFrame();
		pendingRevert = false;
		wristRoot.Transform.position = originalInteractorRootPose.Position.FromFlippedZVector3f();
		wristRoot.Transform.rotation = originalInteractorRootPose.Orientation.FromFlippedZQuatf();
	}
}
