using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BattleV2.Execution
{
    public sealed class ActionPipeline
    {
        private readonly IReadOnlyList<IActionMiddleware> middlewares;

        public ActionPipeline(IReadOnlyList<IActionMiddleware> middlewares)
        {
            this.middlewares = middlewares ?? Array.Empty<IActionMiddleware>();
        }

        public Task ExecuteAsync(ActionContext context)
        {
            var walker = new Walker(middlewares, context);
            return walker.InvokeNext();
        }

        private sealed class Walker
        {
            private readonly IReadOnlyList<IActionMiddleware> middlewares;
            private readonly ActionContext context;
            private int index;

            public Walker(IReadOnlyList<IActionMiddleware> middlewares, ActionContext context)
            {
                this.middlewares = middlewares;
                this.context = context;
                index = 0;
            }

            public Task InvokeNext()
            {
                if (index >= middlewares.Count)
                {
                    return Task.CompletedTask;
                }

                var middleware = middlewares[index++];
                return middleware.InvokeAsync(context, InvokeNext);
            }
        }
    }
}

