using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PhotonLobbyRow : MonoBehaviour
{
    [SerializeField]
    TMPro.TextMeshPro lobbyRowText;

    public void SetRowText(string text)
    {
        lobbyRowText.text = text;
    }

    public string GetRowText()
    {
        return lobbyRowText.text;
    }
}
