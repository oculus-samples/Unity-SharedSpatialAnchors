/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Meta.WitAi;
using UnityEngine;

namespace Meta.Conduit
{
    /// <summary>
    /// The dispatcher is responsible for deciding which method to invoke when a request is received as well as parsing
    /// the parameters and passing them to the handling method.
    /// </summary>
    internal class ConduitDispatcher : IConduitDispatcher
    {
        /// <summary>
        /// The Conduit manifest which captures the structure of the voice-enabled methods.
        /// </summary>
        private Manifest _manifest;

        /// <summary>
        /// The manifest loader.
        /// </summary>
        private readonly IManifestLoader _manifestLoader;

        /// <summary>
        /// Resolves instances (objects) based on their type.
        /// </summary>
        private readonly IInstanceResolver _instanceResolver;

        /// <summary>
        /// Resolves the actual parameters for method invocations.
        /// </summary>
        private readonly IParameterProvider _parameterProvider;

        /// <summary>
        /// Maps internal parameter names to fully qualified parameter names (roles/slots).
        /// </summary>
        private readonly Dictionary<string, string> _parameterToRoleMap = new Dictionary<string, string>();

        public ConduitDispatcher(IManifestLoader manifestLoader, IInstanceResolver instanceResolver, IParameterProvider parameterProvider)
        {
            _manifestLoader = manifestLoader;
            _instanceResolver = instanceResolver;
            _parameterProvider = parameterProvider;
        }

        /// <summary>
        /// Parses the manifest provided and registers its callbacks for dispatching.
        /// </summary>
        /// <param name="manifestFilePath">The path to the manifest file.</param>
        public void Initialize(string manifestFilePath)
        {
            if (_manifest != null)
            {
                return;
            }

            _manifest = _manifestLoader.LoadManifest(manifestFilePath);
            if (_manifest == null)
            {
                return;
            }

            // Map fully qualified role names to internal parameters.
            foreach (var action in _manifest.Actions)
            {
                foreach (var parameter in action.Parameters)
                {
                    if (!_parameterToRoleMap.ContainsKey(parameter.InternalName))
                    {
                        _parameterToRoleMap.Add(parameter.InternalName, parameter.QualifiedName);
                    }
                }
            }
        }

        /// <summary>
        /// Finds invocation contexts that are applicable to the given action and supplied parameter set.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        /// <param name="confidence">The confidence level between 0 and 1.</param>
        /// <returns></returns>
        private List<InvocationContext> ResolveInvocationContexts(string actionId, float confidence, bool partial)
        {
            var invocationContexts = _manifest.GetInvocationContexts(actionId);

            // We may have multiple overloads, find the correct match.
            return invocationContexts.Where(context => CompatibleInvocationContext(context, confidence, partial)).ToList();
        }

        /// <summary>
        /// Returns true if the invocation context is compatible with the actual parameters the parameter provider
        /// is supplying. False otherwise.
        /// </summary>
        /// <param name="invocationContext">The invocation context.</param>
        /// <param name="confidence">The intent confidence level.</param>
        /// <returns>True if the invocation can be made with the actual parameters. False otherwise.</returns>
        private bool CompatibleInvocationContext(InvocationContext invocationContext, float confidence, bool partial)
        {
            var parameters = invocationContext.MethodInfo.GetParameters();
            if (invocationContext.ValidatePartial != partial)
            {
                return false;
            }
            if (invocationContext.MinConfidence > confidence || confidence > invocationContext.MaxConfidence)
            {
                return false;
            }

            var log = new StringBuilder();
            if (!parameters.All(parameter => _parameterProvider.ContainsParameter(parameter, log)))
            {
                VLog.W($"Failed to dispatch method\nType: {invocationContext.Type.FullName}\nMethod: {invocationContext.MethodInfo.Name}\n{log}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Invokes the method matching the specified action ID.
        /// This should NOT be called before the dispatcher is initialized.
        /// </summary>
        /// <param name="actionId">The action ID (which is also the intent name).</param>
        /// <param name="parameters">Dictionary of parameters mapping parameter name to value.</param>
        /// <param name="confidence">The confidence level (between 0-1) of the intent that's invoking the action.</param>
        /// <param name="partial">Whether partial responses should be accepted or not</param>
        /// <returns>True if all invocations succeeded. False if at least one failed or no callbacks were found.</returns>
        public bool InvokeAction(string actionId, Dictionary<string, object> parameters, float confidence = 1f, bool partial = false)
        {
            if (!_manifest.ContainsAction(actionId))
            {
                VLog.D($"Conduit did not find {actionId} in manifest");
                return false;
            }

            _parameterProvider.Populate(parameters, _parameterToRoleMap);

            var invocationContexts = ResolveInvocationContexts(actionId, confidence, partial);
            if (invocationContexts.Count < 1)
            {
                VLog.D($"Failed to resolve method for {actionId} with supplied context");
                return false;
            }

            var allSucceeded = true;
            foreach (var invocationContext in invocationContexts)
            {
                try
                {
                    if (!this.InvokeMethod(invocationContext))
                    {
                        allSucceeded = false;
                    }
                }
                catch (Exception e)
                {
                    VLog.W($"Failed to invoke {invocationContext.MethodInfo.Name}. {e}");
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }

        /// <summary>
        /// Invokes a method on all callbacks of a specific invocation context. If the method is static, then only a
        /// single call is made. If it's an instance method, then it is invoked on all instances.
        /// </summary>
        /// <param name="invocationContext">The invocation context.</param>
        /// <returns>True if the method was invoked successfully on all valid targets.</returns>
        private bool InvokeMethod(InvocationContext invocationContext)
        {
            var method = invocationContext.MethodInfo;
            var formalParametersInfo = method.GetParameters();
            var parameterObjects = new object[formalParametersInfo.Length];
            for (var i = 0; i < formalParametersInfo.Length; i++)
            {
                var log = new StringBuilder();
                if (!_parameterProvider.ContainsParameter(formalParametersInfo[i], log))
                {
                    VLog.W($"Failed to find method param while invoking\nType: {invocationContext.Type.FullName}\nMethod: {invocationContext.MethodInfo.Name}\nParameter Issues\n{log}");
                    return false;
                }
                parameterObjects[i] = _parameterProvider.GetParameterValue(formalParametersInfo[i]);
            }

            if (method.IsStatic)
            {
                try
                {
                    method.Invoke(null, parameterObjects.ToArray());
                }
                catch (Exception e)
                {
                    VLog.W($"Failed to invoke static method {method.Name}. {e}");
                    return false;
                }

                return true;
            }
            else
            {
                var allSucceeded = true;
                foreach (var obj in this._instanceResolver.GetObjectsOfType(invocationContext.Type))
                {
                    try
                    {
                        method.Invoke(obj, parameterObjects.ToArray());
                    }
                    catch (Exception e)
                    {
                        VLog.W($"Failed to invoke method {method.Name}. {e} on {obj}");
                        allSucceeded = false;
                        continue;
                    }
                }

                return allSucceeded;
            }
        }
    }
}
