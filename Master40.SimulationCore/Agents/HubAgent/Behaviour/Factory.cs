﻿using Master40.DB.Enums;
using Master40.SimulationCore.Types;
using static FOperations;
using static FUpdateStartConditions;

namespace Master40.SimulationCore.Agents.HubAgent.Behaviour
{
    public static class Factory
    {
        public static IBehaviour Get(SimulationType simType)
        {
            switch (simType)
            {
                default:
                    return Default();
            }
        }

        private static IBehaviour Default()
        {

            return new Default();

        }

    }
}