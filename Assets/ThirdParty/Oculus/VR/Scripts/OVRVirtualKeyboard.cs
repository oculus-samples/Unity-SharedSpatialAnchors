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
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using System.Linq;

/// <summary>
/// Supports Virtual Keyboard integration by providing the implementation to necessary common patterns
/// </summary>
public class OVRVirtualKeyboard : MonoBehaviour
{
	public enum KeyboardInputMode
	{
		Far = 0,
		Direct = 1,
		Max = 2,
	}

	public abstract class OVRVirtualKeyboardInput : MonoBehaviour
	{
		public OVRInput.Controller InteractionDevice;

		public abstract bool PositionValid { get; }
		public abstract bool IsPressed { get; }
		public abstract OVRPlugin.Posef InputPose { get; }
		public abstract OVRPlugin.Posef InteractorRootPose { get; }
		public virtual void ModifyInteractorRoot(OVRPlugin.Posef interactorRootPose) { }

		// Conversion helpers
		public Vector3 InputPosePosition => InputPose.Position.FromFlippedZVector3f();
		public Quaternion InputPoseRotation => InputPose.Orientation.FromFlippedZQuatf();
	}

	public static class Events
	{
		public static void Init()
		{
			eventHandler_ = new VirtualKeyboardEventHandler();
			OVRManager.instance.RegisterEventListener(eventHandler_);
		}

		public static void Deinit()
		{
			OVRManager.instance.DeregisterEventListener(eventHandler_);
		}

		/// <summary>
		/// Occurs when text has been committed
		/// @params (string text)
		/// </summary>
		public static event Action<string> CommitText;

		/// <summary>
		/// Occurs when a backspace is pressed
		/// </summary>
		public static event Action Backspace;

		/// <summary>
		/// Occurs when a return key is pressed
		/// </summary>
		public static event Action Enter;

		/// <summary>
		/// Occurs when keyboard is shown
		/// </summary>
		public static event Action KeyboardShown;

		/// <summary>
		/// Occurs when keyboard is hidden
		/// </summary>
		public static event Action KeyboardHidden;

		/// <summary>
		/// Occurs when a sound should be played
		/// </summary>
		public static event Action<uint> PlaySound;

		private static VirtualKeyboardEventHandler eventHandler_;

		private class VirtualKeyboardEventHandler : OVRManager.EventListener
		{
			public void OnEvent(OVRPlugin.EventDataBuffer eventDataBuffer)
			{
				switch (eventDataBuffer.EventType)
				{
					case OVRPlugin.EventType.VirtualKeyboardCommitText:
					{
						CommitText?.Invoke(Encoding.UTF8.GetString(eventDataBuffer.EventData));
						break;
					}
					case OVRPlugin.EventType.VirtualKeyboardBackspace:
					{
						Backspace?.Invoke();
						break;
					}
					case OVRPlugin.EventType.VirtualKeyboardEnter:
					{
						Enter?.Invoke();
						break;
					}
					case OVRPlugin.EventType.VirtualKeyboardShown:
					{
						KeyboardShown?.Invoke();
						break;
					}
					case OVRPlugin.EventType.VirtualKeyboardHidden:
					{
						KeyboardHidden?.Invoke();
						break;
					}
					case OVRPlugin.EventType.VirtualKeyboardPlaySound:
					{
						PlaySound?.Invoke(BitConverter.ToUInt32(eventDataBuffer.EventData, 0));
						break;
					}
				}
			}
		}
	}

	internal class OVRVirtualKeyboardSwipeTrail : MonoBehaviour {

		public static bool HasTrail()
		{
			// get buffer/trail size
			var swipeTrailInfo = new OVRPlugin.VirtualKeyboardSwipeTrailState
			{
				shapeCapacityInput = 0
			};

			var result = OVRPlugin.GetVirtualKeyboardSwipeTrailState(ref swipeTrailInfo);
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("GetVirtualKeyboardSwipeTrailInfo failed:" + result);
				return false;
			}

			return swipeTrailInfo.shapeCountOutput >= 1;
		}

		public static OVRVirtualKeyboardSwipeTrail AttachTo(Transform targetParent, Material mat)
		{
			var go = new GameObject();
			go.transform.SetParent(targetParent, false);
			var trail = go.AddComponent<OVRVirtualKeyboardSwipeTrail>();
			trail.swipeLine.material = trail.material = mat;
			return trail;
		}

		private static readonly float SimplificationTolerance = 0.001f;

		private Vector3[] shapeBuffer = new Vector3[1000];
		private IntPtr shapeBufferPtr;
		private LineRenderer swipeLine;
		private Material material;
		private float startWidth;
		private float lifetime;
		private bool isDetached;
		private float ageSinceDetached;

		public void Detach()
		{
			Marshal.FreeHGlobal(shapeBufferPtr);
			swipeLine.Simplify(SimplificationTolerance);
			isDetached = true;
		}

		private void Awake()
		{
			shapeBufferPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Vector3)) * shapeBuffer.Length);

			isDetached = false;

			gameObject.AddComponent<RectTransform>();
			swipeLine = gameObject.AddComponent<LineRenderer>();
			swipeLine.useWorldSpace = false;
			swipeLine.alignment = LineAlignment.TransformZ;
			swipeLine.numCornerVertices = 4;
			swipeLine.numCapVertices = 4;
			swipeLine.widthCurve = AnimationCurve.EaseInOut(0, 0, 1, 0);
			swipeLine.receiveShadows = false;
			swipeLine.shadowCastingMode = ShadowCastingMode.Off;
		}

		private void Update()
		{
			if (isDetached == false)
			{
				UpdateTrail();
			}
			else
			{
				UpdateTrailDecay();
			}
		}

		private void UpdateTrail()
		{
			// retrieve swipe info
			var swipeTrailInfo = new OVRPlugin.VirtualKeyboardSwipeTrailState
			{
				shapeCapacityInput = (uint)shapeBuffer.Length,
				shape = shapeBufferPtr,
			};
			var result = OVRPlugin.GetVirtualKeyboardSwipeTrailState(ref swipeTrailInfo, shapeBuffer);
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("GetVirtualKeyboardSwipeTrailInfo failed:" + result);
				return;
			}

			// update points
			swipeLine.positionCount = (int)swipeTrailInfo.shapeCountOutput;
			swipeLine.SetPositions(shapeBuffer);

			// Update properties
			lifetime = swipeTrailInfo.lifetimeSeconds;
			material.color = swipeTrailInfo.color.FromColorf();
			swipeLine.startWidth = startWidth = swipeTrailInfo.startWidth;
		}

		private void UpdateTrailDecay()
		{
			// Age
			ageSinceDetached += Time.deltaTime;
			var t = (ageSinceDetached / lifetime);
			t = t * t; // ease in
			// Decay
			swipeLine.startWidth = Mathf.Lerp(startWidth, 0.0f, t);
			// Death
			if (ageSinceDetached >= lifetime)
			{
				Destroy(this);
			}
		}

	}

	/// <summary>
	/// Unity UI field to automatically commit text into. (optional)
	/// </summary>
	[SerializeField]
	public InputField TextCommitField;

	/// <summary>
	/// Input handlers, which provide pose and other data for each
	/// input device (hand or controller).
	/// </summary>
	[SerializeField]
	public OVRVirtualKeyboardInput[] InputHandlers;

	/// <summary>
	/// (Internal) Transform of GameObject at root of virtual keyboard tree.
	/// </summary>
	[SerializeField]
	private Transform keyboardRootTransform;

	/// <summary>
	/// (Internal) Material used for the swipe trail
	/// </summary>
	[SerializeField]
	private Material swipeTrailMaterial;

	/// <summary>
	/// (Internal) Controls which style of input used for interracting with the keyboard
	/// </summary>
	[SerializeField]
	public KeyboardInputMode InputMode = KeyboardInputMode.Far;

	[SerializeField]
	private Shader keyboardModelShader;
	[SerializeField]
	private Shader keyboardModelAlphaBlendShader;

	private bool isKeyboardCreated_ = false;

	private Dictionary<uint, AudioClip> soundClips = new Dictionary<uint, AudioClip>();
	private AudioSource audioSource;
	private bool isSwipeTrailActive = false;
	private OVRVirtualKeyboardSwipeTrail activeSwipeTrail;

	private UInt64 keyboardSpace_;
	private float scale_ = 1.0f;

	private Dictionary<ulong, List<Material>> virtualKeyboardTextures_ = new Dictionary<ulong, List<Material>>();
	private OVRGLTFScene virtualKeyboardScene_;
	private ulong virtualKeyboardModelKey_;
	private bool modelInitialized_ = false;
	private bool modelAvailable_ = false;
	private bool keyboardVisible_ = false;

	// Unity event functions

	void Awake()
	{
		Events.Init();

		// Register for events
		Events.CommitText += OnCommitText;
		Events.Backspace += OnBackspace;
		Events.Enter += OnEnter;
		Events.KeyboardShown += OnKeyboardShown;
		Events.KeyboardHidden += OnKeyboardHidden;
		Events.PlaySound += OnPlaySound;

		audioSource = GetComponent<AudioSource>();
	}

	void OnDestroy()
	{
		Events.Deinit();
		DestroyKeyboard();
	}

	void OnEnable()
	{
		ShowKeyboard();
	}

	void OnDisable()
	{
		HideKeyboard();
	}

	// Public properties

	public Vector3 Position
	{
		get => keyboardRootTransform.position;

		set
		{
			keyboardRootTransform.position = value;
			UpdateKeyboardLocation();
		}
	}

	public Quaternion Rotation
	{
		get => keyboardRootTransform.rotation;

		set
		{
			keyboardRootTransform.rotation = value;
			UpdateKeyboardLocation();
		}
	}

	public float Scale
	{
		get => scale_;

		set
		{
			bool scaleChanged = (scale_ != value);
			scale_ = value;
			UpdateKeyboardLocation();
		}
	}

	public void SuggestVirtualKeyboardLocationForInputMode(KeyboardInputMode inputMode)
	{
		OVRPlugin.VirtualKeyboardLocationInfo locationInfo = new OVRPlugin.VirtualKeyboardLocationInfo();
		switch (inputMode)
		{
			case KeyboardInputMode.Direct:
				locationInfo.locationType = OVRPlugin.VirtualKeyboardLocationType.Direct;
				break;
			case KeyboardInputMode.Far:
				locationInfo.locationType = OVRPlugin.VirtualKeyboardLocationType.Far;
				break;
			default:
				Debug.LogError("Unknown KeyboardInputMode: " + inputMode);
				break;
		}
		var result = OVRPlugin.SuggestVirtualKeyboardLocation(locationInfo);
		if (result != OVRPlugin.Result.Success)
		{
			Debug.LogError("SuggestVirtualKeyboardLocation failed: " + result);
		}
	}


	// Private methods
	private GameObject LoadRuntimeVirtualKeyboardMesh()
	{
		Debug.Log("LoadRuntimeVirtualKeyboardMesh");
		string[] modelPaths = OVRPlugin.GetRenderModelPaths();

		var keyboardPath = modelPaths?.FirstOrDefault(p => p.Equals("/model_fb/virtual_keyboard")
		                                                   || p.Equals("/model_meta/keyboard/virtual"));

		if (String.IsNullOrEmpty(keyboardPath))
		{
			Debug.LogError("Failed to find keyboard model.");
			return null;
		}

		OVRPlugin.RenderModelProperties modelProps = new OVRPlugin.RenderModelProperties();
		if (OVRPlugin.GetRenderModelProperties(keyboardPath, ref modelProps))
		{
			if (modelProps.ModelKey != OVRPlugin.RENDER_MODEL_NULL_KEY)
			{
				virtualKeyboardModelKey_ = modelProps.ModelKey;

				byte[] data = OVRPlugin.LoadRenderModel(modelProps.ModelKey);
				if (data != null)
				{
					OVRGLTFLoader gltfLoader = new OVRGLTFLoader(data);
					gltfLoader.textureUriHandler = (string rawUri, Material mat) =>
					{
						var uri = new Uri(rawUri);
						// metaVirtualKeyboard://texture/{id}?w={width}&h={height}&ft=RGBA32
						if (uri.Scheme != "metaVirtualKeyboard" && uri.Host != "texture")
						{
							return null;
						}

						var textureId = ulong.Parse(uri.LocalPath.Substring(1));
						if (virtualKeyboardTextures_.ContainsKey(textureId) == false)
						{
							virtualKeyboardTextures_[textureId] = new List<Material>();
						}
						virtualKeyboardTextures_[textureId].Add(mat);
						return null; // defer texture data loading
					};
					gltfLoader.SetModelShader(keyboardModelShader);
					gltfLoader.SetModelAlphaBlendShader(keyboardModelAlphaBlendShader);
					virtualKeyboardScene_ = gltfLoader.LoadGLB(supportAnimation: true, loadMips: true);

					modelAvailable_ = true;

					return virtualKeyboardScene_.root;
				}
			}
		}
		Debug.LogError("Failed to load model.");
		return null;
	}

	private void ShowKeyboard()
	{
		if (!isKeyboardCreated_)
		{
			var createInfo = new OVRPlugin.VirtualKeyboardCreateInfo();

			var result = OVRPlugin.CreateVirtualKeyboard(createInfo);
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("Create failed: " + result);
				return;
			}

			// Once created the keyboard should be positioned
			// instead of using a default location, initially use with the unity keyboard root transform
			var locationInfo = ComputeLocation(keyboardRootTransform, Scale);

			var createSpaceInfo = new OVRPlugin.VirtualKeyboardSpaceCreateInfo();
			createSpaceInfo.pose = OVRPlugin.Posef.identity;
			result = OVRPlugin.CreateVirtualKeyboardSpace(createSpaceInfo, out keyboardSpace_);
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("Create failed to create keyboard space: " + result);
				return;
			}

			result = OVRPlugin.SuggestVirtualKeyboardLocation(locationInfo);
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("Create failed to position keyboard: " + result);
				return;
			}

			// Initialize the keyboard model
			if (modelInitialized_ != true)
			{
				modelInitialized_ = true;
				LoadRuntimeVirtualKeyboardMesh();
			}

			// Should call this whenever the keyboard is created or when the text focus changes
			result = OVRPlugin.ChangeVirtualKeyboardTextContext(TextCommitField.text);
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("Failed to set keyboard text context");
				return;
			}
		}

		try
		{
			SetKeyboardVisibility(true);
			UpdateKeyboardLocation();
			isKeyboardCreated_ = true;
		}
		catch
		{
			DestroyKeyboard();
			throw;
		}
	}

	private void SetKeyboardVisibility(bool visible)
	{
		if (!modelInitialized_)
		{
			// Set active was called before the model was even attempted to be loaded
			return;
		}
		if (!modelAvailable_)
		{
			Debug.LogError("Failed to set visibility. Keyboard model unavailable.");
			return;
		}

		var visibility = new OVRPlugin.VirtualKeyboardModelVisibility();
		visibility.ModelKey = virtualKeyboardModelKey_;
		visibility.Visible = visible;
		var res = OVRPlugin.SetVirtualKeyboardModelVisibility(ref visibility);
		if (res != OVRPlugin.Result.Success)
		{
			Debug.Log("SetVirtualKeyboardModelVisibility failed: " + res);
		}
	}

	private void HideKeyboard()
	{
		if (!modelAvailable_)
		{
			// If model has not been loaded, completely uninitialize
			DestroyKeyboard();
			return;
		}
		SetKeyboardVisibility(false);
	}

	private void DestroyKeyboard()
	{
		if (isKeyboardCreated_)
		{
			if (modelAvailable_)
			{
				GameObject.Destroy(virtualKeyboardScene_.root);
				modelAvailable_ = false;
				modelInitialized_ = false;
			}

			var result = OVRPlugin.DestroyVirtualKeyboard();
			if (result != OVRPlugin.Result.Success)
			{
				Debug.LogError("Destroy failed");
				return;
			}
			Debug.Log("Destroy success");
		}

		isKeyboardCreated_ = false;
	}

	private OVRPlugin.VirtualKeyboardLocationInfo ComputeLocation(Transform transform, float scale)
	{
		OVRPlugin.VirtualKeyboardLocationInfo location = new OVRPlugin.VirtualKeyboardLocationInfo();

		location.locationType = OVRPlugin.VirtualKeyboardLocationType.Custom;
		// Plane in Unity has its normal facing towards camera by default, in runtime it's facing away,
		// so to compensate, flip z for both position and rotation, for both plane and pointer pose.
		location.pose.Position = transform.position.ToFlippedZVector3f();
		location.pose.Orientation = transform.rotation.ToFlippedZQuatf();
		location.scale = scale;
		return location;
	}

	private void UpdateKeyboardLocation()
	{
		var locationInfo = ComputeLocation(keyboardRootTransform, Scale);
		var result = OVRPlugin.SuggestVirtualKeyboardLocation(locationInfo);
		if (result != OVRPlugin.Result.Success)
		{
			Debug.LogError("Failed to update keyboard location: " + result);
		}
	}

	void Update()
	{
		if (!isKeyboardCreated_)
		{
			return;
		}

		UpdateInputs();
		SyncKeyboardLocation();
		UpdateAnimationState();
	}

	private void UpdateInputs()
	{
		foreach (OVRVirtualKeyboardInput inputHandler in InputHandlers)
		{
			if (inputHandler.PositionValid)
			{
				var inputInfo = new OVRPlugin.VirtualKeyboardInputInfo();
				switch (inputHandler.InteractionDevice)
				{
					case OVRInput.Controller.LHand:
						inputInfo.inputSource = InputMode == KeyboardInputMode.Far ?
							OVRPlugin.VirtualKeyboardInputSource.HandRayLeft :
							OVRPlugin.VirtualKeyboardInputSource.HandDirectIndexTipLeft;
						break;
					case OVRInput.Controller.LTouch:
						inputInfo.inputSource = InputMode == KeyboardInputMode.Far ?
							OVRPlugin.VirtualKeyboardInputSource.ControllerRayLeft :
							OVRPlugin.VirtualKeyboardInputSource.ControllerDirectLeft;
						break;
					case OVRInput.Controller.RHand:
						inputInfo.inputSource = InputMode == KeyboardInputMode.Far ?
							OVRPlugin.VirtualKeyboardInputSource.HandRayRight :
							OVRPlugin.VirtualKeyboardInputSource.HandDirectIndexTipRight;
						break;
					case OVRInput.Controller.RTouch:
						inputInfo.inputSource = InputMode == KeyboardInputMode.Far ?
							OVRPlugin.VirtualKeyboardInputSource.ControllerRayRight:
							OVRPlugin.VirtualKeyboardInputSource.ControllerDirectRight;
						break;
					default:
						inputInfo.inputSource = OVRPlugin.VirtualKeyboardInputSource.Invalid;
						break;
				}
				inputInfo.inputPose = inputHandler.InputPose;
				inputInfo.inputState = 0;
				if (inputHandler.IsPressed)
				{
					inputInfo.inputState |= OVRPlugin.VirtualKeyboardInputStateFlags.IsPressed;
				}

				var interactorRootPose = inputHandler.InteractorRootPose;
				var result = OVRPlugin.SendVirtualKeyboardInput(inputInfo, ref interactorRootPose);
				inputHandler.ModifyInteractorRoot(interactorRootPose);
			}
		}
	}

	private void SyncKeyboardLocation()
	{
		if (!OVRPlugin.TryLocateSpace(keyboardSpace_, OVRPlugin.GetTrackingOriginType(), out var keyboardPose))
		{
			Debug.LogError("Failed to locate the virtual keyboard space.");
			return;
		}

		var result = OVRPlugin.GetVirtualKeyboardScale(out var keyboardScale);
		if (result != OVRPlugin.Result.Success)
		{
			Debug.LogError("Failed to get virtual keyboard scale.");
			return;
		}

		keyboardRootTransform.position = keyboardPose.Position.FromFlippedZVector3f();
		keyboardRootTransform.rotation = keyboardPose.Orientation.FromFlippedZQuatf();
		Scale = keyboardScale;

		if (modelAvailable_)
		{
			virtualKeyboardScene_.root.transform.position = keyboardPose.Position.FromFlippedZVector3f();
			// Rotate to face user
			virtualKeyboardScene_.root.transform.rotation = keyboardPose.Orientation.FromFlippedZQuatf() * Quaternion.Euler(0,180f,0);
			virtualKeyboardScene_.root.transform.localScale = Vector3.one * Scale;
		}

	}

	private void UpdateAnimationState()
	{
		UpdateSwipeTrails();

		if (!modelAvailable_)
		{
			return;
		}

		OVRPlugin.GetVirtualKeyboardDirtyTextures(out var dirtyTextures);
		foreach(var textureId in dirtyTextures.TextureIds)
		{
			if (!virtualKeyboardTextures_.TryGetValue(textureId, out var textureMaterials))
			{
				continue;
			};

			var textureData = new OVRPlugin.VirtualKeyboardTextureData();
			OVRPlugin.GetVirtualKeyboardTextureData(textureId, ref textureData);
			if (textureData.TextureCountOutput > 0)
			{
				try
				{
					textureData.TextureBuffer = Marshal.AllocHGlobal((int)textureData.TextureCountOutput);
					textureData.TextureCapacityInput = textureData.TextureCountOutput;
					OVRPlugin.GetVirtualKeyboardTextureData(textureId, ref textureData);

					var texBytes = new byte[textureData.TextureCountOutput];
					Marshal.Copy(textureData.TextureBuffer, texBytes, 0, (int)textureData.TextureCountOutput);

					var tex = new Texture2D((int)textureData.TextureWidth, (int)textureData.TextureHeight, TextureFormat.RGBA32, false);
					tex.filterMode = FilterMode.Trilinear;
					tex.SetPixelData(texBytes, 0);
					tex.Apply(true /*updateMipmaps*/, true /*makeNoLongerReadable*/);
					foreach (var material in textureMaterials)
					{
						material.mainTexture = tex;
					}
				}
				finally
				{
					Marshal.FreeHGlobal(textureData.TextureBuffer);
				}
			}
		}

		var result = OVRPlugin.GetVirtualKeyboardModelAnimationStates(virtualKeyboardModelKey_, out var animationStates);
		if (result == OVRPlugin.Result.Success)
		{
			for(var i = 0; i < animationStates.States.Length; i++)
			{
				if (!virtualKeyboardScene_.animationNodeLookup.ContainsKey(animationStates.States[i].AnimationIndex))
				{
					Debug.LogWarning($"Unknown Animation State Index {animationStates.States[i].AnimationIndex}");
					continue;
				}
				virtualKeyboardScene_.animationNodeLookup[animationStates.States[i].AnimationIndex].UpdatePose(animationStates.States[i].Fraction, false);
			}
			if (animationStates.States.Length > 0)
			{
				foreach (var morphTargets in virtualKeyboardScene_.morphTargetHandlers)
				{
					morphTargets.Update();
				}
			}
		}
	}

	private void UpdateSwipeTrails()
	{
		if (OVRVirtualKeyboardSwipeTrail.HasTrail() && !isSwipeTrailActive)
		{
			isSwipeTrailActive = true;
			activeSwipeTrail = OVRVirtualKeyboardSwipeTrail.AttachTo(transform, swipeTrailMaterial);
		}
		else if (!OVRVirtualKeyboardSwipeTrail.HasTrail() && isSwipeTrailActive)
		{
			isSwipeTrailActive = false;
			activeSwipeTrail.Detach();
		}
	}

	private void OnCommitText(string text)
	{
		// TODO: take caret and selection position into account T127712980
		if (TextCommitField != null)
		{
			TextCommitField.text += text;
		}
	}

	private void OnBackspace()
	{
		// TODO: take caret and selection position into account T127712980
		if (TextCommitField != null)
		{
			string text = TextCommitField.text;
			TextCommitField.text = text.Substring(0, text.Length - 1);
		}
	}

	private void OnEnter()
	{
		// TODO: take caret and selection position into account T127712980
		if (TextCommitField != null && TextCommitField.multiLine)
		{
			TextCommitField.text += "\n";
		}
	}

	private void OnKeyboardShown()
	{
		if (!keyboardVisible_)
		{
			keyboardVisible_ = true;
			gameObject.SetActive(keyboardVisible_);
			virtualKeyboardScene_.root.gameObject.SetActive(keyboardVisible_);
		}
	}

	private void OnKeyboardHidden()
	{
		if (keyboardVisible_)
		{
			keyboardVisible_ = false;
			gameObject.SetActive(keyboardVisible_);
			virtualKeyboardScene_.root.gameObject.SetActive(keyboardVisible_);
		}
	}

	private AudioClip LoadSound(uint soundId)
	{
		OVRPlugin.VirtualKeyboardSound sound = new OVRPlugin.VirtualKeyboardSound();
		sound.SoundId = soundId;
		OVRPlugin.Result result = OVRPlugin.GetVirtualKeyboardSound(ref sound);
		if (result != OVRPlugin.Result.Success)
		{
			Debug.LogError("Get sound failed");
			return null;
		}

		Debug.Log("LoadSound (" + soundId.ToString() + "): samples=" + sound.SoundBuffer.Length + ", channels=" + sound.Channels + ", frequency=" + sound.Frequency);

		AudioClip clip = AudioClip.Create("KeyboardSound" + soundId, sound.SoundBuffer.Length, sound.Channels, sound.Frequency, false);
		clip.SetData(sound.SoundBuffer, 0);
		return clip;
	}

	private void OnPlaySound(uint soundId)
	{
		AudioClip clip;
		if (!soundClips.TryGetValue(soundId, out clip))
		{
			// Lazy load sound for this type
			clip = LoadSound(soundId);
			if (clip)
			{
				soundClips.Add(soundId, clip);
			}
		}
		if (clip)
		{
			audioSource.PlayOneShot(clip);
		}
	}
}
