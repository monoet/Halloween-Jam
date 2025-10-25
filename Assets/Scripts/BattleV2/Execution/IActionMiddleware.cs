using System;
using System.Threading.Tasks;

namespace BattleV2.Execution
{
    public interface IActionMiddleware
    {
        Task InvokeAsync(ActionContext context, Func<Task> next);
    }
}

