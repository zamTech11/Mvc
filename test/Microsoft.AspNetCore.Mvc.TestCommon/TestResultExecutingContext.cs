// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Microsoft.AspNetCore.Mvc
{
    internal class TestResultExecutingContext : ResultExecutingContext
    {
        public TestResultExecutingContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            object controller,
            IActionResult result)
        {
            ActionContext = actionContext;
            Filters = filters;
            Controller = controller;
            Result = result;
        }

        public override ActionContext ActionContext { get; }

        public override bool Cancel { get; set; }

        public override object Controller { get; }

        public override IList<IFilterMetadata> Filters { get; }

        public override IActionResult Result { get; set; }
    }
}
