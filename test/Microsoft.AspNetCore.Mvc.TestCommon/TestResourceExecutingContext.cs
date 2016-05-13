// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Microsoft.AspNetCore.Mvc
{
    internal class TestResourceExecutingContext : ResourceExecutingContext
    {
        public TestResourceExecutingContext(ActionContext actionContext, IList<IFilterMetadata> filters)
        {
            ActionContext = actionContext;
            Filters = filters;
        }

        public override ActionContext ActionContext { get; }

        public override IList<IFilterMetadata> Filters { get; }

        public override IActionResult Result { get; set; }
    }
}
