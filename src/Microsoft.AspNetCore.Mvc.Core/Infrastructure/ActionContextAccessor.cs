// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET451
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
#else
using System.Threading;
#endif

namespace Microsoft.AspNetCore.Mvc.Infrastructure
{
    public class ActionContextAccessor : IActionContextAccessor
    {
        public ActionContext ActionContext { get; set; }
    }
}