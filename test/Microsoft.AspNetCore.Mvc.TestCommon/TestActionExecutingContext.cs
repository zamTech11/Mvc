// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Microsoft.AspNetCore.Mvc
{
    internal class TestActionExecutingContext : ActionExecutingContext
    {
        public TestActionExecutingContext(
            ActionContext actionContext,
            IList<IFilterMetadata> filters,
            object controller,
            IDictionary<string, object> actionArguments)
        {
            ActionContext = actionContext;
            Filters = filters;
            Controller = controller;
            ActionArguments = actionArguments;
        }

        public override IDictionary<string, object> ActionArguments { get; }

        public override ActionContext ActionContext { get; }

        public override object Controller { get; }

        public override IList<IFilterMetadata> Filters { get; }

        public override IActionResult Result { get; set; }
    }
}
