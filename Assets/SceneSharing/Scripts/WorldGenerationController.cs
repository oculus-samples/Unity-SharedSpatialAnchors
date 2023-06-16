/*
* Copyright (c) Meta Platforms, Inc. and affiliates.
* All rights reserved.
*
* Licensed under the Oculus SDK License Agreement (the "License");
* you may not use the Oculus SDK except in compliance with the License,
* which is provided at the time of installation or download, or which
* otherwise accompanies this software in either electronic or hard copy form.
*
* You may obtain a copy of the License at
*
* https://developer.oculus.com/licenses/oculussdk/
*
* Unless required by applicable law or agreed to in writing, the Oculus SDK
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Common;

public class WorldGenerationController : MonoBehaviour
{
    [SerializeField]
    GameObject wallPrefab;

    [SerializeField]
    GameObject obstaclePrefab;

    List<GameObject> sceneObjects = new List<GameObject>();

    bool sceneAlignmentApplied = false;

    public void GenerateWorld(Scene scene)
    {
        SampleController.Instance.Log("Updating Generated World...");
        SampleController.Instance.Log($"Walls: {scene.walls.Length}");
        SampleController.Instance.Log($"Floor: {scene.floor}");
        SampleController.Instance.Log($"Obstacles: {scene.obstacles.Length}");

        foreach (GameObject obj in sceneObjects)
            Destroy(obj);
        sceneObjects.Clear();

        GameObject newFloor = GameObject.Instantiate(wallPrefab, scene.floor.position, scene.floor.rotation);
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
            GameObject newWall = GameObject.Instantiate(wallPrefab, wall.position, wall.rotation);
            newWall.transform.localScale = new Vector3(wall.rect.width, wall.rect.height, 0.07f);
            newWall.transform.rotation = wall.rotation * Quaternion.Euler(0, 180, 0);
            newWall.SetActive(sceneAlignmentApplied);
            sceneObjects.Add(newWall);
        }

        GameObject newObject;
        foreach (var obstacle in scene.obstacles)
        {
            newObject = PopulateScaledObstacle(obstacle);
            newObject.SetActive(sceneAlignmentApplied);
            sceneObjects.Add(newObject);
        }

        StartCoroutine(PlayIntroPassthrough());
    }

    private GameObject PopulateScaledObstacle(Obstacle obstacle)
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

        Vector3 objPos = obstacle.position;
        if (obstacle.type != ObstacleType.Window && obstacle.type != ObstacleType.Door)
            objPos.y -= (obstacleScale.y / 2.0f);

        GameObject obstacleGameObject = Object.Instantiate(obstaclePrefab, objPos, obstacleRotation);
        obstacleGameObject.transform.localScale = obstacleScale;
        return obstacleGameObject;
    }

    public void ShowSceneObjects()
    {
        foreach (GameObject sceneObj in sceneObjects)
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

            float normTime = Mathf.Clamp01(timer / lerpTime);

            foreach (GameObject sceneObj in sceneObjects)
            {
                sceneObj.GetComponentInChildren<MeshRenderer>().material.SetFloat("_EffectIntensity", 1.0f);
                sceneObj.GetComponentInChildren<MeshRenderer>().material.SetFloat("_EdgeTimeline", normTime);
            }
            yield return null;
        }
    }
}
