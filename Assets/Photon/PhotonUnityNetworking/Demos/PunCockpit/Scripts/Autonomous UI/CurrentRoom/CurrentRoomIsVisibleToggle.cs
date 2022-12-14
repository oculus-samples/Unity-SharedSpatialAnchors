// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CurrentRoomIsVisibleToggle.cs" company="Exit Games GmbH">
//   Part of: Pun Cockpit Demo
// </copyright>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Photon.Pun.Demo.Cockpit
{
	/// <summary>
	/// PhotonNetwork.CurrentRoom.IsVisible UI Toggle
	/// </summary>
	[RequireComponent(typeof(Toggle))]
	public class CurrentRoomIsVisibleToggle : MonoBehaviour, IPointerClickHandler
	{
		Toggle _toggle;


		// Use this for initialization
		void OnEnable()
		{
			_toggle = GetComponent<Toggle>();
		}

		void Update()
		{
			if (PhotonNetwork.CurrentRoom == null && _toggle.interactable)
			{
				_toggle.interactable = false;
				
			}
			else if (PhotonNetwork.CurrentRoom != null && !_toggle.interactable)
			{
				_toggle.interactable = true;
			}

			if (PhotonNetwork.CurrentRoom!=null && PhotonNetwork.CurrentRoom.IsVisible != _toggle.isOn)
			{
				Debug.Log("Update toggle : PhotonNetwork.CurrentRoom.IsVisible = " + PhotonNetwork.CurrentRoom.IsVisible, this);
				_toggle.isOn = PhotonNetwork.CurrentRoom.IsVisible;
			}
		}


		public void ToggleValue(bool value)
		{
			if (PhotonNetwork.CurrentRoom != null)
			{
				Debug.Log("PhotonNetwork.CurrentRoom.IsVisible = " + value, this);
				PhotonNetwork.CurrentRoom.IsVisible = value;
			}

			
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			ToggleValue(_toggle.isOn);
		}
	}
}
