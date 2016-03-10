// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    public abstract class ModelBroMetadata : IEquatable<ModelBroMetadata>
    {
        private IReadOnlyList<ModelBroMetadata> _children;

        public IReadOnlyList<ModelBroMetadata> Children
        {
            get
            {
                if (_children == null)
                {
                    _children = CreateChildren();
                }

                return _children;
            }
        }

        public abstract string ModelName { get; }

        protected abstract IReadOnlyList<ModelBroMetadata> CreateChildren();

        public abstract T Get<T>() where T : class;

        public abstract bool Equals(ModelBroMetadata other);

        public override abstract int GetHashCode();

        public override bool Equals(object obj)
        {
            return Equals(obj as ModelBroMetadata);
        }
    }

    public class ActionDescriptorBroMetadata : ModelBroMetadata
    {
        private readonly IModelMetadataProvider _metadataProvider;
        private readonly ActionDescriptor _actionDescriptor;

        public ActionDescriptorBroMetadata(IModelMetadataProvider metadataProvider, ActionDescriptor actionDescriptor)
        {
            _metadataProvider = metadataProvider;
            _actionDescriptor = actionDescriptor;
        }

        public override string ModelName => "$";

        protected override IReadOnlyList<ModelBroMetadata> CreateChildren()
        {
            var items = new List<ModelBroMetadata>();

            foreach (var parameter in _actionDescriptor.Parameters)
            {
                items.Add(new ParameterDescriptorBroMetadata(_metadataProvider, parameter));
            }

            foreach (var property in _actionDescriptor.BoundProperties)
            {
                items.Add(new PropertyDescriptorBroMetadata(_metadataProvider, property));
            }

            return items;
        }

        public override T Get<T>()
        {
            return null;
        }

        public override bool Equals(ModelBroMetadata obj)
        {
            var other = obj as ActionDescriptorBroMetadata;
            return other?._actionDescriptor == _actionDescriptor;
        }

        public override int GetHashCode()
        {
            return _actionDescriptor.GetHashCode();
        }
    }

    public class ParameterDescriptorBroMetadata : ModelBroMetadata
    {
        private readonly IModelMetadataProvider _metadataProvider;
        private readonly ParameterDescriptor _parameterDescriptor;
        private readonly ModelMetadataBroMetadata _inner;

        public ParameterDescriptorBroMetadata(
            IModelMetadataProvider metadataProvider,
            ParameterDescriptor parameterDescriptor)
        {
            _metadataProvider = metadataProvider;
            _parameterDescriptor = parameterDescriptor;

            var modelMetadata = _metadataProvider.GetMetadataForType(_parameterDescriptor.ParameterType);
            _inner = new ModelMetadataBroMetadata(modelMetadata);
        }

        public ParameterDescriptor ParameterDescriptor { get; }

        public override string ModelName => _inner.ModelName;

        protected override IReadOnlyList<ModelBroMetadata> CreateChildren()
        {
            return _inner.Children;
        }

        public override T Get<T>()
        {
            return _inner.Get<T>();
        }

        public override bool Equals(ModelBroMetadata obj)
        {
            var other = obj as ParameterDescriptorBroMetadata;
            return other?._parameterDescriptor == _parameterDescriptor;
        }

        public override int GetHashCode()
        {
            return _parameterDescriptor.GetHashCode();
        }
    }

    public class PropertyDescriptorBroMetadata : ModelBroMetadata
    {
        private readonly IModelMetadataProvider _metadataProvider;
        private readonly ParameterDescriptor _parameterDescriptor;
        private readonly ModelMetadataBroMetadata _inner;

        public PropertyDescriptorBroMetadata(
            IModelMetadataProvider metadataProvider, 
            ParameterDescriptor parameterDescriptor)
        {
            _metadataProvider = metadataProvider;
            _parameterDescriptor = parameterDescriptor;

            var modelMetadata = _metadataProvider.GetMetadataForType(_parameterDescriptor.ParameterType);
            _inner = new ModelMetadataBroMetadata(modelMetadata);
        }
        
        public ParameterDescriptor ParameterDescriptor { get; }

        public override string ModelName => _inner.ModelName;

        protected override IReadOnlyList<ModelBroMetadata> CreateChildren()
        {
            return _inner.Children;
        }

        public override T Get<T>()
        {
            return _inner.Get<T>();
        }

        public override bool Equals(ModelBroMetadata obj)
        {
            var other = obj as PropertyDescriptorBroMetadata;
            return other?._parameterDescriptor == _parameterDescriptor;
        }

        public override int GetHashCode()
        {
            return _parameterDescriptor.GetHashCode();
        }
    }

    public class ModelMetadataBroMetadata : ModelBroMetadata
    {
        private readonly ModelMetadata _inner;

        public ModelMetadataBroMetadata(ModelMetadata inner)
        {
            _inner = inner;
        }

        public override string ModelName => _inner.BinderModelName ?? _inner.PropertyName;

        protected override IReadOnlyList<ModelBroMetadata> CreateChildren()
        {
            var children = new List<ModelBroMetadata>();
            foreach (var property in _inner.Properties)
            {
                children.Add(new ModelMetadataBroMetadata(property));
            }

            return children;
        }

        public override T Get<T>()
        {
            if (typeof(IModelBindingMessageProvider) == typeof(T))
            {
                return (T)_inner.ModelBindingMessageProvider;
            }

            return _inner as T;
        }

        public override bool Equals(ModelBroMetadata obj)
        {
            var other = obj as ModelMetadataBroMetadata;
            return other?._inner == _inner;
        }

        public override int GetHashCode()
        {
            return _inner.GetHashCode();
        }
    }
}
