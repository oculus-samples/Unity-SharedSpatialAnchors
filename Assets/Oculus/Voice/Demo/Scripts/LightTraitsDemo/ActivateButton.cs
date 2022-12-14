using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Voice;
using UnityEngine;
using UnityEngine.UI;

public class ActivateButton : MonoBehaviour
{
    private bool _active = false;

    public AppVoiceExperience appVoiceExperience;

    private Text _buttonLabel;

    private void Awake()
    {
        _buttonLabel = GetComponentInChildren<Text>();
    }

    public void ToggleActive()
    {
        SetButtonActiveState(!_active);
    }

    public void SetButtonActiveState(bool newActiveState)
    {
        if (_active != newActiveState)
        {
            _active = newActiveState;
            
            if (_active)
            {
                _buttonLabel.text = "Listening";
            
                appVoiceExperience.Activate();
            }
            else
            {
                _buttonLabel.text = "Activate";
            
                appVoiceExperience.Deactivate();
            }
        }
    }
}
