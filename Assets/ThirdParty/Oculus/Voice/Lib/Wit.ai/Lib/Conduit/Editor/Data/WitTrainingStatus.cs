/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Runtime.Serialization;

namespace Meta.Conduit
{
    public enum WitTrainingStatus
    {
        Unknown,
        [EnumMember(Value = "done")]
        Done,
        [EnumMember(Value = "scheduled")]
        Scheduled,
        [EnumMember(Value = "ongoing")]
        Ongoing
    }
}
