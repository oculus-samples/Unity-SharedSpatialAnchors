// Copyright (c) Meta Platforms, Inc. and affiliates.
// This code is licensed under the MIT license (see LICENSE for details).

using System;
using System.Collections.Generic;

using TMPro;

using UnityEngine;
using UnityEngine.UI;

using Sampleton = SampleController; // only transitional


/// <remarks>
///   OVRSpatialAnchor allows you to inherit from it, however beware of which Unity Messages you choose to implement!
///   Ill-defined states are known to occur if you implement any of:
///     - Start
///     - Update
///     - LateUpdate
///     - OnDestroy
///   You've been warned!
/// </remarks>
/// <remarks>
///   All key API calls relating to this component are implemented in <see cref="ColoDiscoMan"/>. This is in contrast to
///   how <see cref="SharedAnchor"/> and <see cref="SharedAnchorLoader"/> are split.
/// </remarks>
public sealed class ColoDiscoAnchor : OVRSpatialAnchor
{

    //
    // Public API:

    public static ColoDiscoAnchor Alignment { get; private set; } // TODO refactor

    public static bool HasRememberedAnchor // TODO refactor
        => PlayerPrefs.HasKey(k_RememberedAnchorKey);

    public static Guid RememberedAnchorId // TODO refactor
    {
        get
        {
            if (!s_RememberedAnchorId.HasValue)
            {
                string ser = PlayerPrefs.GetString(k_RememberedAnchorKey, null);
                _ = Guid.TryParse(ser, out var parsed);
                s_RememberedAnchorId = parsed;
            }
            return s_RememberedAnchorId.Value;
        }
        private set
        {
            if (value == Guid.Empty)
            {
                PlayerPrefs.DeleteKey(k_RememberedAnchorKey);
                Sampleton.Log($"{k_RememberedAnchorKey} cleared from PlayerPrefs.");
                s_RememberedAnchorId = null;
                return;
            }
            PlayerPrefs.SetString(k_RememberedAnchorKey, value.Serialize());
            Sampleton.Log($"{value.Brief()} persisted to PlayerPrefs!");
            s_RememberedAnchorId = value;
        }
    }

    public static bool IsRemembered(Guid anchorId) // TODO refactor
        => anchorId != Guid.Empty && anchorId == RememberedAnchorId;


    // instance

    public bool IsSaved { get; set; }

    public AnchorSource Source
    {
        get => m_Source;
        set
        {
            if (m_Source.Equals(value))
                return;

            if (m_Source.IsSet)
            {
                Sampleton.LogError($"{name}.{nameof(Source)} is already set!");
                return;
            }

            m_Source = value;
        }
    }


    public void UpdateUI()
    {
        m_UuidLabel.text = $"{Uuid}\n{nameof(Source)}: {m_Source}";

        if (Alignment == this)
        {
            m_AlignIcon.color = k_ColorGreen;
            m_AlignBtn.interactable = false;
        }
        else
        {
            m_AlignIcon.color = k_ColorGray;
            m_AlignBtn.interactable = true;
        }

        if (s_RememberedAnchor == this)
        {
            m_RememberIcon.color = k_ColorGreen;
            m_RememberBtn.interactable = false;
        }
        else
        {
            m_RememberIcon.color = k_ColorGray;
            m_RememberBtn.interactable = true;
        }

        var groups = ColoDiscoMan.GetGroupsFor(Uuid);

        list2ui(ColoDiscoMan.GetDisplayNamesFor(groups), m_GroupListLabel);

        return;

        // local function section:

        void list2ui<T>(IEnumerable<T> list, TMP_Text ui)
        {
            if (!ui)
                return;

            s_Stringer.Clear();

            foreach (var thing in list)
            {
                s_Stringer.AppendLine(thing.ToString());
            }

            ui.SetText(s_Stringer);
        }
    }


    //
    // UnityEvent Callbacks (for Buttons)

    public void Hide()
    {
        Sampleton.Log($"{nameof(ColoDiscoAnchor)}.{nameof(Hide)}: {Uuid.Brief()}");

        Destroy(gameObject);

        // NOTE: "Destroy" == "Hide" for anchors only if they have been saved locally or shared.
        // Otherwise, this is a proper deletion as far as your app is concerned; even if you saved its Uuid prior to
        // destroying the anchor, there is no guarantee you'll be able to load it back (although it isn't guaranteed
        // that you *can't* load it back, either.. better !!! than ???)
    }

    public async void Erase()
    {
        var uuid = Uuid;
        Sampleton.Log($"{nameof(ColoDiscoAnchor)}.{nameof(Erase)}: {uuid}");

        // API call: instance OVRSpatialAnchor.EraseAnchorAsync()
        var result = await EraseAnchorAsync();

        var loggedResult = result.Status.ForLogging();

        if (!result.Success)
        {
            if (!Source.IsMine)
            {
                loggedResult += $"\n  (You aren't the original creator of {uuid.Brief()}.)";
                try // not being lazy
                {
                    transform
                        .FindChildRecursive("Button: Erase")
                        .FindChildRecursive("Icon")
                        .GetComponent<Image>().color = k_ColorYellow;
                }
                catch (Exception e)
                {
                    Sampleton.Log($"{e.GetType().Name}: {e.Message}", LogType.Exception, LogOption.None);
                    Debug.Log(e);
                    // pass
                }
            }
            Sampleton.LogError($"- Erase Anchor FAILED! {loggedResult}");
            return;
        }

        if (RememberedAnchorId == uuid)
            RememberedAnchorId = Guid.Empty;

        ColoDiscoMan.NotifyAnchorErased(uuid);

        Destroy(gameObject);

        Sampleton.Log($"+ Erase Anchor: {loggedResult}");

        if (Source.IsMine)
            return;

        Sampleton.Log(
            $"- WARNING: You aren't the original creator of {uuid.Brief()}." +
            "\n  (The owner, you, or other sharees may see undefined behaviours.)",
            LogType.Warning
        );
    }

    public void Share()
    {
        ColoDiscoMan.ShareToActiveGroup(this);
    }

    public void SetAsAlignmentAnchor()
    {
        Sampleton.Log($"{nameof(SetAsAlignmentAnchor)}: {gameObject.name}");

        var previous = Alignment;

        Alignment = this;

        ColoDiscoMan.NotifyAnchorAlignment(this, previous);

        if (previous)
            previous.UpdateUI();

        UpdateUI();
    }

    public void RememberUuid()
    {
        var previous = s_RememberedAnchor;

        RememberedAnchorId = Uuid;
        s_RememberedAnchor = this;

        UpdateUI();

        if (previous)
            previous.UpdateUI();
    }


    //
    // Fields & Constants

    [Header("[ReadOnly] - no need to touch these manually:")]
    [SerializeField]
    TMP_Text m_UuidLabel;
    [SerializeField]
    TMP_Text m_GroupListLabel;
    [SerializeField]
    Button m_AlignBtn;
    [SerializeField]
    Image m_AlignIcon;
    [SerializeField]
    Button m_RememberBtn;
    [SerializeField]
    Image m_RememberIcon;

    AnchorSource m_Source;

    static readonly System.Text.StringBuilder s_Stringer = new();

    static ColoDiscoAnchor s_RememberedAnchor;
    static Guid? s_RememberedAnchorId;

    const string k_RememberedAnchorKey = nameof(ColoDiscoAnchor) + ".SpecialAnchor";

    // TODO refactor these up since they're identical to SharedAnchor's
    static readonly Color k_ColorGray = new Color32(0x8B, 0x8C, 0x8E, 0xFF);
    static readonly Color k_ColorGreen = new Color32(0x5A, 0xCA, 0x25, 0xFF);
    static readonly Color k_ColorRed = new Color32(0xDD, 0x25, 0x35, 0xFF);
    static readonly Color k_ColorYellow = Color.yellow;

    //
    // Unity Messages

    void OnValidate()
    {
        var idLabel = transform.FindChildRecursive("ID Label");
        if (!idLabel || !idLabel.TryGetComponent(out m_UuidLabel))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        var btn = transform.FindChildRecursive("Button: Align");
        if (!btn || !btn.TryGetComponent(out m_AlignBtn))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }
        else
        {
            var icon = m_AlignBtn.transform.FindChildRecursive("Icon");
            if (!icon || !icon.TryGetComponent(out m_AlignIcon))
                Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        btn = transform.FindChildRecursive("Button: Remember UUID");
        if (!btn || !btn.TryGetComponent(out m_RememberBtn))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }
        else
        {
            var icon = m_RememberBtn.transform.FindChildRecursive("Icon");
            if (!icon || !icon.TryGetComponent(out m_RememberIcon))
                Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        var listBox = transform.FindChildRecursive("List: Groups Shared");
        if (!listBox || !listBox.TryGetComponent(out m_GroupListLabel))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }
    }

    async void Awake() // Can't override Start, but since we immediately await, Awake is equivalent enough.
    {
        // handy API: instance OVRSpatialAnchor.WhenCreatedAsync()
        if (await WhenCreatedAsync())
        {
            Sampleton.Log($"{nameof(ColoDiscoAnchor)}: Created!\n+ {Uuid}");
        }
        else
        {
            Sampleton.LogError($"{nameof(ColoDiscoAnchor)}: FAILED TO CREATE!\n- destroying instance..");
            Destroy(gameObject);
            return;
        }

        gameObject.name = $"anchor:{Uuid:N}";
        m_UuidLabel.text = $"{Uuid}";

        if (RememberedAnchorId == Uuid)
        {
            RememberUuid();
        }

        // handy API: instance OVRSpatialAnchor.WhenLocalizedAsync()
        if (!await WhenLocalizedAsync())
        {
            Sampleton.LogError($"- Localization FAILED! ({Uuid.Brief()})");
            Destroy(gameObject);
            return;
        }

        if (m_Source.IsSet)
        {
            if (m_Source.Origin != AnchorSource.Type.FromSave) // TODO: edge case where anchor was shared AND loaded from save
                gameObject.name += "-SHARED";
            Sampleton.Log($"+ Loaded spatial anchor {Uuid.Brief()}; bound and localized! ({m_Source.Origin})");
        }
        else
        {
            // we can reasonably assume this is a brand-new anchor:
            m_Source = AnchorSource.New(ColoDiscoMan.CurrentUserID, Uuid);
            Sampleton.Log($"+ New spatial anchor {Uuid.Brief()} created and localized!");
        }

        if (!Alignment)
        {
            SetAsAlignmentAnchor();
        }

        UpdateUI();

        ColoDiscoMan.NotifyAnchorLocalized(this);
    }

    void OnEnable()
    {
        UpdateUI();
    }

} // end MonoBehaviour ColoDiscoAnchor
