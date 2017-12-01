﻿using System;
using System.Reflection;
using System.Threading.Tasks;

namespace SexyProxy
{
    public class AsyncInvocationHandler
    {
        private Func<IAsyncInvocation, Task<object>> asyncHandler;
        private Func<object, MethodInfo, PropertyInfo, bool> proxyPredicate;
        private AsyncInvocationMode asyncMode;

        /// <summary>
        /// Provides a handler for all methods, async or otherwise.
        /// </summary>
        public AsyncInvocationHandler(Func<IAsyncInvocation, Task<object>> asyncHandler, Func<object, MethodInfo, PropertyInfo, bool> proxyPredicate = null, AsyncInvocationMode asyncMode = AsyncInvocationMode.Throw)
        {
            this.asyncHandler = asyncHandler;
            this.proxyPredicate = proxyPredicate ?? ((x, method, property) => true);
            this.asyncMode = asyncMode;
        }

        public bool IsHandlerActive(object proxy, MethodInfo method, PropertyInfo property)
        {
            return proxyPredicate(proxy, method, property);
        }

        private Task<object> GetTask(IAsyncInvocation invocation)
        {
            var task = asyncHandler(invocation);

            if (!typeof(Task).IsAssignableFrom(invocation.Method.ReturnType) && !task.IsCompleted)
            {
                switch (asyncMode)
                {
                    case AsyncInvocationMode.Throw:
                        throw new InvalidAsyncException(
                            "Cannot use async tasks (await) in proxy handler for methods with a non-Task return-type. To force a synchronous wait, pass in AsyncInvocationMode.Wait when creating your proxy or InvocationHandler.");
                    case AsyncInvocationMode.Wait:
                        task.Wait();
                        break;
                }
            }
            return task;
        }

        public async Task<T> AsyncInvokeT<T>(AsyncInvocationT<T> invocation)
        {
            var task = GetTask(invocation);
            if (task.Status == TaskStatus.RanToCompletion && !(task.Result is T))
            {
                if (task.Result != null || typeof(T).IsValueType)
                {
                    throw new InvalidAsyncException($"The invocation returned {task.Result ?? "null"}, but {invocation.Method.DeclaringType.FullName}.{invocation.Method.Name} expected an instance of {typeof(T)}");
                }
            }
            var result = await task;
            return (T)result;
        }

        public async Task VoidAsyncInvoke(VoidAsyncInvocation invocation)
        {
            var task = GetTask(invocation);
            await task;
        }

        public T InvokeT<T>(InvocationT<T> invocation)
        {
            var task = GetTask(invocation);
            return (T)task.Result;
        }

        public void VoidInvoke(VoidInvocation invocation)
        {
            var task = GetTask(invocation);
            task.Wait();
        }
    }
}
