using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Common;

public class WorldGenerationController : MonoBehaviour
{
    [SerializeField]
    GameObject          wallPrefab;

    [SerializeField]
    GameObject          obstaclePrefab;

    List<GameObject>    sceneObjects = new List<GameObject>();

    bool                sceneAlignmentApplied = false;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public Transform FindDeskTransform(Scene scene)
    {
        GameObject deskObject = null;
        foreach (var obstacle in scene.obstacles)
        {
            if (obstacle.type == ObstacleType.Desk)
                deskObject = PopulateScaledObstacle(obstacle, ObstacleType.Desk);
        }

        return deskObject.transform;
    }

    public void GenerateWorld(Scene scene)
    {
        SampleController.Instance.Log("Generating World...");
        SampleController.Instance.Log("Walls: " + scene.walls.Length);

        if(scene.floor != null)
            SampleController.Instance.Log("Floor: YES");
        else
            SampleController.Instance.Log("Floor: NO");

        SampleController.Instance.Log("Obstacles: " + scene.obstacles.Length);

        foreach (GameObject obj in sceneObjects)
            Destroy(obj);
        sceneObjects.Clear();

        GameObject newFloor = GameObject.Instantiate(wallPrefab,scene.floor.position,scene.floor.rotation);
        newFloor.transform.localScale = new Vector3(scene.floor.rect.width, scene.floor.rect.height, 0.07f);
        newFloor.transform.rotation = scene.floor.rotation * Quaternion.Euler(180, 0, 0);
        newFloor.SetActive(sceneAlignmentApplied);
        sceneObjects.Add(newFloor);

        GameObject newCeiling = GameObject.Instantiate(wallPrefab, scene.ceiling.position, scene.ceiling.rotation);
        newCeiling.transform.localScale = new Vector3(scene.ceiling.rect.width, scene.ceiling.rect.height, 0.07f);
        newCeiling.transform.rotation = scene.ceiling.rotation * Quaternion.Euler(180, 0, 0);
        newCeiling.SetActive(sceneAlignmentApplied);
        sceneObjects.Add(newCeiling);

        foreach (var wall in scene.walls)
        {
            GameObject newWall = GameObject.Instantiate(wallPrefab,wall.position,wall.rotation);
            newWall.transform.localScale = new Vector3(wall.rect.width, wall.rect.height, 0.07f);
            newWall.transform.rotation = wall.rotation * Quaternion.Euler(0, 180, 0);
            newWall.SetActive(sceneAlignmentApplied);
            sceneObjects.Add(newWall);
        }
        GameObject deskObject = null;
        foreach (var obstacle in scene.obstacles)
        {
            deskObject = PopulateScaledObstacle(obstacle, ObstacleType.Desk);
            deskObject.SetActive(sceneAlignmentApplied);
            sceneObjects.Add(deskObject);
        }

        StartCoroutine(PlayIntroPassthrough());
    }

    private GameObject PopulateScaledObstacle(Obstacle obstacle, ObstacleType type)
    {
        Quaternion obstacleRotation = obstacle.rotation;
        Quaternion toUp = Quaternion.AngleAxis(-Vector3.Angle(obstacleRotation * Vector3.up, Vector3.up), obstacle.rotation * Vector3.right);
        obstacleRotation = toUp * obstacleRotation;
        Vector3 obstacleScale;

        float dotXY = Mathf.Abs(Vector3.Dot(Vector3.Cross(obstacle.rotation * Vector3.right, obstacle.rotation * Vector3.up), Vector3.up));
        float dotXZ = Mathf.Abs(Vector3.Dot(Vector3.Cross(obstacle.rotation * Vector3.right, obstacle.rotation * Vector3.forward), Vector3.up));
        float dotYZ = Mathf.Abs(Vector3.Dot(Vector3.Cross(obstacle.rotation * Vector3.up, obstacle.rotation * Vector3.forward), Vector3.up));

        if (dotXY > dotXZ && dotXY > dotYZ)
        {
            obstacleScale = new Vector3(obstacle.boundingBox.size.x, obstacle.boundingBox.size.z, obstacle.boundingBox.size.y);
        }
        else if (dotXZ > dotXY && dotXZ > dotYZ)
        {
            obstacleScale = new Vector3(obstacle.boundingBox.size.x, obstacle.boundingBox.size.y, obstacle.boundingBox.size.z);
        }
        else
        {
            obstacleScale = new Vector3(obstacle.boundingBox.size.y, obstacle.boundingBox.size.x, obstacle.boundingBox.size.z);
        }

        Vector3 deskPos = obstacle.position;
        deskPos.y += obstacleScale.y;

        GameObject obstacleDeskGameObject = Object.Instantiate(obstaclePrefab, deskPos, obstacleRotation);
        obstacleDeskGameObject.transform.localScale = obstacleScale;
        return obstacleDeskGameObject;
    }

    public void ShowSceneObjects()
    {
        foreach(GameObject sceneObj in sceneObjects)
        {
            sceneObj.SetActive(true);
        }

        sceneAlignmentApplied = true;
    }

    IEnumerator PlayIntroPassthrough()
    {
        // fade in edges
        float timer = 0.0f;
        float lerpTime = 4.0f;
        while (timer <= lerpTime)
        {
            timer += Time.deltaTime;

            Color edgeColor = Color.white;
            edgeColor.a = Mathf.Clamp01(timer / 3.0f); // fade from transparent
            //_passthroughLayer.edgeColor = edgeColor;

            float normTime = Mathf.Clamp01(timer / lerpTime);
            //_fadeSphere.sharedMaterial.SetColor("_Color", Color.Lerp(Color.black, Color.clear, normTime));

            //VirtualRoom.Instance.SetEdgeEffectIntensity(normTime);
            foreach (GameObject sceneObj in sceneObjects)
            {
                sceneObj.GetComponentInChildren<MeshRenderer>().material.SetFloat("_EffectIntensity", 1.0f);
                sceneObj.GetComponentInChildren<MeshRenderer>().material.SetFloat("_EdgeTimeline", normTime);
            }
            /*
            // once lerpTime is over, fade in normal passthrough
            if (timer >= lerpTime)
            {
                PassthroughStylist.PassthroughStyle normalPassthrough = new PassthroughStylist.PassthroughStyle(
                    new Color(0, 0, 0, 0),
                    1.0f,
                    0.0f,
                    0.0f,
                    0.0f,
                    false,
                    Color.white,
                    Color.black,
                    Color.white);
                _passthroughStylist.ShowStylizedPassthrough(normalPassthrough, 5.0f);
                _fadeSphere.gameObject.SetActive(false);
            }
            */
            yield return null;
        }
    }
}
