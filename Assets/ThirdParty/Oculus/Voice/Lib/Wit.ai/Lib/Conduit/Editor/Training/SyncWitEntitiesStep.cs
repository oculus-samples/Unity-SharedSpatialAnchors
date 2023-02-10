/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Meta.Conduit.Editor.Training;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Synchronizes the entities between Conduit (locally) and Wit.
    /// If an entity exists only in Wit then this step will create an enum for it locally.
    /// If the entity exists only on Conduit AND is referenced by any of the callbacks, then it's added to Wit.Ai.
    /// If it exists on both, then the missing values at each side are added.
    /// </summary>
    internal class SyncWitEntitiesStep : ProcessStep
    {
        // Maps CLR types to "some" Wit built in types. We can expand this list as needed.
        // Key is CLR type and value is Wit type.
        private readonly Dictionary<string, string> _builtInTypes = new Dictionary<string, string>()
        {
            {"Int32", "wit$number"}
        };

        internal SyncWitEntitiesStep(IWitHttp witHttp, Manifest manifest, Payload payload) : base("Ensure entities",
            witHttp, manifest, payload)
        {
        }

        public override IEnumerator Run(Action<string, float> updateProgress, StepResult completionCallback)
        {
            var allEntities = this.Manifest.Entities.ToList();

            // Add all the built in types to the manifest entities.
            foreach (var builtInType in _builtInTypes)
            {
                allEntities.Add(new ManifestEntity()
                {
                    Name = builtInType.Value,
                    ID = builtInType.Key,
                    Type = builtInType.Key,
                });
            }

            for (var i = 0; i < allEntities.Count; i++)
            {
                updateProgress(this.StepName, i / (float) allEntities.Count);
                var entity = allEntities[i];

                var entityExists = false;
                yield return EntityExists(entity.Name, (success, data) => entityExists = success);

                if (entityExists)
                {
                    var rolesAdded = false;

                    yield return this.AddRolesToExistingEntity(entity, (success, data) => rolesAdded = success);

                    if (!rolesAdded)
                    {
                        Payload.Error = $"Failed to update existing entity {entity}";
                        completionCallback(false, Payload.Error);
                        yield break;
                    }

                    continue;
                }

                yield return TrainEntity(entity, this.Manifest, completionCallback);
                yield break;
            }

            Payload.Error = "";
            completionCallback(true, "");
        }

        private IEnumerator EntityExists(string entity, StepResult completionCallback)
        {
            yield return this.WitHttp.MakeUnityWebRequest($"/entities/{entity}", WebRequestMethods.Http.Get, completionCallback);
        }

        private HashSet<WitRole> GetRolesForEntityType(string entityType)
        {
            var roles = new HashSet<WitRole>();
            foreach (var action in this.Manifest.Actions)
            {
                foreach (var parameter in action.Parameters)
                {
                    if (parameter.EntityType == entityType)
                    {
                        roles.Add(new WitRole()
                            {name = parameter.QualifiedName}
                        );
                    }
                }
            }

            return roles;
        }

        private IEnumerator AddRolesToExistingEntity(ManifestEntity entity, StepResult completionCallback)
        {
            Debug.Log("Adding roles to existing entity");
            WitIncomingEntity witIncomingEntity = null;
            yield return this.GetEntity(entity.Name, incomingEntity => witIncomingEntity = incomingEntity );
            var witOutgoingEntity = new WitOutgoingEntity(witIncomingEntity);
            var rolesToAdd = this.GetRolesForEntityType(entity.ID);

            var rolesAdded = false;

            // TODO: Optimize this for larger sets
            foreach (var role in rolesToAdd)
            {
                if (witOutgoingEntity.roles.Contains(role.name))
                {
                    continue;
                }

                Debug.Log($"Adding role {role}");
                witOutgoingEntity.roles.Add(role.name);
                rolesAdded = true;
            }

            if (!rolesAdded)
            {
                Debug.Log("No additional roles needed");
                completionCallback(true, "");
                yield break;
            }

            var entityData = JsonConvert.SerializeObject(witOutgoingEntity);
            Debug.Log($"About to put entity: {entityData}");
            yield return this.WitHttp.MakeUnityWebRequest($"/entities/{witOutgoingEntity.name}", WebRequestMethods.Http.Put, entityData, completionCallback);
        }

        private IEnumerator GetEntity(string entityName, Action<WitIncomingEntity> callBack)
        {
            var response = "";
            var result = false;
            yield return this.WitHttp.MakeUnityWebRequest($"/entities/{entityName}", WebRequestMethods.Http.Get,
                (success, data) => { response = data;
                    result = success;
                });

            if (!result)
            {
                callBack(null);
                yield break;
            }

            Debug.Log($"Got entity: {response}");
            var entity = JsonConvert.DeserializeObject<WitIncomingEntity>(response);
            callBack(entity);
        }

        private IEnumerator TrainEntity(ManifestEntity entity, Manifest manifest, StepResult callback)
        {
            var witIncomingEntity = new WitIncomingEntity()
            {
                name = entity.Name,
                keywords = entity.Values,
                roles = GetRolesForEntityType(entity.ID).ToList()
            };

            var witOutgoingEntity = new WitOutgoingEntity(witIncomingEntity);

            var entityData = JsonConvert.SerializeObject(witOutgoingEntity);

            Debug.Log($"Training entity: {entityData}");

            yield return this.WitHttp.MakeUnityWebRequest("/entities", WebRequestMethods.Http.Post, entityData, callback);
        }
    }
}
