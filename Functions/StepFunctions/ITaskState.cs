using System;

namespace Functions
{
    public interface ITaskState : IState {
        Type Next { get; }
    }


}
