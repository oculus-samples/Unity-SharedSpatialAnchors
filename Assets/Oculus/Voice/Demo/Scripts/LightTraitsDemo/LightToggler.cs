using System;
using Facebook.WitAi;
using Facebook.WitAi.Lib;
using UnityEngine;

public class LightToggler : MonoBehaviour
{
    public enum LightState
    {
        On,
        Off
    }

    private const string EMISSION_COLOR = "_EmissionColor";
    private const string EMISSION = "_EMISSION";

    private LightState _lightState;

    private Material _material;

    private Color _offColor = Color.black;
    private Color _onColor;

    // Start is called before the first frame update
    void Start()
    {
        _material = GetComponent<Renderer>().material;

        _onColor = _material.GetColor(EMISSION_COLOR);

        SetLightState((LightState.Off));
    }

    public void OnResponse(WitResponseNode commandResult)
    {
        var traitValue = commandResult.GetTraitValue("wit$on_off").Replace('o', 'O');

        SetLightState((LightState)Enum.Parse(typeof(LightState), traitValue));
    }

    public void SetLightState(LightState newState)
    {
        switch (newState)
        {
            case LightState.On:

                if (_lightState == LightState.On)
                    break;

                _material.EnableKeyword(EMISSION);

                _material.SetColor(EMISSION_COLOR, _onColor);

                break;

            case LightState.Off:

                if (_lightState == LightState.Off)
                    break;

                _material.DisableKeyword(EMISSION);

                _material.SetColor(EMISSION_COLOR, _offColor);

                break;
        }

        _lightState = newState;
    }
}
