// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if NETSTANDARD1_5
using System.Reflection;
#endif
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class ControllerActionInvoker : IActionInvoker
    {
        private readonly IControllerFactory _controllerFactory;
        private readonly IControllerArgumentBinder _controllerArgumentBinder;
        private readonly DiagnosticSource _diagnosticSource;
        private readonly ILogger _logger;

        private readonly ControllerContext _controllerContext;
        private readonly IFilterMetadata[] _filters;
        private readonly ObjectMethodExecutor _executor;

        // Do not make this readonly, it's mutable. We don't want to make a copy.
        // https://blogs.msdn.microsoft.com/ericlippert/2008/05/14/mutating-readonly-structs/
        private FilterCursor _cursor;
        private Dictionary<string, object> _arguments;
        private object _controller;
        private IActionResult _result;
        private Exception _exception;
        private ExceptionDispatchInfo _exceptionDispatchInfo;
        private bool _exceptionHandled;
        private IActionResult _exceptionResult;

        private AuthorizationFilterContext _authorizationContext;

        private ResourceExecutingContext _resourceExecutingContext;
        private ResourceExecutedContext _resourceExecutedContext;

        private ExceptionContext _exceptionContext;

        private ActionExecutingContext _actionExecutingContext;
        private ActionExecutedContext _actionExecutedContext;

        private ResultExecutingContext _resultExecutingContext;
        private ResultExecutedContext _resultExecutedContext;

        public ControllerActionInvoker(
            ControllerActionInvokerCache cache,
            IControllerFactory controllerFactory,
            IControllerArgumentBinder controllerArgumentBinder,
            ILogger logger,
            DiagnosticSource diagnosticSource,
            ActionContext actionContext,
            IReadOnlyList<IValueProviderFactory> valueProviderFactories,
            int maxModelValidationErrors)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (controllerFactory == null)
            {
                throw new ArgumentNullException(nameof(controllerFactory));
            }

            if (controllerArgumentBinder == null)
            {
                throw new ArgumentNullException(nameof(controllerArgumentBinder));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (diagnosticSource == null)
            {
                throw new ArgumentNullException(nameof(diagnosticSource));
            }

            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            if (valueProviderFactories == null)
            {
                throw new ArgumentNullException(nameof(valueProviderFactories));
            }

            _controllerFactory = controllerFactory;
            _controllerArgumentBinder = controllerArgumentBinder;
            _logger = logger;
            _diagnosticSource = diagnosticSource;

            _controllerContext = new ControllerContext(actionContext);
            _controllerContext.ModelState.MaxAllowedErrors = maxModelValidationErrors;

            // PERF: These are rarely going to be changed, so let's go copy-on-write.
            _controllerContext.ValueProviderFactories = new CopyOnWriteList<IValueProviderFactory>(valueProviderFactories);

            var cacheEntry = cache.GetState(_controllerContext);
            _filters = cacheEntry.Filters;
            _executor = cacheEntry.ActionMethodExecutor;
            _cursor = new FilterCursor(_filters);
        }

        public virtual async Task InvokeAsync()
        {
            await InvokeAllAuthorizationFiltersAsync();

            // If Authorization Filters return a result, it's a short circuit because
            // authorization failed. We don't execute Result Filters around the result.
            if (_result != null)
            {
                await InvokeResultAsync();
                return;
            }

            try
            {
                await InvokeAllResourceFiltersAsync();
            }
            finally
            {
                // Release the instance after all filters have run. We don't need to surround
                // Authorizations filters because the instance will be created much later than
                // that.
                if (_controller != null)
                {
                    _controllerFactory.ReleaseController(_controllerContext, _controller);
                }
            }

            // We've reached the end of resource filters. If there's an unhandled exception on the context then
            // it should be thrown and middleware has a chance to handle it.
            if (_exceptionHandled)
            {
                _exception = null;
                _exceptionDispatchInfo = null;
                _exceptionHandled = false;
            }

            if (_exceptionDispatchInfo != null)
            {
                _exceptionDispatchInfo.Throw();
            }

            if (_exception != null)
            {
                throw _exception;
            }
        }

        private Task InvokeAllAuthorizationFiltersAsync()
        {
            _cursor.Reset();

            return InvokeAuthorizationFilterAsync();
        }

        private async Task InvokeAuthorizationFilterAsync()
        {
            // We should never get here if we already have a result.
            Debug.Assert(_result == null);

            var current = _cursor.GetNextFilter<IAuthorizationFilter, IAsyncAuthorizationFilter>();
            if (current.FilterAsync != null)
            {
                _authorizationContext = _authorizationContext ?? new PrivateAuthorizationFilterContext(this);

                _diagnosticSource.BeforeOnAuthorizationAsync(_authorizationContext, current.FilterAsync);

                await current.FilterAsync.OnAuthorizationAsync(_authorizationContext);

                _diagnosticSource.AfterOnAuthorizationAsync(_authorizationContext, current.FilterAsync);

                if (_result == null)
                {
                    // Only keep going if we don't have a result
                    await InvokeAuthorizationFilterAsync();
                }
                else
                {
                    _logger.AuthorizationFailure(current.FilterAsync);
                }
            }
            else if (current.Filter != null)
            {
                _authorizationContext = _authorizationContext ?? new PrivateAuthorizationFilterContext(this);

                _diagnosticSource.BeforeOnAuthorization(_authorizationContext, current.Filter);

                current.Filter.OnAuthorization(_authorizationContext);

                _diagnosticSource.AfterOnAuthorization(_authorizationContext, current.Filter);

                if (_result == null)
                {
                    // Only keep going if we don't have a result
                    await InvokeAuthorizationFilterAsync();
                }
                else
                {
                    _logger.AuthorizationFailure(current.Filter);
                }
            }
            else
            {
                // We've run out of Authorization Filters - if we haven't short circuited by now then this
                // request is authorized.
            }
        }

        private Task InvokeAllResourceFiltersAsync()
        {
            _cursor.Reset();

            return InvokeResourceFilterAsync();
        }

        private async Task<ResourceExecutedContext> InvokeResourceFilterAwaitedAsync()
        {
            Debug.Assert(_resourceExecutingContext != null);

            if (_result != null)
            {
                // If we get here, it means that an async filter set a result AND called next(). This is forbidden.
                var message = Resources.FormatAsyncResourceFilter_InvalidShortCircuit(
                    typeof(IAsyncResourceFilter).Name,
                    nameof(ResourceExecutingContext.Result),
                    typeof(ResourceExecutingContext).Name,
                    typeof(ResourceExecutionDelegate).Name);

                throw new InvalidOperationException(message);
            }

            await InvokeResourceFilterAsync();
            
            return _resourceExecutedContext = _resourceExecutedContext ?? new PrivateResourceExecutedContext(this);
        }

        private async Task InvokeResourceFilterAsync()
        {
            var item = _cursor.GetNextFilter<IResourceFilter, IAsyncResourceFilter>();
            try
            {
                if (item.FilterAsync != null)
                {
                    _resourceExecutingContext = _resourceExecutingContext ?? new PrivateResourceExecutingContext(this);

                    _diagnosticSource.BeforeOnResourceExecution(_resourceExecutingContext, item.FilterAsync);

                    await item.FilterAsync.OnResourceExecutionAsync(_resourceExecutingContext, InvokeResourceFilterAwaitedAsync);

                    if (_resourceExecutedContext == null)
                    {
                        // If we get here then the filter didn't call 'next' indicating a short circuit
                        _resourceExecutedContext = new PrivateResourceExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }

                    _diagnosticSource.AfterOnResourceExecution(_resourceExecutedContext, item.FilterAsync);

                    if (_result != null)
                    {
                        _logger.ResourceFilterShortCircuited(item.FilterAsync);

                        await InvokeResultAsync();
                    }
                }
                else if (item.Filter != null)
                {
                    _resourceExecutingContext = _resourceExecutingContext ?? new PrivateResourceExecutingContext(this);

                    _diagnosticSource.BeforeOnResourceExecuting(_resourceExecutingContext, item.Filter);

                    item.Filter.OnResourceExecuting(_resourceExecutingContext);

                    _diagnosticSource.AfterOnResourceExecuting(_resourceExecutingContext, item.Filter);

                    if (_result != null)
                    {
                        // Short-circuited by setting a result.
                        _logger.ResourceFilterShortCircuited(item.Filter);

                        await InvokeResultAsync();

                        _resourceExecutedContext = new PrivateResourceExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                    else
                    {
                        await InvokeResourceFilterAsync();

                        _resourceExecutedContext = _resourceExecutedContext ?? new PrivateResourceExecutedContext(this);

                        _diagnosticSource.BeforeOnResourceExecuted(_resourceExecutedContext, item.Filter);

                        item.Filter.OnResourceExecuted(_resourceExecutedContext);

                        _diagnosticSource.AfterOnResourceExecuted(_resourceExecutedContext, item.Filter);
                    }
                }
                else
                {
                    // >> ExceptionFilters >> Model Binding >> ActionFilters >> Action
                    await InvokeAllExceptionFiltersAsync();

                    // If Exception Filters provide a result, it's a short-circuit due to an exception.
                    // We don't execute Result Filters around the result.
                    if (_exceptionResult != null)
                    {
                        // This means that exception filters returned a result to 'handle' an error.
                        // We're not interested in seeing the exception details since it was handled.
                        _exception = null;
                        _exceptionDispatchInfo = null;

                        _result = _exceptionResult;
                        await InvokeResultAsync();
                    }
                    else if (_exception != null)
                    {
                        // If we get here, this means that we have an unhandled exception.
                        // Exception filted didn't handle this, so send it on to resource filters.
                    }
                    else
                    {
                        // We have a successful 'result' from the action or an Action Filter, so run
                        // Result Filters.
                        //
                        // >> ResultFilters >> (Result)
                        await InvokeAllResultFiltersAsync();
                    }
                }
            }
            catch (Exception exception)
            {
                _exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                _exception = exception;
            }
        }

        private Task InvokeAllExceptionFiltersAsync()
        {
            _cursor.Reset();

            return InvokeExceptionFilterAsync();
        }

        private async Task InvokeExceptionFilterAsync()
        {
            var current = _cursor.GetNextFilter<IExceptionFilter, IAsyncExceptionFilter>();
            if (current.FilterAsync != null)
            {
                // Exception filters run "on the way out" - so the filter is run after the rest of the
                // pipeline.
                await InvokeExceptionFilterAsync();
                
                if (_exception != null)
                {
                    _exceptionContext = _exceptionContext ?? new PrivateExceptionContext(this);

                    _diagnosticSource.BeforeOnExceptionAsync(_exceptionContext, current.FilterAsync);

                    // Exception filters only run when there's an exception - unsetting it will short-circuit
                    // other exception filters.
                    await current.FilterAsync.OnExceptionAsync(_exceptionContext);

                    _diagnosticSource.AfterOnExceptionAsync(_exceptionContext, current.FilterAsync);

                    if (_exception == null)
                    {
                        _logger.ExceptionFilterShortCircuited(current.FilterAsync);
                    }
                }
            }
            else if (current.Filter != null)
            {
                // Exception filters run "on the way out" - so the filter is run after the rest of the
                // pipeline.
                await InvokeExceptionFilterAsync();
                
                if (_exception != null)
                {
                    _exceptionContext = _exceptionContext ?? new PrivateExceptionContext(this);

                    _diagnosticSource.BeforeOnException(_exceptionContext, current.Filter);

                    // Exception filters only run when there's an exception - unsetting it will short-circuit
                    // other exception filters.
                    current.Filter.OnException(_exceptionContext);

                    _diagnosticSource.AfterOnException(_exceptionContext, current.Filter);

                    if (_exception == null)
                    {
                        _logger.ExceptionFilterShortCircuited(current.Filter);
                    }
                }
            }
            else
            {
                // We've reached the 'end' of the exception filter pipeline - this means that one stack frame has
                // been built for each exception. When we return from here, these frames will either:
                //
                // 1) Call the filter (if we have an exception)
                // 2) No-op (if we don't have an exception)
                try
                {
                    await InvokeAllActionFiltersAsync();

                    if (_exceptionHandled)
                    {
                        _exception = null;
                        _exceptionDispatchInfo = null;
                        _exceptionHandled = false;
                    }

                    // Action filters might 'return' an unhandled exception instead of throwing, if that happens then
                    // it will flow through the _exception field.
                }
                catch (Exception exception)
                {
                    _exceptionContext = new PrivateExceptionContext(this);
                    _exceptionContext.ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                }
            }
        }

        private async Task InvokeAllActionFiltersAsync()
        {
            _cursor.Reset();

            _controller = _controllerFactory.CreateController(_controllerContext);

            _arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            await _controllerArgumentBinder.BindArgumentsAsync(_controllerContext, _controller, _arguments);

            await InvokeActionFilterAsync();
        }

        private async Task<ActionExecutedContext> InvokeActionFilterAwaitedAsync()
        {
            if (_result != null)
            {
                // If we get here, it means that an async filter set a result AND called next(). This is forbidden.
                var message = Resources.FormatAsyncActionFilter_InvalidShortCircuit(
                    typeof(IAsyncActionFilter).Name,
                    nameof(ActionExecutingContext.Result),
                    typeof(ActionExecutingContext).Name,
                    typeof(ActionExecutionDelegate).Name);

                throw new InvalidOperationException(message);
            }

            await InvokeActionFilterAsync();
            
            return _actionExecutedContext = _actionExecutedContext ?? new PrivateActionExecutedContext(this);
        }

        private async Task InvokeActionFilterAsync()
        {
            var item = _cursor.GetNextFilter<IActionFilter, IAsyncActionFilter>();
            try
            {
                if (item.FilterAsync != null)
                {
                    _actionExecutingContext = _actionExecutingContext ?? new PrivateActionExecutingContext(this);

                    _diagnosticSource.BeforeOnActionExecution(_actionExecutingContext, item.FilterAsync);

                    await item.FilterAsync.OnActionExecutionAsync(_actionExecutingContext, InvokeActionFilterAwaitedAsync);

                    if (_actionExecutedContext == null)
                    {
                        // If we get here then the filter didn't call 'next' indicating a short circuit
                        _logger.ActionFilterShortCircuited(item.FilterAsync);

                        _actionExecutedContext = new PrivateActionExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }

                    _diagnosticSource.AfterOnActionExecution(_actionExecutedContext, item.FilterAsync);
                }
                else if (item.Filter != null)
                {
                    _actionExecutingContext = _actionExecutingContext ?? new PrivateActionExecutingContext(this);

                    _diagnosticSource.BeforeOnActionExecuting(_actionExecutingContext, item.Filter);

                    item.Filter.OnActionExecuting(_actionExecutingContext);

                    _diagnosticSource.AfterOnActionExecuting(_actionExecutingContext, item.Filter);

                    if (_result != null)
                    {
                        // Short-circuited by setting a result.
                        _logger.ActionFilterShortCircuited(item.Filter);

                        _actionExecutedContext = new PrivateActionExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                    else
                    {
                        await InvokeActionFilterAsync();

                        _actionExecutedContext = _actionExecutedContext ?? new PrivateActionExecutedContext(this);

                        _diagnosticSource.BeforeOnActionExecuted(_actionExecutedContext, item.Filter);

                        item.Filter.OnActionExecuted(_actionExecutedContext);

                        _diagnosticSource.BeforeOnActionExecuted(_actionExecutedContext, item.Filter);
                    }
                }
                else
                {
                    // All action filters have run, execute the action method.
                    try
                    {
                        _diagnosticSource.BeforeActionMethod(_controllerContext, _arguments, _controller);

                        var method = _controllerContext.ActionDescriptor.MethodInfo;

                        var arguments = ControllerActionExecutor.PrepareArguments(
                            _actionExecutingContext.ActionArguments,
                            _executor);

                        _logger.ActionMethodExecuting(_actionExecutingContext, arguments);

                        var actionReturnValue = await ControllerActionExecutor.ExecuteAsync(
                            _executor,
                            _controller,
                            arguments);

                        _result = CreateActionResult(method.ReturnType, actionReturnValue);

                        _logger.ActionMethodExecuted(_actionExecutingContext, _result);
                    }
                    finally
                    {
                        _diagnosticSource.AfterActionMethod(
                            _controllerContext,
                            _arguments,
                            _controller,
                            _result);
                    }
                }
            }
            catch (Exception exception)
            {
                // Exceptions thrown by the action method OR filters bubble back up through ActionExcecutedContext.
                _exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                _exception = exception;
            }
        }

        private async Task InvokeAllResultFiltersAsync()
        {
            _cursor.Reset();

            await InvokeResultFilterAsync();
        }

        private async Task<ResultExecutedContext> InvokeResultFilterAwaitedAsync()
        {
            Debug.Assert(_resultExecutingContext != null);

            if (_resultExecutingContext.Cancel == true)
            {
                // If we get here, it means that an async filter set cancel == true AND called next().
                // This is forbidden.
                var message = Resources.FormatAsyncResultFilter_InvalidShortCircuit(
                    typeof(IAsyncResultFilter).Name,
                    nameof(ResultExecutingContext.Cancel),
                    typeof(ResultExecutingContext).Name,
                    typeof(ResultExecutionDelegate).Name);

                throw new InvalidOperationException(message);
            }

            await InvokeResultFilterAsync();
            
            return _resultExecutedContext = _resultExecutedContext ?? new PrivateResultExecutedContext(this);
        }

        private async Task InvokeResultFilterAsync()
        {
            try
            {
                var item = _cursor.GetNextFilter<IResultFilter, IAsyncResultFilter>();
                if (item.FilterAsync != null)
                {
                    _resultExecutingContext = _resultExecutingContext ?? new PrivateResultExecutingContext(this);

                    _diagnosticSource.BeforeOnResultExecution(_resultExecutingContext, item.FilterAsync);

                    await item.FilterAsync.OnResultExecutionAsync(_resultExecutingContext, InvokeResultFilterAwaitedAsync);

                    if (_resultExecutedContext == null || _resultExecutingContext.Cancel == true)
                    {
                        // Short-circuited by not calling next || Short-circuited by setting Cancel == true
                        _logger.ResourceFilterShortCircuited(item.FilterAsync);

                        _resultExecutedContext = new PrivateResultExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }

                    _diagnosticSource.AfterOnResultExecution(_resultExecutedContext, item.FilterAsync);
                }
                else if (item.Filter != null)
                {
                    _resultExecutingContext = _resultExecutingContext ?? new PrivateResultExecutingContext(this);

                    _diagnosticSource.BeforeOnResultExecuting(_resultExecutingContext, item.Filter);

                    item.Filter.OnResultExecuting(_resultExecutingContext);

                    _diagnosticSource.AfterOnResultExecuting(_resultExecutingContext, item.Filter);

                    if (_resultExecutingContext.Cancel == true)
                    {
                        // Short-circuited by setting Cancel == true
                        _logger.ResourceFilterShortCircuited(item.Filter);

                        _resultExecutedContext = new PrivateResultExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                    else
                    {
                        await InvokeResultFilterAsync();

                        _resultExecutedContext = _resultExecutedContext ?? new PrivateResultExecutedContext(this);

                        _diagnosticSource.BeforeOnResultExecuted(_resultExecutedContext, item.Filter);

                        item.Filter.OnResultExecuted(_resultExecutedContext);

                        _diagnosticSource.AfterOnResultExecuted(_resultExecutedContext, item.Filter);
                    }
                }
                else
                {
                    _cursor.Reset();

                    // The empty result is always flowed back as the 'executed' result
                    _result = _result ?? new EmptyResult();

                    await InvokeResultAsync();

                    if (_exceptionHandled)
                    {
                        _exception = null;
                        _exceptionDispatchInfo = null;
                        _exceptionHandled = false;
                    }
                }
            }
            catch (Exception exception)
            {
                _exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                _exception = exception;
            }
        }

        private async Task InvokeResultAsync()
        {
            _diagnosticSource.BeforeActionResult(_controllerContext, _result);

            try
            {
                await _result.ExecuteResultAsync(_controllerContext);
            }
            finally
            {
                _diagnosticSource.AfterActionResult(_controllerContext, _result);
            }
        }

        // Marking as internal for Unit Testing purposes.
        internal static IActionResult CreateActionResult(Type declaredReturnType, object actionReturnValue)
        {
            if (declaredReturnType == null)
            {
                throw new ArgumentNullException(nameof(declaredReturnType));
            }

            // optimize common path
            var actionResult = actionReturnValue as IActionResult;
            if (actionResult != null)
            {
                return actionResult;
            }

            if (declaredReturnType == typeof(void) ||
                declaredReturnType == typeof(Task))
            {
                return new EmptyResult();
            }

            // Unwrap potential Task<T> types.
            var actualReturnType = GetTaskInnerTypeOrNull(declaredReturnType) ?? declaredReturnType;
            if (actionReturnValue == null &&
                typeof(IActionResult).IsAssignableFrom(actualReturnType))
            {
                throw new InvalidOperationException(
                    Resources.FormatActionResult_ActionReturnValueCannotBeNull(actualReturnType));
            }

            return new ObjectResult(actionReturnValue)
            {
                DeclaredType = actualReturnType
            };
        }

        private static Type GetTaskInnerTypeOrNull(Type type)
        {
            var genericType = ClosedGenericMatcher.ExtractGenericInterface(type, typeof(Task<>));

            return genericType?.GenericTypeArguments[0];
        }

        /// <summary>
        /// A one-way cursor for filters.
        /// </summary>
        /// <remarks>
        /// This will iterate the filter collection once per-stage, and skip any filters that don't have
        /// the one of interfaces that applies to the current stage.
        ///
        /// Filters are always executed in the following order, but short circuiting plays a role.
        ///
        /// Indentation reflects nesting.
        ///
        /// 1. Exception Filters
        ///     2. Authorization Filters
        ///     3. Action Filters
        ///        Action
        ///
        /// 4. Result Filters
        ///    Result
        ///
        /// </remarks>
        private struct FilterCursor
        {
            private int _index;
            private readonly IFilterMetadata[] _filters;

            public FilterCursor(int index, IFilterMetadata[] filters)
            {
                _index = index;
                _filters = filters;
            }

            public FilterCursor(IFilterMetadata[] filters)
            {
                _index = 0;
                _filters = filters;
            }

            public void Reset()
            {
                _index = 0;
            }

            public FilterCursorItem<TFilter, TFilterAsync> GetNextFilter<TFilter, TFilterAsync>()
                where TFilter : class
                where TFilterAsync : class
            {
                while (_index < _filters.Length)
                {
                    var filter = _filters[_index] as TFilter;
                    var filterAsync = _filters[_index] as TFilterAsync;

                    _index += 1;

                    if (filter != null || filterAsync != null)
                    {
                        return new FilterCursorItem<TFilter, TFilterAsync>(_index, filter, filterAsync);
                    }
                }

                return default(FilterCursorItem<TFilter, TFilterAsync>);
            }
        }

        private struct FilterCursorItem<TFilter, TFilterAsync>
        {
            public readonly int Index;
            public readonly TFilter Filter;
            public readonly TFilterAsync FilterAsync;

            public FilterCursorItem(int index, TFilter filter, TFilterAsync filterAsync)
            {
                Index = index;
                Filter = filter;
                FilterAsync = filterAsync;
            }
        }

        private class PrivateAuthorizationFilterContext : AuthorizationFilterContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateAuthorizationFilterContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters)
            {
                _invoker = invoker;
            }

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class PrivateResourceExecutingContext : ResourceExecutingContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateResourceExecutingContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters)
            {
                _invoker = invoker;
            }

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class PrivateResourceExecutedContext : ResourceExecutedContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateResourceExecutedContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters)
            {
                _invoker = invoker;
            }

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exceptionDispatchInfo != null)
                    {
                        return _invoker._exceptionDispatchInfo.SourceException;
                    }
                    else
                    {
                        return _invoker._exception;
                    }
                }

                set
                {
                    _invoker._exceptionDispatchInfo = null;
                    _invoker._exception = value;
                }
            }

            public override ExceptionDispatchInfo ExceptionDispatchInfo
            {
                get
                {
                    return _invoker._exceptionDispatchInfo;
                }

                set
                {
                    _invoker._exception = value?.SourceException;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override bool ExceptionHandled
            {
                get { return _invoker._exceptionHandled; }
                set { _invoker._exceptionHandled = value; }
            }

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class PrivateExceptionContext : ExceptionContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateExceptionContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters)
            {
                _invoker = invoker;
            }

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exceptionDispatchInfo != null)
                    {
                        return _invoker._exceptionDispatchInfo.SourceException;
                    }
                    else
                    {
                        return _invoker._exception;
                    }
                }

                set
                {
                    _invoker._exceptionDispatchInfo = null;
                    _invoker._exception = value;
                }
            }

            public override ExceptionDispatchInfo ExceptionDispatchInfo
            {
                get
                {
                    return _invoker._exceptionDispatchInfo;
                }

                set
                {
                    _invoker._exception = value?.SourceException;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override IActionResult Result
            {
                get { return _invoker._exceptionResult; }
                set { _invoker._exceptionResult = value; }
            }
        }

        private class PrivateActionExecutingContext : ActionExecutingContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateActionExecutingContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters, invoker._arguments, invoker._controller)
            {
                _invoker = invoker;
            }

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class PrivateActionExecutedContext : ActionExecutedContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateActionExecutedContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters, invoker._controller)
            {
                _invoker = invoker;
            }

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exceptionDispatchInfo != null)
                    {
                        return _invoker._exceptionDispatchInfo.SourceException;
                    }
                    else
                    {
                        return _invoker._exception;
                    }
                }

                set
                {
                    _invoker._exceptionDispatchInfo = null;
                    _invoker._exception = value;
                }
            }

            public override ExceptionDispatchInfo ExceptionDispatchInfo
            {
                get
                {
                    return _invoker._exceptionDispatchInfo;
                }

                set
                {
                    _invoker._exception = value?.SourceException;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override bool ExceptionHandled
            {
                get { return _invoker._exceptionHandled; }
                set { _invoker._exceptionHandled = value; }
            }

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class PrivateResultExecutingContext : ResultExecutingContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateResultExecutingContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters, invoker._result, invoker._controller)
            {
                _invoker = invoker;
            }

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set
                {
                    if (_invoker != null)
                    {
                        _invoker._result = value;
                    }
                }
            }
        }

        private class PrivateResultExecutedContext : ResultExecutedContext
        {
            private readonly ControllerActionInvoker _invoker;

            public PrivateResultExecutedContext(ControllerActionInvoker invoker)
                : base(invoker._controllerContext, invoker._filters, invoker._result, invoker._controller)
            {
                _invoker = invoker;
            }

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exceptionDispatchInfo != null)
                    {
                        return _invoker._exceptionDispatchInfo.SourceException;
                    }
                    else
                    {
                        return _invoker._exception;
                    }
                }

                set
                {
                    _invoker._exceptionDispatchInfo = null;
                    _invoker._exception = value;
                }
            }

            public override ExceptionDispatchInfo ExceptionDispatchInfo
            {
                get
                {
                    return _invoker._exceptionDispatchInfo;
                }

                set
                {
                    _invoker._exception = value?.SourceException;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override bool ExceptionHandled
            {
                get { return _invoker._exceptionHandled; }
                set { _invoker._exceptionHandled = value; }
            }
        }
    }
}
