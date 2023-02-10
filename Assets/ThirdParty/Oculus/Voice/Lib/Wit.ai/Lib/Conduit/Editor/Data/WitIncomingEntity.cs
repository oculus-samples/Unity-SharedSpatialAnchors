/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.WitAi.Data.Info;

namespace Meta.Conduit
{
    /// <summary>
    /// This class exists because Wit.ai returns the roles in a format different from what it expects.
    /// This is used for GET requests.
    /// </summary>
    public class WitIncomingEntity : WitEntity
    {
        public List<WitRole> roles { get; set; } = new List<WitRole>();

        public List<WitKeyword> keywords { get; set; } = new List<WitKeyword>();
    }
}
