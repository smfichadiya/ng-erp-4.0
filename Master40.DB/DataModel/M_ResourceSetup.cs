﻿using System.Collections.Generic;

namespace Master40.DB.DataModel
{
    /*
     * JOINTABLE Describes a combination of Resource and ResourceTool to provide a skill
     */

    public class M_ResourceSetup : BaseEntity
    {
        public string Name { get; set; }
        public int ResourceCapabilityProviderId { get; set; }
        public M_ResourceCapabilityProvider ResourceCapabilityProvider { get; set; }
        public int ResourceId { get; set; }
        public M_Resource Resource { get; set; }
        public long SetupTime { get; set; }
        public bool UsedInSetup { get; set; }
        public bool UsedInProcess { get; set; }
    }
}
