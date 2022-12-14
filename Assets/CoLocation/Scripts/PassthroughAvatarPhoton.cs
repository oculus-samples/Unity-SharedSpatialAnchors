using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class PassthroughAvatarPhoton : MonoBehaviour, IPunObservable
{
    public GameObject headPrefab, leftPrefab, rightPrefab;
    private Transform head, right, left, body;
    private PhotonView photonView;
    private AvatarPassthrough passthrough;

    private void Start()
    {
        photonView = GetComponent<PhotonView>();
        if (!photonView.IsMine)
        {
            body = new GameObject("Player" + photonView.CreatorActorNr).transform;
            head = headPrefab == null ?
                new GameObject("head").transform :
                Instantiate(headPrefab, Vector3.zero, Quaternion.identity).transform;
            right = rightPrefab == null ?
                new GameObject("right").transform :
                Instantiate(headPrefab, Vector3.zero, Quaternion.identity).transform;
            left = leftPrefab == null ?
                new GameObject("left").transform :
                Instantiate(headPrefab, Vector3.zero, Quaternion.identity).transform;
            head.SetParent(body);
            right.SetParent(body);
            left.SetParent(body);
        }
        passthrough = CoLocatedPassthroughManager.Instance.AddCoLocalUser(head, right, left);
        passthrough.IsMine = photonView.IsMine;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(CoLocatedPassthroughManager.Instance.localHead.position);
            stream.SendNext(CoLocatedPassthroughManager.Instance.localHead.eulerAngles);
            stream.SendNext(CoLocatedPassthroughManager.Instance.localLeft.position);
            stream.SendNext(CoLocatedPassthroughManager.Instance.localLeft.eulerAngles);
            stream.SendNext(CoLocatedPassthroughManager.Instance.localRight.position);
            stream.SendNext(CoLocatedPassthroughManager.Instance.localRight.eulerAngles);
            stream.SendNext(CoLocatedPassthroughManager.Instance.location);
        }
        else
        {
            head.position = (Vector3)stream.ReceiveNext();
            head.eulerAngles = (Vector3)stream.ReceiveNext();
            left.position = (Vector3)stream.ReceiveNext();
            left.eulerAngles = (Vector3)stream.ReceiveNext();
            right.position = (Vector3)stream.ReceiveNext();
            right.eulerAngles = (Vector3)stream.ReceiveNext();
            passthrough.location = (string)stream.ReceiveNext();
        }
    }

    private void OnDisable()
    {
        CoLocatedPassthroughManager.Instance.RemoveCoLocalUser(head);
        Destroy(body.gameObject);
    }
}
