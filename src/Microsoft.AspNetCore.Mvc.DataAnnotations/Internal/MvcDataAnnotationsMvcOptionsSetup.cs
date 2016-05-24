// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.DataAnnotations.Internal
{
    /// <summary>
    /// Sets up default options for <see cref="MvcOptions"/>.
    /// </summary>
    public class MvcDataAnnotationsMvcOptionsSetup : ConfigureOptions<MvcOptions>
    {
        public MvcDataAnnotationsMvcOptionsSetup(DataAnnotationsModelValidatorProvider dataAnnotationsModelValidatorProvider)
            : base(options => ConfigureMvc(options, dataAnnotationsModelValidatorProvider))
        {
        }

        public static void ConfigureMvc(
            MvcOptions options, 
            DataAnnotationsModelValidatorProvider dataAnnotationsModelValidatorProvider)
        {
            options.ModelMetadataDetailsProviders.Add(new DataAnnotationsMetadataProvider());
            options.ModelValidatorProviders.Add(dataAnnotationsModelValidatorProvider);
        }
    }
}