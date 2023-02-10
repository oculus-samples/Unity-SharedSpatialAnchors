/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.Conduit
{
    /// <summary>
    /// This class exists because Wit.ai returns the roles in a format different from what it expects.
    /// This is used for PUT requests with the bare essentials.
    /// </summary>
    public class WitOutgoingEntity : WitEntity
    {
        public WitOutgoingEntity(WitIncomingEntity incoming)
        {
            this.name = incoming.name;

            this.roles = new List<string>();
            foreach (var role in incoming.roles)
            {
                this.roles.Add(role.name);
            }
        }

        public List<string> roles { get; set; }
    }
}
