// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public static class TaskCache
    {
#if !NETSTANDARD1_3
        static readonly Task _completedTask = Task.FromResult(0);
#endif

        /// <summary>Gets a task that's already been completed successfully.</summary>
        /// <remarks>May not always return the same instance.</remarks>        
        public static Task CompletedTask
        {
            get
            {
#if NETSTANDARD1_3
                return Task.CompletedTask;
#else
                return _completedTask;
#endif
            }
        }
    }

}
