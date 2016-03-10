// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Metadata
{
    public interface ITypeMetadata
    {
        /// <summary>
        /// Gets the <see cref="Type"/> for elements of <see cref="ModelType"/> if that <see cref="Type"/>
        /// implements <see cref="IEnumerable"/>.
        /// </summary>
        Type ElementType { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="ModelType"/> is a simple type.
        /// </summary>
        /// <remarks>
        /// A simple type is defined as a <see cref="Type"/> which has a
        /// <see cref="System.ComponentModel.TypeConverter"/> that can convert from <see cref="string"/>.
        /// </remarks>
        bool IsComplexType { get; }

        /// <summary>
        /// Gets a value indicating whether or not <see cref="ModelType"/> is a <see cref="Nullable{T}"/>.
        /// </summary>
        bool IsNullableValueType { get; }

        /// <summary>
        /// Gets a value indicating whether or not <see cref="ModelType"/> is a collection type.
        /// </summary>
        /// <remarks>
        /// A collection type is defined as a <see cref="Type"/> which is assignable to <see cref="ICollection{T}"/>.
        /// </remarks>
        bool IsCollectionType { get; }

        /// <summary>
        /// Gets a value indicating whether or not <see cref="ModelType"/> is an enumerable type.
        /// </summary>
        /// <remarks>
        /// An enumerable type is defined as a <see cref="Type"/> which is assignable to
        /// <see cref="IEnumerable"/>, and is not a <see cref="string"/>.
        /// </remarks>
        bool IsEnumerableType { get; }

        /// <summary>
        /// Gets a value indicating whether or not <see cref="ModelType"/> allows <c>null</c> values.
        /// </summary>
        bool IsReferenceOrNullableType { get; }

        Type ModelType { get; }

        /// <summary>
        /// Gets the underlying type argument if <see cref="ModelType"/> inherits from <see cref="Nullable{T}"/>.
        /// Otherwise gets <see cref="ModelType"/>.
        /// </summary>
        /// <remarks>
        /// Identical to <see cref="ModelType"/> unless <see cref="IsNullableValueType"/> is <c>true</c>.
        /// </remarks>
        Type UnderlyingOrModelType { get; }
    }
}
