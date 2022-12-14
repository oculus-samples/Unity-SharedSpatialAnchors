using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhotonPun = Photon.Pun;
using PhotonRealtime = Photon.Realtime;
using Oculus.Interaction;

public class PhotonLobbyPanel : MonoBehaviour
{
    [SerializeField]
    private GameObject              menuPanel;

    [SerializeField]
    private PhotonAnchorManager     anchorManager;

    [SerializeField]
    private GameObject              roomLayoutPanel;

    [SerializeField]
    private GameObject              roomLayoutPanelRowPrefab;

    List<GameObject>                lobbyRowList = new List<GameObject>();

    [SerializeField]
    private PokeInteractable        createRoomPokeInter;

    [SerializeField]
    private PokeInteractable        joinRoomPokeInter;

    // Start is called before the first frame update
    void Start()
    {
        DisableRoomButtons();
    }

    // Update is called once per frame
    void Update()
    {
        //Input for testing without a Quest device - TODO: Remove (omega)
        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            OnCreateRoomButtonPressed();           
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            OnFindRoomButtonPressed();
        }

        if(Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (lobbyRowList.Count > 0)
            {
                AttemptToJoinRoom(lobbyRowList[0].GetComponent<PhotonLobbyRow>().GetRowText());
            }
        }
        //-------------------------------------------------------------------------------
    }

    public void OnCreateRoomButtonPressed()
    {
        SampleController.Instance.Log("OnCreateRoomButtonPressed");

        if (PhotonPun.PhotonNetwork.IsConnected)
        {
            if (PhotonPun.PhotonNetwork.NickName != "")
                anchorManager.CreateNewRoomForLobby(PhotonPun.PhotonNetwork.NickName);
            else
            {
                Random.InitState((int)(Time.time * 10000));
                string testName = "TestUser" + Random.Range(0, 1000);
                PhotonPun.PhotonNetwork.NickName = testName;
                anchorManager.CreateNewRoomForLobby(testName);
            }

            menuPanel.SetActive(true);
            gameObject.SetActive(false);
        }
        else
        {
            SampleController.Instance.Log("Attempting to reconnect and rejoin a room");
            PhotonPun.PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnFindRoomButtonPressed()
    {
        if (PhotonPun.PhotonNetwork.IsConnected){
            SampleController.Instance.Log("There are currently " + lobbyRowList.Count + " rooms in the lobby");
            roomLayoutPanel.SetActive(true);
        }
        else
        {
            SampleController.Instance.Log("Attempting to reconnect and rejoin a room");
            PhotonPun.PhotonNetwork.ConnectUsingSettings();
        }
    }

    public void OnJoinRoomButtonPressed(TMPro.TextMeshPro textObj)
    {
        AttemptToJoinRoom(textObj.text);
    }

    void AttemptToJoinRoom(string roomName)
    {
        SampleController.Instance.Log("OnJoinRoomButtonPressed");

        if (PhotonPun.PhotonNetwork.NickName == "")
        {
            string testName = "TestUser" + Random.Range(0, 1000);
            PhotonPun.PhotonNetwork.NickName = testName;
        }

        anchorManager.JoinRoomFromLobby(roomName);

        menuPanel.SetActive(true);
        gameObject.SetActive(false);
    }

    public void SetRoomList(List<PhotonRealtime.RoomInfo> roomList)
    {
        foreach(Transform roomTransform in roomLayoutPanel.transform)
        {
            if(roomTransform.gameObject != roomLayoutPanelRowPrefab)
                GameObject.Destroy(roomTransform.gameObject);
        }
        lobbyRowList.Clear();

        if(roomList.Count > 0)
        {
            for(int i = 0; i < roomList.Count; i++)
            {
                if (roomList[i].PlayerCount == 0)
                    continue;

                GameObject newLobbyRow = GameObject.Instantiate(roomLayoutPanelRowPrefab, roomLayoutPanel.transform);
                newLobbyRow.SetActive(true);
                newLobbyRow.GetComponent<PhotonLobbyRow>().SetRowText(roomList[i].Name);
                lobbyRowList.Add(newLobbyRow);
            }
        }
        else
        {
            /*
            //TODO - Remove this test data after using it to implement scrolling / paging
            for (int i = 0; i < 10; i++)
            {
                GameObject newLobbyRow = GameObject.Instantiate(roomLayoutPanelRowPrefab, roomLayoutPanel.transform);
                newLobbyRow.SetActive(true);
                newLobbyRow.GetComponent<PhotonLobbyRow>().SetRowText("Room#" + i);
                lobbyRowList.Add(newLobbyRow);
            }
            */
        }
    }

    private void DisableRoomButtons()
    {
        if (createRoomPokeInter)
        {
            createRoomPokeInter.enabled = false;
            TMPro.TextMeshPro buttonText = createRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = new Color(buttonText.color.r, buttonText.color.g, buttonText.color.b, 0.25f);
        }

        if (joinRoomPokeInter)
        {
            joinRoomPokeInter.enabled = false;
            TMPro.TextMeshPro buttonText = joinRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = new Color(buttonText.color.r, buttonText.color.g, buttonText.color.b, 0.25f);
        }
    }

    public void DisplayLobbyPanel()
    {
        gameObject.SetActive(true);
        menuPanel.SetActive(false);
    }

    public void EnableRoomButtons()
    {
        if (createRoomPokeInter)
        {
            createRoomPokeInter.enabled = true;
            TMPro.TextMeshPro buttonText = createRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = Color.white;
        }

        if (joinRoomPokeInter)
        {
            joinRoomPokeInter.enabled = true;
            TMPro.TextMeshPro buttonText = joinRoomPokeInter.GetComponentInChildren<TMPro.TextMeshPro>();
            if (buttonText)
                buttonText.color = Color.white;
        }
    }
}
