// Copyright (c) Meta Platforms, Inc. and affiliates.

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
            m_AlignIcon.color = SampleColors.Green;
            m_AlignBtn.interactable = false;
        }
        else
        {
            m_AlignIcon.color = SampleColors.Gray;
            m_AlignBtn.interactable = true;
        }

        if (LocallySaved.AnchorIsRemembered(Uuid))
        {
            m_RememberIcon.color = SampleColors.Green;
            m_RememberLabel.SetText("Forget UUID");
        }
        else
        {
            m_RememberIcon.color = SampleColors.Gray;
            m_RememberLabel.SetText("Remember UUID");
        }
        m_RememberBtn.interactable = LocallySaved.AnchorsCanGrow;

        var groups = ColoDiscoMan.GetGroupsFor(Uuid);

        list2ui(ColoDiscoMan.GetDisplayNamesFor(groups), m_GroupListLabel);

        return;

        // local function section:

        static void list2ui<T>(IEnumerable<T> list, TMP_Text ui)
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
                if (m_EraseIcon)
                    m_EraseIcon.color = SampleColors.Yellow;
            }
            else if (m_EraseIcon)
            {
                m_EraseIcon.color = SampleColors.Alert;
            }
            Sampleton.LogError($"- Erase Anchor FAILED! {loggedResult}");
            return;
        }

        LocallySaved.IgnoreAnchor(uuid);

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

    public async void ToggleRememberUuid()
    {
        if (LocallySaved.AnchorIsRemembered(Uuid))
        {
            if (LocallySaved.ForgetAnchor(Uuid))
                Sampleton.Log($"+ Forget Anchor: {SampleColors.RichText.Noice}Success</color>");
            else
                Sampleton.LogError($"- Forget Anchor: {SampleColors.RichText.Alert}Failure</color>");
        }
        else
        {
            string loggedResult = null;
            if (!IsSaved)
            {
                var saveResult = await SaveAnchorAsync();
                IsSaved = saveResult.Success;
                loggedResult = saveResult.Status.ForLogging();
            }

            IsSaved = IsSaved && LocallySaved.RememberAnchor(Uuid, Source.IsMine);

            if (IsSaved)
                Sampleton.Log($"+ Remember (Save) Anchor: {loggedResult}");
            else
                Sampleton.LogError($"- Remember (Save) Anchor FAILED! (SaveAnchorAsync returned {loggedResult})");
        }

        UpdateUI();
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
    [SerializeField]
    TMP_Text m_RememberLabel;
    [SerializeField]
    Image m_EraseIcon;

    AnchorSource m_Source;

    static readonly System.Text.StringBuilder s_Stringer = new();


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
            var icon = btn.FindChildRecursive("Icon");
            if (!icon || !icon.TryGetComponent(out m_RememberIcon))
                Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);

            m_RememberLabel = btn.GetComponentInChildren<TMP_Text>();
            if (!m_RememberLabel)
                Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }

        btn = transform.FindChildRecursive("Button: Erase");
        if (btn)
        {
            var icon = btn.FindChildRecursive("Icon");
            if (icon)
                m_EraseIcon = icon.GetComponent<Image>();
        }

        var listBox = transform.FindChildRecursive("List: Groups Shared");
        if (!listBox || !listBox.TryGetComponent(out m_GroupListLabel))
        {
            Debug.LogError($"\"{name}\" seems to be improperly set-up.", this);
        }
    }

    async void Awake() // Can't override Start, but since we immediately await, Awake is equivalent enough.
    {
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas)
            canvas.gameObject.SetActive(false); // don't render controls until creation & localization is complete

        // handy API: instance OVRSpatialAnchor.WhenCreatedAsync()
        if (await WhenCreatedAsync())
        {
            Sampleton.Log($"{nameof(ColoDiscoAnchor)}: Created!");
        }
        else
        {
            Sampleton.LogError($"{nameof(ColoDiscoAnchor)}: FAILED TO CREATE!\n- destroying instance..");
            Destroy(gameObject);
            return;
        }

        var uuid = Uuid;

        Sampleton.Log($"+ Uuid: {uuid}");

        if (!m_Source.IsSet)
            m_Source = AnchorSource.New(uuid);

        gameObject.name =
            m_Source.Origin == AnchorSource.Type.FromGroupShare ? $"anchor:{uuid:N}-SHARED"
                                                                : $"anchor:{uuid:N}";

        // handy API: instance OVRSpatialAnchor.WhenLocalizedAsync()
        if (!await WhenLocalizedAsync())
        {
            Sampleton.LogError($"- Localization FAILED! ({uuid.Brief()})");
            Destroy(gameObject);
            return;
        }

        Sampleton.Log($"+ Loaded spatial anchor {uuid.Brief()}; bound and localized! ({m_Source.Origin})");

        if (!Alignment)
            SetAsAlignmentAnchor();

        UpdateUI();

        if (canvas)
            canvas.gameObject.SetActive(true);

        ColoDiscoMan.NotifyAnchorLocalized(this);
    }

    void OnEnable()
    {
        UpdateUI();
    }

} // end MonoBehaviour ColoDiscoAnchor
