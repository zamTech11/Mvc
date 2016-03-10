// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Microsoft.AspNetCore.Mvc.Formatters
{
    /// <summary>
    /// Result of a <see cref="IInputFormatter.ReadAsync"/> operation.
    /// </summary>
    public class InputFormatterResult
    {
        public InputFormatterResult()
        {
        }

        public IDictionary<string, ModelError> Errors { get; } = new Dictionary<string, ModelError>();

        /// <summary>
        /// Gets an indication whether the <see cref="IInputFormatter.ReadAsync"/> operation had an error.
        /// </summary>
        public bool HasError => Errors.Count > 0;

        /// <summary>
        /// Gets the deserialized <see cref="object"/>.
        /// </summary>
        /// <value>
        /// <c>null</c> if <see cref="HasError"/> is <c>true</c>.
        /// </value>
        public object Model { get; set; }
    }
}
