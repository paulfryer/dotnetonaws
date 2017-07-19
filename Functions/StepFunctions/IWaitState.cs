using System;

namespace Functions
{
    public interface IWaitState {
        int Seconds { get; }
        Type Next { get; }
    }


}
