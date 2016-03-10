// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Metadata
{
    public interface IDisplayMetadata
    {
        /// <summary>
        /// Gets a set of additional values. See <see cref="ModelMetadata.AdditionalValues"/>
        /// </summary>
        IReadOnlyDictionary<object, object> AdditionalValues { get; }

        /// <summary>
        /// Gets or sets a value indicating whether or not empty strings should be treated as <c>null</c>.
        /// See <see cref="ModelMetadata.ConvertEmptyStringToNull"/>
        /// </summary>
        bool ConvertEmptyStringToNull { get; }

        /// <summary>
        /// Gets or sets the name of the data type.
        /// See <see cref="ModelMetadata.DataTypeName"/>
        /// </summary>
        string DataTypeName { get; }

        /// <summary>
        /// Gets or sets a delegate which is used to get a value for the
        /// model description. See <see cref="ModelMetadata.Description"/>.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets or sets a display format string for the model.
        /// See <see cref="ModelMetadata.DisplayFormatString"/>
        /// </summary>
        string DisplayFormatString { get; }

        /// <summary>
        /// Gets or sets a delegate delegate which is used to get a value for the
        /// display name of the model. See <see cref="ModelMetadata.DisplayName"/>.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets or sets an edit format string for the model.
        /// See <see cref="ModelMetadata.EditFormatString"/>
        /// </summary>
        string EditFormatString { get; }

        /// <summary>
        /// Gets the ordered and grouped display names and values of all <see cref="System.Enum"/> values in
        /// <see cref="ModelMetadata.UnderlyingOrModelType"/>. See
        /// <see cref="ModelMetadata.EnumGroupedDisplayNamesAndValues"/>.
        /// </summary>
        IEnumerable<KeyValuePair<EnumGroupAndName, string>> EnumGroupedDisplayNamesAndValues { get; }

        /// <summary>
        /// Gets the names and values of all <see cref="System.Enum"/> values in
        /// <see cref="ModelMetadata.UnderlyingOrModelType"/>. See <see cref="ModelMetadata.EnumNamesAndValues"/>.
        /// </summary>
        // This could be implemented in DefaultModelMetadata. But value should be cached.
        IReadOnlyDictionary<string, string> EnumNamesAndValues { get; }

        /// <summary>
        /// Gets or sets a value indicating whether or not the model has a non-default edit format.
        /// See <see cref="ModelMetadata.HasNonDefaultEditFormat"/>
        /// </summary>
        bool HasNonDefaultEditFormat { get; }

        /// <summary>
        /// Gets or sets a value indicating if the surrounding HTML should be hidden.
        /// See <see cref="ModelMetadata.HideSurroundingHtml"/>
        /// </summary>
        bool HideSurroundingHtml { get; }

        /// <summary>
        /// Gets or sets a value indicating if the model value should be HTML encoded.
        /// See <see cref="ModelMetadata.HtmlEncode"/>
        /// </summary>
        bool HtmlEncode { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="ModelMetadata.UnderlyingOrModelType"/> is for an
        /// <see cref="System.Enum"/>. See <see cref="ModelMetadata.IsEnum"/>.
        /// </summary>
        // This could be implemented in DefaultModelMetadata. But value is needed in the details provider.
        bool IsEnum { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="ModelMetadata.UnderlyingOrModelType"/> is for an
        /// <see cref="System.Enum"/> with an associated <see cref="System.FlagsAttribute"/>. See
        /// <see cref="ModelMetadata.IsFlagsEnum"/>.
        /// </summary>
        // This could be implemented in DefaultModelMetadata. But value is needed in the details provider.
        bool IsFlagsEnum { get; }

        /// <summary>
        /// Gets or sets the text to display when the model value is null.
        /// See <see cref="ModelMetadata.NullDisplayText"/>
        /// </summary>
        string NullDisplayText { get; }

        /// <summary>
        /// Gets or sets the order.
        /// See <see cref="ModelMetadata.Order"/>
        /// </summary>
        int Order { get; }

        /// <summary>
        /// Gets or sets a delegate which is used to get a value for the
        /// model's placeholder text. See <see cref="ModelMetadata.Placeholder"/>.
        /// </summary>
        string Placeholder { get; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to include in the model value in display.
        /// See <see cref="ModelMetadata.ShowForDisplay"/>
        /// </summary>
        bool ShowForDisplay { get; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to include in the model value in an editor.
        /// See <see cref="ModelMetadata.ShowForEdit"/>
        /// </summary>
        bool ShowForEdit { get; }

        /// <summary>
        /// Gets or sets a the property name of a model property to use for display.
        /// See <see cref="ModelMetadata.SimpleDisplayProperty"/>
        /// </summary>
        string SimpleDisplayProperty { get; }

        /// <summary>
        /// Gets or sets a hint for location of a display or editor template.
        /// See <see cref="ModelMetadata.TemplateHint"/>
        /// </summary>
        string TemplateHint { get; }

        string GetDisplayName();
    }
}
