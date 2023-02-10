using System;
using System.IO;
using System.IO.Compression;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using Plane = Common.Plane;
using ExitGames.Client.Photon;
using Photon.Pun;
using System.Runtime.Serialization.Formatters.Binary;

namespace Common
{
    public class SceneApiSceneCaptureStrategy : SceneCaptureStrategy
    {
        private static readonly Dictionary<string, ObstacleType> _obstacleTypeMap = new Dictionary<string, ObstacleType>()
{
{"DESK", ObstacleType.Desk}, {"COUCH", ObstacleType.Couch}, {"OTHER", ObstacleType.Misc}
};

        private Action<Scene> _onComplete;

        private ulong _roomLayoutQuery = ulong.MinValue;
        private ulong _roomEntitiesQuery = ulong.MinValue;

        private List<Plane> _walls;
        private List<Obstacle> _obstacles;
        private Scene _scene;

        private bool _idQueryCompleted;
        private int _componentEnablingInProgress;

        public const string RoomDataKey = "roomData";

        [SerializeField]
        WorldGenerationController worldGenerationController;

        public override void Start()
        {

        }

        public void InitSceneCapture()
        {
            Debug.Log("Subscribing all OVRManager listeners");
            OVRManager.SceneCaptureComplete += OnSceneCaptureComplete;
            OVRManager.SpaceQueryComplete += OnSpaceQueryCompleted;
            OVRManager.SpaceSetComponentStatusComplete += OnSpaceSetComponentStatusComplete;
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        public void BeginCaptureScene()
        {
            CaptureScene(OnSceneCreated);
        }

        public override void CaptureScene(Action<Scene> onComplete)
        {
            SampleController.Instance.Log("SceneApiSceneCaptureStrategy::CaptureScene - Scene Capture Started");

            _onComplete = onComplete;
            StartRoomCapture();
        }

        private void OnSceneCreated(Scene scene)
        {
            SampleController.Instance.Log("PhotonAnchorManager::OnSceneCreated - Scene Capture Complete");
        }

        private void Cleanup()
        {
            SampleController.Instance.Log("Unsubscribing all OVRManager listeners");
            OVRManager.SceneCaptureComplete -= OnSceneCaptureComplete;
            OVRManager.SpaceQueryComplete -= OnSpaceQueryCompleted;
            OVRManager.SpaceSetComponentStatusComplete -= OnSpaceSetComponentStatusComplete;
        }

        private void StartRoomCapture()
        {
            _walls = new List<Plane>();
            _obstacles = new List<Obstacle>();
            _scene = new Scene();

            var req = "ROOMBOX";
            var id = ulong.MinValue;
            SampleController.Instance.Log("requesting scene capture");
            var success = OVRPlugin.RequestSceneCapture(req, out id);
            if (!success)
            {
                SampleController.Instance.Log("Failure requesting scene capture");
            }
        }

        /// <summary>
        /// This is called when the user finished the room capture flow
        /// </summary>
        private void OnSceneCaptureComplete(ulong requestId, bool result)
        {
            SampleController.Instance.Log("Scene capture complete");
            LoadRoomLayout();
        }

        /// <summary>
        /// Create a query for the RoomLayout
        /// </summary>
        private void LoadRoomLayout()
        {
            var spatialEntityFilterInfoComponents = new OVRPlugin.SpaceFilterInfoComponents
            {
                Components = new OVRPlugin.SpaceComponentType[OVRPlugin.SpaceFilterInfoComponentsMaxSize],
                NumComponents = 1,
            };
            spatialEntityFilterInfoComponents.Components[0] = OVRPlugin.SpaceComponentType.RoomLayout;
            var queryInfo = new OVRPlugin.SpaceQueryInfo()
            {
                QueryType = OVRPlugin.SpaceQueryType.Action,
                MaxQuerySpaces = 30,
                Timeout = 0,
                Location = OVRPlugin.SpaceStorageLocation.Local,
                ActionType = OVRPlugin.SpaceQueryActionType.Load,
                FilterType = OVRPlugin.SpaceQueryFilterType.Components,
                ComponentsInfo = spatialEntityFilterInfoComponents
            };
            SampleController.Instance.Log("Starting Room Layout query");
            OVRPlugin.QuerySpaces(queryInfo, out _roomLayoutQuery);
        }

        /// <summary>
        /// This is called once per query, when all results have been returned
        /// </summary>
        private void OnSpaceQueryCompleted(ulong requestId, bool result)
        {
            Debug.Log($"{MethodBase.GetCurrentMethod().Name} requestId: [{requestId}]");
            OVRPlugin.RetrieveSpaceQueryResults(requestId, out var results);
            if (requestId == _roomLayoutQuery)
            {
                // After the roomlayout query we now load the individual entities in the room
                var ids = ExtractEntityIdsFromRoomLayout(results);
                LoadEntitiesById(ids.ToList());
            }
            else if (requestId == _roomEntitiesQuery)
            {
                EnableBaseComponents(results);
                _idQueryCompleted = true;
                EndRoomCaptureIfReady();
            }
            else
            {
                Debug.Log($"Unknown request ID [{requestId}]");
            }
        }

        /// <summary>
        /// Fetches the IDs of the all entities in the RoomLayout
        /// </summary>
        private HashSet<Guid> ExtractEntityIdsFromRoomLayout(OVRPlugin.SpaceQueryResult[] results)
        {
            var idsToQuery = new HashSet<Guid>();
            for (var i = 0; i < results.Length; i++)
            {
                var space = results[i].space;

                // This will fetch all entities in this room: Walls, floor, ceiling, furniture, ...
                var success = OVRPlugin.GetSpaceContainer(space, out var entityUuids);
                Debug.Log($"SpatialEntityGetContainer: success [{success}], count [{entityUuids.Length}]");
                if (!success)
                    continue;
                foreach (var uuid in entityUuids)
                {
                    Debug.Log($"SpatialEntityGetContainer: UUID [{uuid.ToString()}]");
                    // TODO check if uuid is valid? (i.e. non-zero)
                    idsToQuery.Add(uuid);
                }
            }
            return idsToQuery;
        }

        private void LoadEntitiesById(List<Guid> ids)
        {
            var numIds = Math.Min(OVRPlugin.SpaceFilterInfoIdsMaxSize, ids.Count);
            var idInfo = new OVRPlugin.SpaceFilterInfoIds()
            {
                NumIds = Math.Min(OVRPlugin.SpaceFilterInfoIdsMaxSize, ids.Count),
                Ids = new Guid[OVRPlugin.SpaceFilterInfoIdsMaxSize]
            };
            for (var i = 0; i < idInfo.NumIds; ++i)
            {
                idInfo.Ids[i] = ids[i];
                Debug.Log($"{MethodBase.GetCurrentMethod().Name} UUID to query [{ids[i]}]");
            }

            var queryInfo = new OVRPlugin.SpaceQueryInfo()
            {
                QueryType = OVRPlugin.SpaceQueryType.Action,
                MaxQuerySpaces = 30,
                Timeout = 0,
                Location = OVRPlugin.SpaceStorageLocation.Local,
                ActionType = OVRPlugin.SpaceQueryActionType.Load,
                FilterType = OVRPlugin.SpaceQueryFilterType.Ids,
                IdInfo = idInfo
            };

            Debug.Log($"Starting entity ID query for [{numIds}] ids: [{string.Join(", ", idInfo.Ids.Select(uuid => uuid.ToString()))}]");
            OVRPlugin.QuerySpaces(queryInfo, out _roomEntitiesQuery);
        }

        /// <summary>
        /// Enable Storable & Locatable on the given entities. These are base components needed to get the position of objects
        /// </summary>
        private void EnableBaseComponents(OVRPlugin.SpaceQueryResult[] entities)
        {
            for (var i = 0; i < entities.Length; i++)
            {
                var space = entities[i].space;
                DebugLogInitialPose(space);
                // Enable Storable and Locatable components, as they are not enabled when the space is loaded from the storage for the first time.
                // EnableComponentIfNecessary(space, OVRPlugin.SpaceComponentType.Storable);
                if (!EnableComponentIfNecessary(space, OVRPlugin.SpaceComponentType.Locatable))
                {
                    AddEntityToRoom(space);
                }
            }
        }

        private void DebugLogInitialPose(ulong space)
        {
            OVRPlugin.Posef posef;
            if (OVRPlugin.TryLocateSpace(space, OVRPlugin.GetTrackingOriginType(), out posef))
            {
                Debug.Log($"Initial posef for entity [{space}] = {posef.ToString()}");
            }
        }

        /// <summary>
        /// Converts the entity into a object for the Room.
        /// The entity needs to have the Locatable component enabled!
        /// </summary>
        /// <param name="space"></param>
        private void AddEntityToRoom(ulong space)
        {
            Debug.Log($"Adding entity for space [{space}]");

            // Get the locatable component
            OVRPlugin.GetSpaceComponentStatus(space, OVRPlugin.SpaceComponentType.Locatable, out var locatableEnabled, out _);
            if (!locatableEnabled)
            {
                Debug.LogWarning($"Entity [{space}] is not Locatable!");
                return;
            }

            OVRPlugin.Posef posef;
            if (OVRPlugin.TryLocateSpace(space, OVRPlugin.GetTrackingOriginType(), out posef) == false)
            {
                Debug.LogWarning($"TryLocateSpace failed [{space}]");
                return;
            }

            var worldPose = OVRExtensions.ToWorldSpacePose(posef.ToOVRPose(), Camera.main);

            // Get the semantic labels
            OVRPlugin.GetSpaceSemanticLabels(space, out var labels);
            Debug.Log($"GetSpaceSemanticLabels space [{space}] labels [{labels}]");

            // Handle both 2D and 3D bounding boxes
            OVRPlugin.GetSpaceComponentStatus(space, OVRPlugin.SpaceComponentType.Bounded2D, out var bounded2dEnabled, out _);
            Debug.Log($"{MethodBase.GetCurrentMethod().Name} space: [{space}] bounded2dEnabled [{bounded2dEnabled}]");

            OVRPlugin.GetSpaceComponentStatus(space, OVRPlugin.SpaceComponentType.Bounded3D, out var bounded3dEnabled, out _);
            Debug.Log($"{MethodBase.GetCurrentMethod().Name} space: [{space}] bounded3dEnabled [{bounded3dEnabled}]");

            if (bounded3dEnabled)
            {
                var success = OVRPlugin.GetSpaceBoundingBox3D(space, out var boundsf);
                Debug.Log($"GetSpaceBoundingBox3D success [{success}]");
                if (!success) return;

                Add3DEntityToRoom(boundsf, worldPose, labels);
            }
            else if (bounded2dEnabled)
            {
                var success = OVRPlugin.GetSpaceBoundingBox2D(space, out var rectf);
                Debug.Log($"GetSpaceBoundingBox2D success [{success}]");
                if (!success) return;

                Add2DEntityToRoom(rectf, worldPose, labels);
            }
            else
            {
                Debug.LogWarning($"{MethodBase.GetCurrentMethod().Name} entity has no bounding box - space: [{space}]");
                return;
            }
        }

        private void Add2DEntityToRoom(OVRPlugin.Rectf rectf, OVRPose worldPose, string labels)
        {
            switch (labels)
            {
                case "WALL_FACE":
                    {
                        var plane = CreatePlane(rectf, worldPose, labels);
                        _walls.Add(plane);
                        break;
                    }
                case "FLOOR":
                    {
                        var plane = CreatePlane(rectf, worldPose, labels);
                        _scene.floor = plane;
                        break;
                    }
                case "CEILING":
                    {
                        var plane = CreatePlane(rectf, worldPose, labels);
                        _scene.ceiling = plane;
                        break;
                    }
                case "DESK":
                case "COUCH":
                case "OTHER":
                    {
                        if (!_obstacleTypeMap.TryGetValue(labels, out var obstacleType))
                        {
                            Debug.LogWarning($"{MethodBase.GetCurrentMethod().Name} Unhandled labels: [{labels}]");
                            return;
                        }
                        var obstacle = CreateObstacle(worldPose, rectf, labels, obstacleType);
                        _obstacles.Add(obstacle);
                        break;
                    }
                default:
                    Debug.LogWarning($"{MethodBase.GetCurrentMethod().Name} Unhandled labels: [{labels}]");
                    break;
            }
        }

        private void Add3DEntityToRoom(OVRPlugin.Boundsf boundsf, OVRPose worldPose, string labels)
        {
            if (!_obstacleTypeMap.TryGetValue(labels, out var obstacleType))
            {
                Debug.LogWarning($"{MethodBase.GetCurrentMethod().Name} Unhandled labels: [{labels}]");
                return;
            }

            var obstacle = CreateObstacle(worldPose, boundsf, labels, obstacleType);
            _obstacles.Add(obstacle);
        }

        private static Plane CreatePlane(OVRPlugin.Rectf rectf, OVRPose worldPose, string labels)
        {
            var plane = new Plane
            {
                rect = new Rect(rectf.Pos.x, rectf.Pos.y, rectf.Size.w, rectf.Size.h),
                position = worldPose.position,
                rotation = worldPose.orientation,
            };
            //Debug.Log($"Plane Data for [{labels}]:" + plane.ToJson());
            return plane;
        }

        private static Obstacle CreateObstacle(OVRPose worldPose, OVRPlugin.Rectf rectf, string labels, ObstacleType obstacleType)
        {
            var position = worldPose.position;
            position.y /= 2.0f;

            var size = new Vector3(rectf.Size.w, rectf.Size.h, worldPose.position.y);

            var obstacle = new Obstacle
            {
                position = position,
                rotation = worldPose.orientation,
                type = obstacleType,
                boundingBox = new Bounds(Vector3.zero, size)
            };

            //Debug.Log($"Obstacle Data for [{labels}]:" + obstacle.ToJson());
            return obstacle;
        }

        private static Obstacle CreateObstacle(OVRPose worldPose, OVRPlugin.Boundsf boundsf, string labels, ObstacleType obstacleType)
        {
            var position = worldPose.position;
            position.y /= 2.0f;

            var size = new Vector3(boundsf.Size.w, boundsf.Size.h, boundsf.Size.d);

            var obstacle = new Obstacle
            {
                position = position,
                rotation = worldPose.orientation,
                type = obstacleType,
                boundingBox = new Bounds(Vector3.zero, size)
            };

            //Debug.Log($"Obstacle Data for [{labels}]:" + obstacle.ToJson());
            return obstacle;
        }

        /// <summary>
        /// Enables the given component on the entity if not already enabled
        /// </summary>
        /// <returns>true if it was necessary to enable</returns>
        private bool EnableComponentIfNecessary(ulong space, OVRPlugin.SpaceComponentType componentType)
        {
            OVRPlugin.GetSpaceComponentStatus(space, componentType, out var enabled, out _);
            if (enabled)
            {
                Debug.Log($"{MethodBase.GetCurrentMethod().Name} component [{componentType}] is already enabled for space [{space}]");
                return false;
            }

            const double dTimeout = 10 * 1000f;
            OVRPlugin.SetSpaceComponentStatus(space, componentType, true, dTimeout, out var requestId);
            Debug.Log($"{MethodBase.GetCurrentMethod().Name} component [{componentType}] requested for space [{space}] with requestId [{requestId}]");
            _componentEnablingInProgress++;
            Debug.Log($"_componentEnablingInProgress now at [{_componentEnablingInProgress}]");
            return true;
        }

        private void OnSpaceSetComponentStatusComplete(ulong requestId, bool result, OVRSpace space, Guid id, OVRPlugin.SpaceComponentType componentType, bool enabled)
        {
            Debug.Log($"{MethodBase.GetCurrentMethod().Name} requestId [{requestId}] for space [{space}] result [{result}]");
            _componentEnablingInProgress--;
            Debug.Log($"_componentEnablingInProgress now at [{_componentEnablingInProgress}]");
            if (componentType == OVRPlugin.SpaceComponentType.Locatable)
            {
                AddEntityToRoom(space);
            }

            EndRoomCaptureIfReady();
        }

        /// <summary>
        /// Ends the room capture if we're not waiting on any callbacks anymore
        /// </summary>
        private void EndRoomCaptureIfReady()
        {
            //AnchorSession.Log($"EndRoomCaptureIfReady _idQueryCompleted [{_idQueryCompleted}] _componentEnablingInProgress [{_componentEnablingInProgress}]");
            if (!_idQueryCompleted || _componentEnablingInProgress > 0) return;

            _scene.walls = _walls.ToArray();
            _scene.obstacles = _obstacles.ToArray();
            EndRoomCapture(_scene);
        }

        private void EndRoomCapture(Scene scene)
        {
            //OMEGA TODO - Clean up the callbacks to not conflict with the shared anchor system
            //Cleanup();

            ShareRoomOnPhoton(scene);

            if (_onComplete != null)
                _onComplete(scene);
        }

        public void ShareRoomOnPhoton(Scene scene = null)
        {
            if (scene == null)
                scene = _scene;

            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            {
                byte[] serializedData = Encoding.ASCII.GetBytes(JsonUtility.ToJson(scene));

                if (serializedData != null)
                {
                    var roomProps = new ExitGames.Client.Photon.Hashtable
                    {
                        [RoomDataKey] = serializedData
                    };

                    PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);
                }
            }
        }

        public void AlignmentApplied()
        {
            if (Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                BeginCaptureScene();
            }

            if (worldGenerationController)
            {
                worldGenerationController.ShowSceneObjects();
            }
        }
    }
}
