// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Microsoft.AspNetCore.Mvc.ModelBinding
{
    /// <summary>
    /// A context that contains operating information for model binding and validation.
    /// </summary>
    public class DefaultModelBindingContext : ModelBindingContext
    {
        private readonly Stack<State> _stack = new Stack<State>();

        private OperationBindingContext _operationBindingContext;
        private ModelStateDictionary _modelState;
        private ValidationStateDictionary _validationState;
        private State _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultModelBindingContext"/> class.
        /// </summary>
        public DefaultModelBindingContext()
        {
        }

        /// <summary>
        /// Creates a new <see cref="DefaultModelBindingContext"/> for top-level model binding operation.
        /// </summary>
        /// <param name="operationBindingContext">
        /// The <see cref="OperationBindingContext"/> associated with the binding operation.
        /// </param>
        /// <param name="bindingInfo"><see cref="BindingInfo"/> associated with the model.</param>
        /// <returns>A new instance of <see cref="DefaultModelBindingContext"/>.</returns>
        public static ModelBindingContext CreateBindingContext(
            OperationBindingContext operationBindingContext,
            BindingInfo bindingInfo)
        {
            if (operationBindingContext == null)
            {
                throw new ArgumentNullException(nameof(operationBindingContext));
            }

            return new DefaultModelBindingContext()
            {
                ModelState = operationBindingContext.ActionContext.ModelState,
                OperationBindingContext = operationBindingContext,
                ValueProvider = operationBindingContext.ValueProvider,

                ValidationState = new ValidationStateDictionary(),
            };
        }

        /// <inheritdoc />
        public override NestedScope EnterNestedScope(object model, string modelName)
        {
            var modelPath = ModelNames.CreatePropertyModelName(ModelPath, modelName);

            var scope = EnterNestedScope();

            Model = model;
            ModelPath = modelPath;

            return scope;
        }

        /// <inheritdoc />
        public override NestedScope EnterNestedScope()
        {
            _stack.Push(_state);

            Result = null;

            return new NestedScope(this);
        }

        /// <inheritdoc />
        protected override void ExitNestedScope()
        {
            _state = _stack.Pop();
        }

        /// <inheritdoc />
        public override OperationBindingContext OperationBindingContext
        {
            get { return _operationBindingContext; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _operationBindingContext = value;
            }
        }

        public override HttpContext HttpContext
        {
            get { return OperationBindingContext.HttpContext; }
        }

        /// <inheritdoc />
        public override object Model
        {
            get { return _state.Model; }
            set { _state.Model = value; }
        }

        public override string ModelPath
        {
            get { return _state.ModelPath; }
            set { _state.ModelPath = value; }
        }

        /// <inheritdoc />
        public override ModelStateDictionary ModelState
        {
            get { return _modelState; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _modelState = value;
            }
        }

        /// <inheritdoc />
        public override IValueProvider ValueProvider
        {
            get { return _state.ValueProvider; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                _state.ValueProvider = value;
            }
        }

        /// <inheritdoc />
        public override ValidationStateDictionary ValidationState
        {
            get { return _validationState; }
            set { _validationState = value; }
        }

        /// <inheritdoc />
        public override ModelBindingResult? Result
        {
            get
            {
                return _state.Result;
            }
            set
            {
                if (value.HasValue && value.Value == default(ModelBindingResult))
                {
                    throw new ArgumentException(nameof(ModelBindingResult));
                }

                _state.Result = value;
            }
        }

        private struct State
        {
            public string ModelPath;
            public object Model;
            public IValueProvider ValueProvider;
            public ModelBindingResult? Result;
        };
    }
}
