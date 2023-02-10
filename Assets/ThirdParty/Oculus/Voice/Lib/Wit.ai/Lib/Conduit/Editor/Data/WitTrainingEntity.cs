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
    public class WitTrainingEntity
    {
      public string Entity { get; set; }

      public string Role { get; set; }

      public string Body { get; set; }

      public int Start { get; set; }

      public int End { get; set; }

      public List<WitTrainingEntity> Entities { get; set; }
    }
}
