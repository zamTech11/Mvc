// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.DataAnnotations.Internal
{
    /// <summary>
    /// Sets up default options for <see cref="MvcOptions"/>.
    /// </summary>
    public class MvcDataAnnotationsMvcOptionsSetup : IConfigureOptions<MvcOptions>
    {
        private IServiceProvider _serviceProvider;

        public MvcDataAnnotationsMvcOptionsSetup(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            _serviceProvider = serviceProvider;
        }

        public void Configure(MvcOptions options)
        {
            var dataAnnotationLocalizationOptions =
                _serviceProvider.GetRequiredService<IOptions<MvcDataAnnotationsLocalizationOptions>>();

            // This service will be registered only if AddDataAnnotationsLocalization() is added to service collection.
            var stringLocalizerFactory = _serviceProvider.GetService<IStringLocalizerFactory>();
            var validationAttributeAdapterProvider = _serviceProvider.GetRequiredService<IValidationAttributeAdapterProvider>();

            options.ModelMetadataDetailsProviders.Add(new DataAnnotationsMetadataProvider(stringLocalizerFactory));

            options.ModelValidatorProviders.Add(new DataAnnotationsModelValidatorProvider(
                validationAttributeAdapterProvider,
                dataAnnotationLocalizationOptions,
                stringLocalizerFactory));
        }
    }
}