using System.Threading.Tasks;

namespace Functions
{
    public interface ITaskState<TContext> : ITaskState
    {
        Task<TContext> Execute(TContext context);
    }


}
