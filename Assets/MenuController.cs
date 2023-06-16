using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [SerializeField]
    private Transform referencePoint;

    private void Start()
    {
        transform.parent = referencePoint;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        if(transform.position == Vector3.zero)
            gameObject.SetActive(false);
        else
            gameObject.SetActive(true);
    }

    public void OnLoadDemoScene(int iSceneIndex)
    {
        Debug.Log($"{nameof(OnLoadDemoScene)}:{iSceneIndex}");
        SceneManager.LoadScene(iSceneIndex);
    }
}
