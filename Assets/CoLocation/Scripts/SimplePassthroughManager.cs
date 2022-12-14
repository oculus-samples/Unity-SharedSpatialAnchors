using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplePassthroughManager : MonoBehaviour
{
    public static SimplePassthroughManager Instance;
    [SerializeField] private MeshRenderer passthroughRenderer;
    [SerializeField] private bool fadeOutOnStart = true;
    private float setValue, currentValue;
    private float fadeTime = 2f;
    private Coroutine fade;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        passthroughRenderer.gameObject.SetActive(true);
        currentValue = GetPassthrough();
        setValue = GetPassthrough();
        if (fadeOutOnStart)
        {
            FadeOut();
        }
    }

    private void Update()
    {
        if(CoLocatedPassthroughManager.Instance != null)
        {
            passthroughRenderer.transform.position = CoLocatedPassthroughManager.Instance.localHead.position;
        }
    }

    public void FadeOut()
    {
        SetPassthrough(0f, 2f);
    }

    IEnumerator Fade()
    {
        while (setValue != currentValue)
        {
            yield return null;
            if (Mathf.Abs(setValue - currentValue) < (Time.deltaTime / fadeTime))
            {
                currentValue = setValue;
            }
            else
            {
                currentValue += ((currentValue > setValue) ? -1 : 1) * Time.deltaTime / fadeTime;
            }
            passthroughRenderer.material.SetFloat("_Alpha", currentValue);
            passthroughRenderer.material.SetFloat("_Darken", currentValue);
        }
    }

    public void SetPassthrough(float val, float speed = 1f)
    {
        val = Mathf.Clamp01(val);
        if (val != setValue)
        {
            fadeTime = speed;
            setValue = val;
            if (fade != null)
            {
                StopCoroutine(fade);
            }
            fade = StartCoroutine(Fade());
        }
    }

    public float GetPassthrough()
    {
        return passthroughRenderer.material.GetFloat("_Darken");
    }
}
