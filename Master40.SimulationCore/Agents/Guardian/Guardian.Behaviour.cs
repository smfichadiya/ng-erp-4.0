﻿using Akka.Actor;
using Master40.SimulationCore.Helper;
using Master40.SimulationCore.MessageTypes;
using System;
using static Master40.SimulationCore.Agents.Guardian;

namespace Master40.SimulationCore.Agents
{
    public class GuardianBehaviour : Behaviour
    {
        private GuardianBehaviour(Func<IUntypedActorContext, AgentSetup, IActorRef> childMaker) : base(childMaker) { }

        public static GuardianBehaviour Get(Func<IUntypedActorContext, AgentSetup, IActorRef> childMaker)
        {
            return new GuardianBehaviour(childMaker);
        }

        public override bool Action(Agent agent, object message)
        {
            switch (message)
            {
                case Instruction.CreateChild m: CreateChild(agent, m.GetObjectFromMessage); break;
                default: return false;
            }
            return true;
        }

        private void CreateChild(Agent agent, AgentSetup setup)
        {
            var childRef = agent.Behaviour.ChildMaker(agent.Context, setup);
            agent.Send(BasicInstruction.ChildRef.Create(childRef, agent.Sender));
        }

        
    }
}
