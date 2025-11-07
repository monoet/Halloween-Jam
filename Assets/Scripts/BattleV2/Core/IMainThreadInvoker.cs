using System;
using System.Threading.Tasks;

namespace BattleV2.Core
{
    public interface IMainThreadInvoker
    {
        Task RunAsync(Func<Task> action);
        void Run(Action action);
    }
}
