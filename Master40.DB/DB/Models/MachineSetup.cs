﻿using Newtonsoft.Json;

namespace Master40.DB.Models
{
    /// <summary>
    /// Join Table to N:M between Machine and Tool
    /// </summary>
    public class MachineSetup : BaseEntity
    {
        public string Name { get; set; }
        public int MachineId { get; set; }
        [JsonIgnore]
        public Machine Machine { get; set; }
        public int ToolId { get; set; }
        [JsonIgnore]
        public Tool Tool { get; set; }
        public int MachineGroupId { get; set; }
        [JsonIgnore]
        public MachineGroup MahcineGroup { get; set; }
        public int SetupTime { get; set; }
        public string Discription { get; set; }
    }
}
