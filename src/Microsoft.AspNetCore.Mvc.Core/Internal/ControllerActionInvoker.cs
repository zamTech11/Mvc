// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
#if NETSTANDARD1_5
using System.Reflection;
#endif
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Internal
{
    public class ControllerActionInvoker : IActionInvoker
    {
        private readonly IControllerArgumentBinder _argumentBinder;
        private readonly IControllerFactory _controllerFactory;
        private readonly DiagnosticSource _diagnosticSource;
        private readonly ILogger _logger;

        private readonly ControllerActionDescriptor _actionDescriptor;
        private FilterCursor _cursor;
        private ObjectMethodExecutor _executor;

        private IDictionary<string, object> _arguments;
        private readonly ControllerContext _controllerContext;
        private object _controller;
        private IFilterMetadata[] _filters;
        private IActionResult _result;
        private IActionResult _exceptionResult;
        private Exception _exception;
        private ExceptionDispatchInfo _exceptionDispatchInfo;
        private bool _exceptionHandled;

        private AuthorizationFilterContext _authorizationContext;

        private ResourceExecutingContext _resourceExecutingContext;
        private ResourceExecutedContext _resourceExecutedContext;

        private ExceptionContext _exceptionContext;

        private ActionExecutingContext _actionExecutingContext;
        private ActionExecutedContext _actionExecutedContext;

        private ResultExecutingContext _resultExecutingContext;
        private ResultExecutedContext _resultExecutedContext;

        public ControllerActionInvoker(
            ControllerContext controllerContext,
            IControllerFactory controllerFactory,
            IControllerArgumentBinder argumentBinder,
            ControllerActionInvokerCache cache,
            ILogger logger,
            DiagnosticSource diagnosticSource)
        {
            if (controllerContext == null)
            {
                throw new ArgumentNullException(nameof(controllerContext));
            }

            if (controllerFactory == null)
            {
                throw new ArgumentNullException(nameof(controllerFactory));
            }

            if (argumentBinder == null)
            {
                throw new ArgumentNullException(nameof(argumentBinder));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (diagnosticSource == null)
            {
                throw new ArgumentNullException(nameof(diagnosticSource));
            }

            if (controllerFactory == null)
            {
                throw new ArgumentNullException(nameof(controllerFactory));
            }

            _controllerContext = controllerContext;
            _controllerFactory = controllerFactory;
            _argumentBinder = argumentBinder;
            _logger = logger;
            _diagnosticSource = diagnosticSource;

            // PERF: cache this in a field to avoid overhead of calling property that won't change.
            _actionDescriptor = controllerContext.ActionDescriptor;

            var cacheEntry = cache.GetState(controllerContext);

            _filters = cacheEntry.Filters;
            _cursor = new FilterCursor(_filters);
            _executor = cacheEntry.ActionMethodExecutor;
        }

        public virtual async Task InvokeAsync()
        {
            await InvokeAllAuthorizationFiltersAsync();

            // If Authorization Filters return a result, it's a short circuit because
            // authorization failed. We don't execute Result Filters around the result.
            Debug.Assert(_authorizationContext != null);
            if (_result != null)
            {
                await InvokeResultAsync(_result);
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
                return;
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

            _authorizationContext = new DefaultAuthorizationFilterContext(this);
            return InvokeAuthorizationFilterAsync();
        }

        private async Task InvokeAuthorizationFilterAsync()
        {
            // We should never get here if we already have a result.
            Debug.Assert(_authorizationContext != null);
            Debug.Assert(_result == null);

            var current = _cursor.GetNextFilter<IAuthorizationFilter, IAsyncAuthorizationFilter>();
            if (current.FilterAsync != null)
            {
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

            _resourceExecutingContext = new DefaultResourceExecutingContext(this);
            return InvokeResourceFilterAsync();
        }

        private async Task<ResourceExecutedContext> InvokeResourceFilterAsync()
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

            var item = _cursor.GetNextFilter<IResourceFilter, IAsyncResourceFilter>();
            try
            {
                if (item.FilterAsync != null)
                {
                    _diagnosticSource.BeforeOnResourceExecution(_resourceExecutingContext, item.FilterAsync);

                    await item.FilterAsync.OnResourceExecutionAsync(_resourceExecutingContext, InvokeResourceFilterAsync);

                    _diagnosticSource.AfterOnResourceExecution(_resourceExecutedContext, item.FilterAsync);

                    if (_resourceExecutedContext == null)
                    {
                        // If we get here then the filter didn't call 'next' indicating a short circuit
                        if (_result != null)
                        {
                            _logger.ResourceFilterShortCircuited(item.FilterAsync);

                            await InvokeResultAsync(_result);
                        }

                        _resourceExecutedContext = new DefaultResourceExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                }
                else if (item.Filter != null)
                {
                    _diagnosticSource.BeforeOnResourceExecuting(_resourceExecutingContext, item.Filter);

                    item.Filter.OnResourceExecuting(_resourceExecutingContext);

                    _diagnosticSource.AfterOnResourceExecuting(_resourceExecutingContext, item.Filter);

                    if (_result != null)
                    {
                        // Short-circuited by setting a result.
                        _logger.ResourceFilterShortCircuited(item.Filter);

                        await InvokeResultAsync(_result);

                        _resourceExecutedContext = new DefaultResourceExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                    else
                    {
                        _diagnosticSource.BeforeOnResourceExecuted(_resourceExecutedContext, item.Filter);

                        item.Filter.OnResourceExecuted(await InvokeResourceFilterAsync());

                        _diagnosticSource.AfterOnResourceExecuted(_resourceExecutedContext, item.Filter);
                    }
                }
                else
                {
                    // >> ExceptionFilters >> Model Binding >> ActionFilters >> Action
                    await InvokeAllExceptionFiltersAsync();

                    // If Exception Filters provide a result, it's a short-circuit due to an exception.
                    // We don't execute Result Filters around the result.
                    Debug.Assert(_exceptionContext != null);
                    if (_exceptionResult != null)
                    {
                        // This means that exception filters returned a result to 'handle' an error.
                        // We're not interested in seeing the exception details since it was handled.
                        _result = _exceptionResult;
                        _exception = null;
                        _exceptionDispatchInfo = null;

                        await InvokeResultAsync(_result);

                        _resourceExecutedContext = new DefaultResourceExecutedContext(this);
                    }
                    else if (_exceptionContext.Exception != null)
                    {
                        // If we get here, this means that we have an unhandled exception.
                        // Exception filted didn't handle this, so send it on to resource filters.
                        _resourceExecutedContext = new DefaultResourceExecutedContext(this);
                    }
                    else
                    {
                        // We have a successful 'result' from the action or an Action Filter, so run
                        // Result Filters.
                        Debug.Assert(_actionExecutedContext != null);

                        // >> ResultFilters >> (Result)
                        await InvokeAllResultFiltersAsync(_result);

                        _resourceExecutedContext = new DefaultResourceExecutedContext(this);
                    }
                }
            }
            catch (Exception exception)
            {
                _resourceExecutedContext = new DefaultResourceExecutedContext(this)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception)
                };
            }

            Debug.Assert(_resourceExecutedContext != null);
            return _resourceExecutedContext;
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

                Debug.Assert(_exceptionContext != null);
                if (_exceptionContext.Exception != null)
                {
                    _diagnosticSource.BeforeOnExceptionAsync(_exceptionContext, current.FilterAsync);

                    // Exception filters only run when there's an exception - unsetting it will short-circuit
                    // other exception filters.
                    await current.FilterAsync.OnExceptionAsync(_exceptionContext);

                    _diagnosticSource.AfterOnExceptionAsync(_exceptionContext, current.FilterAsync);

                    if (_exceptionContext.Exception == null)
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

                Debug.Assert(_exceptionContext != null);
                if (_exceptionContext.Exception != null)
                {
                    _diagnosticSource.BeforeOnException(_exceptionContext, current.Filter);

                    // Exception filters only run when there's an exception - unsetting it will short-circuit
                    // other exception filters.
                    current.Filter.OnException(_exceptionContext);

                    _diagnosticSource.AfterOnException(_exceptionContext, current.Filter);

                    if (_exceptionContext.Exception == null)
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
                Debug.Assert(_exceptionContext == null);
                _exceptionContext = new DefaultExceptionContext(this);

                try
                {
                    await InvokeAllActionFiltersAsync();
                    Debug.Assert(_actionExecutedContext != null);

                    if (_exceptionHandled)
                    {
                        _exception = null;
                        _exceptionDispatchInfo = null;
                        _exceptionHandled = false;
                    }
                }
                catch (Exception exception)
                {
                    _exceptionContext.ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);
                }
            }
        }

        private async Task InvokeAllActionFiltersAsync()
        {
            _cursor.Reset();

            _controller = _controllerFactory.CreateController(_controllerContext);

            _arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            await _argumentBinder.BindArgumentsAsync(_controllerContext, _controller, _arguments);

            _actionExecutingContext = new DefaultActionExecutingContext(this);

            await InvokeActionFilterAsync();
        }

        private async Task<ActionExecutedContext> InvokeActionFilterAsync()
        {
            Debug.Assert(_actionExecutingContext != null);
            if (_actionExecutingContext.Result != null)
            {
                // If we get here, it means that an async filter set a result AND called next(). This is forbidden.
                var message = Resources.FormatAsyncActionFilter_InvalidShortCircuit(
                    typeof(IAsyncActionFilter).Name,
                    nameof(ActionExecutingContext.Result),
                    typeof(ActionExecutingContext).Name,
                    typeof(ActionExecutionDelegate).Name);

                throw new InvalidOperationException(message);
            }

            var item = _cursor.GetNextFilter<IActionFilter, IAsyncActionFilter>();
            try
            {
                if (item.FilterAsync != null)
                {
                    _diagnosticSource.BeforeOnActionExecution(_actionExecutingContext, item.FilterAsync);

                    await item.FilterAsync.OnActionExecutionAsync(_actionExecutingContext, InvokeActionFilterAsync);

                    _diagnosticSource.AfterOnActionExecution(_actionExecutedContext, item.FilterAsync);

                    if (_actionExecutedContext == null)
                    {
                        // If we get here then the filter didn't call 'next' indicating a short circuit
                        _logger.ActionFilterShortCircuited(item.FilterAsync);

                        _actionExecutedContext = new DefaultActionExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                }
                else if (item.Filter != null)
                {
                    _diagnosticSource.BeforeOnActionExecuting(_actionExecutingContext, item.Filter);

                    item.Filter.OnActionExecuting(_actionExecutingContext);

                    _diagnosticSource.AfterOnActionExecuting(_actionExecutingContext, item.Filter);

                    if (_actionExecutingContext.Result != null)
                    {
                        // Short-circuited by setting a result.
                        _logger.ActionFilterShortCircuited(item.Filter);

                        _actionExecutedContext = new DefaultActionExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                    else
                    {
                        _diagnosticSource.BeforeOnActionExecuted(_actionExecutedContext, item.Filter);

                        item.Filter.OnActionExecuted(await InvokeActionFilterAsync());

                        _diagnosticSource.BeforeOnActionExecuted(_actionExecutedContext, item.Filter);
                    }
                }
                else
                {
                    // All action filters have run, execute the action method.

                    try
                    {
                        _diagnosticSource.BeforeActionMethod(
                            _controllerContext,
                            _actionExecutingContext.ActionArguments,
                            _actionExecutingContext.Controller);

                        var actionMethodInfo = _actionDescriptor.MethodInfo;

                        var arguments = ControllerActionExecutor.PrepareArguments(
                            _actionExecutingContext.ActionArguments,
                            actionMethodInfo.GetParameters());

                        _logger.ActionMethodExecuting(_actionExecutingContext, arguments);

                        var actionReturnValue = await ControllerActionExecutor.ExecuteAsync(
                            _executor,
                            _controller,
                            arguments);

                        _result = CreateActionResult(actionMethodInfo.ReturnType, actionReturnValue);

                        _logger.ActionMethodExecuted(_actionExecutingContext, _result);
                    }
                    finally
                    {
                        _diagnosticSource.AfterActionMethod(
                            _controllerContext,
                            _actionExecutingContext.ActionArguments,
                            _actionExecutingContext.Controller,
                            _result);
                    }

                    _actionExecutedContext = new DefaultActionExecutedContext(this);
                }
            }
            catch (Exception exception)
            {
                // Exceptions thrown by the action method OR filters bubble back up through ActionExcecutedContext.
                _actionExecutedContext = new DefaultActionExecutedContext(this)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception)
                };
            }
            return _actionExecutedContext;
        }

        private async Task InvokeAllResultFiltersAsync(IActionResult result)
        {
            _cursor.Reset();

            _resultExecutingContext = new DefaultResultExecutingContext(this);
            await InvokeResultFilterAsync();
            Debug.Assert(_resultExecutedContext != null);

            if (_exceptionHandled)
            {
                _exception = null;
                _exceptionDispatchInfo = null;
            }
        }

        private async Task<ResultExecutedContext> InvokeResultFilterAsync()
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

            try
            {
                var item = _cursor.GetNextFilter<IResultFilter, IAsyncResultFilter>();
                if (item.FilterAsync != null)
                {
                    _diagnosticSource.BeforeOnResultExecution(_resultExecutingContext, item.FilterAsync);

                    await item.FilterAsync.OnResultExecutionAsync(_resultExecutingContext, InvokeResultFilterAsync);

                    _diagnosticSource.AfterOnResultExecution(_resultExecutedContext, item.FilterAsync);

                    if (_resultExecutedContext == null || _resultExecutingContext.Cancel == true)
                    {
                        // Short-circuited by not calling next || Short-circuited by setting Cancel == true
                        _logger.ResourceFilterShortCircuited(item.FilterAsync);

                        _resultExecutedContext = new DefaultResultExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                }
                else if (item.Filter != null)
                {
                    _diagnosticSource.BeforeOnResultExecuting(_resultExecutingContext, item.Filter);

                    item.Filter.OnResultExecuting(_resultExecutingContext);

                    _diagnosticSource.AfterOnResultExecuting(_resultExecutingContext, item.Filter);

                    if (_resultExecutingContext.Cancel == true)
                    {
                        // Short-circuited by setting Cancel == true
                        _logger.ResourceFilterShortCircuited(item.Filter);

                        _resultExecutedContext = new DefaultResultExecutedContext(this)
                        {
                            Canceled = true,
                        };
                    }
                    else
                    {
                        _diagnosticSource.BeforeOnResultExecuted(_resultExecutedContext, item.Filter);

                        item.Filter.OnResultExecuted(await InvokeResultFilterAsync());

                        _diagnosticSource.AfterOnResultExecuted(_resultExecutedContext, item.Filter);
                    }
                }
                else
                {
                    _cursor.Reset();

                    // The empty result is always flowed back as the 'executed' result
                    if (_result == null)
                    {
                        _result = new EmptyResult();
                    }

                    await InvokeResultAsync(_result);

                    Debug.Assert(_resultExecutedContext == null);
                    _resultExecutedContext = new DefaultResultExecutedContext(this);
                }
            }
            catch (Exception exception)
            {
                _resultExecutedContext = new DefaultResultExecutedContext(this)
                {
                    ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception)
                };
            }

            return _resultExecutedContext;
        }

        private async Task InvokeResultAsync(IActionResult result)
        {
            _diagnosticSource.BeforeActionResult(_controllerContext, result);

            try
            {
                await result.ExecuteResultAsync(_controllerContext);
            }
            finally
            {
                _diagnosticSource.AfterActionResult(_controllerContext, result);
            }
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

        private class DefaultAuthorizationFilterContext : AuthorizationFilterContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultAuthorizationFilterContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class DefaultResourceExecutingContext : ResourceExecutingContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultResourceExecutingContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class DefaultResourceExecutedContext : ResourceExecutedContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultResourceExecutedContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override bool Canceled { get; set; }

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exception == null && _invoker._exceptionDispatchInfo != null)
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
                    _invoker._exception = null;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override bool ExceptionHandled
            {
                get { return _invoker._exceptionHandled; }
                set { _invoker._exceptionHandled = value; }
            }

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class DefaultExceptionContext : ExceptionContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultExceptionContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exception == null && _invoker._exceptionDispatchInfo != null)
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
                    _invoker._exception = null;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._exceptionResult; }
                set { _invoker._exceptionResult = value; }
            }
        }

        private class DefaultActionExecutingContext : ActionExecutingContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultActionExecutingContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override IDictionary<string, object> ActionArguments => _invoker._arguments;

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override object Controller => _invoker._controller;

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class DefaultActionExecutedContext : ActionExecutedContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultActionExecutedContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override bool Canceled { get; set; }

            public override object Controller => _invoker._controller;

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exception == null && _invoker._exceptionDispatchInfo != null)
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
                    _invoker._exception = null;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override bool ExceptionHandled
            {
                get { return _invoker._exceptionHandled; }
                set { _invoker._exceptionHandled = value; }
            }

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class DefaultResultExecutingContext : ResultExecutingContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultResultExecutingContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override bool Cancel { get; set; }

            public override object Controller => _invoker._controller;

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
                set { _invoker._result = value; }
            }
        }

        private class DefaultResultExecutedContext : ResultExecutedContext
        {
            private readonly ControllerActionInvoker _invoker;

            public DefaultResultExecutedContext(ControllerActionInvoker invoker)
            {
                _invoker = invoker;
            }

            public override ActionContext ActionContext => _invoker._controllerContext;

            public override bool Canceled { get; set; }

            public override object Controller
            {
                get
                {
                    throw new NotImplementedException();
                }
            }

            public override Exception Exception
            {
                get
                {
                    if (_invoker._exception == null && _invoker._exceptionDispatchInfo != null)
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
                    _invoker._exception = null;
                    _invoker._exceptionDispatchInfo = value;
                }
            }

            public override bool ExceptionHandled
            {
                get { return _invoker._exceptionHandled; }
                set { _invoker._exceptionHandled = value; }
            }

            public override IList<IFilterMetadata> Filters => _invoker._filters;

            public override IActionResult Result
            {
                get { return _invoker._result; }
            }
        }
    }
}
