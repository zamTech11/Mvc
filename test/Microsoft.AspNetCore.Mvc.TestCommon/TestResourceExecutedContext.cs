// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Microsoft.AspNetCore.Mvc
{
    internal class TestResourceExecutedContext : ResourceExecutedContext
    {
        private Exception _exception;
        private ExceptionDispatchInfo _exceptionDispatchInfo;

        public TestResourceExecutedContext(ActionContext actionContext, IList<IFilterMetadata> filters)
        {
            ActionContext = actionContext;
            Filters = filters;
        }

        public TestResourceExecutedContext(ResourceExecutingContext context)
            : this(context.ActionContext, context.Filters)
        {
        }

        public override ActionContext ActionContext { get; }

        public override bool Canceled { get; set; }

        public override Exception Exception
        {
            get
            {
                if (_exception == null && _exceptionDispatchInfo != null)
                {
                    return _exceptionDispatchInfo.SourceException;
                }
                else
                {
                    return _exception;
                }
            }

            set
            {
                _exceptionDispatchInfo = null;
                _exception = value;
            }
        }

        public override ExceptionDispatchInfo ExceptionDispatchInfo
        {
            get
            {
                return _exceptionDispatchInfo;
            }

            set
            {
                _exception = null;
                _exceptionDispatchInfo = value;
            }
        }

        public override bool ExceptionHandled { get; set; }

        public override IList<IFilterMetadata> Filters { get; }

        public override IActionResult Result { get; set; }
    }
}
