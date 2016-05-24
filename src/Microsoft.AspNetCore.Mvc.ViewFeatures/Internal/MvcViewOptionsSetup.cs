// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc.DataAnnotations.Internal;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.ViewFeatures.Internal
{
    /// <summary>
    /// Sets up default options for <see cref="MvcViewOptions"/>.
    /// </summary>
    public class MvcViewOptionsSetup : ConfigureOptions<MvcViewOptions>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="MvcViewOptionsSetup"/>.
        /// </summary>
        public MvcViewOptionsSetup(
            DataAnnotationsClientModelValidatorProvider dataAnnotationsClientModelValidatorProvider)
            : base(options => ConfigureMvc(options, dataAnnotationsClientModelValidatorProvider))
        {
        }

        public static void ConfigureMvc(
            MvcViewOptions options,
            DataAnnotationsClientModelValidatorProvider dataAnnotationsClientModelValidatorProvider)
        {
            // Set up client validators
            options.ClientModelValidatorProviders.Add(new DefaultClientModelValidatorProvider());
            options.ClientModelValidatorProviders.Add(dataAnnotationsClientModelValidatorProvider);
            options.ClientModelValidatorProviders.Add(new NumericClientModelValidatorProvider());
        }
    }
}
