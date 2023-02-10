/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Conduit
{
    /// <summary>
    /// Used to transfer data between steps.
    /// </summary>
    internal class Payload
    {
        public string Data { get; set; } = "";
        public string Error { get; set; } = "";
    }
}
