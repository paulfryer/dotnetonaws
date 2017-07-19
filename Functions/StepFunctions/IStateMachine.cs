using System;

namespace Functions
{
    public interface IStateMachine {
        Type StartAt { get; }
        string Describe(string region, string accountId);
    }


}
