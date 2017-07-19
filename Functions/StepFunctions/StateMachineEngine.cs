﻿using System;
using System.Threading.Tasks;
using System.Reflection;

namespace Functions
{
    public class StateMachineEngine<TStateMachine, TContext>
        where TStateMachine : IStateMachine
        where TContext : IContext
    {
        TStateMachine stateMachine;
        TContext context;

        public StateMachineEngine(TContext context)
        {
            stateMachine = (TStateMachine)Activator.CreateInstance(typeof(TStateMachine));
            this.context = context;
        }

        public async Task Start()
        {
            await ChangeState(stateMachine.StartAt);
        }

        private async Task ChangeState(Type type)
        {
            try {

                Console.WriteLine("Changing state to: " + type.Name);

                var state = Activator.CreateInstance(type) as IState;

                if (state is ITaskState<TContext>)
                {
                    var taskState = state as ITaskState<TContext>;
                    context = await taskState.Execute(context);
                    if (!taskState.End)
                        await ChangeState(taskState.Next);
                }
                else if (state is IChoiceState)
                {
                    var choiceState = state as IChoiceState;
                    foreach (var choice in choiceState.Choices)
                    {
                        var compairValue = typeof(TContext).GetProperty(choice.Variable).GetValue(context);

                        var operatorStart = choice.Operator.Substring(0, 2).ToUpper();

                        switch (operatorStart)
                        {
                            case "BO":
                                if ((bool)compairValue == (bool)choice.Value)
                                    await ChangeState(choice.Next);
                                break;
                            case "NU":
                                var numericCompairValue = Convert.ToDecimal(compairValue);
                                var numericValue = Convert.ToDecimal(choice.Value);
                                switch (choice.Operator)
                                {
                                    case Operator.NumericEquals:
                                        if (numericCompairValue == numericValue)
                                            await ChangeState(choice.Next);
                                        break;
                                    case Operator.NumericGreaterThan:
                                        if (numericCompairValue > numericValue)
                                            await ChangeState(choice.Next);
                                        break;
                                    default: throw new NotImplementedException("Not implemented: " + choice.Operator);
                                }
                                break;
                            default: throw new NotImplementedException("Operator not supported: " + choice.Operator);
                        }
                    }
                }
                else if (state is IWaitState) {
                    var waitState = state as IWaitState;
                    await Task.Delay(waitState.Seconds);
                    await ChangeState(waitState.Next);
                }
                else if (state is IPassState)
                {
                    // nothing to do here..
                    var passSate = state as IPassState;
                    
                }
                else throw new NotImplementedException("State type not implemented: " + type.Name);

                

            } catch (Exception exception) {
                Console.WriteLine(exception.Message);
                throw;
            }
           
        }
    }
}
