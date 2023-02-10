/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Conduit
{
    public class WitTrainingUtterance
    {
        public string Text { get; set; }

        public string Intent { get; set; }

        public List<string> Traits { get; set; }

        public List<WitTrainingEntity> Entities { get; set; }
    }
}
