﻿using System;
using System.Collections.Generic;

namespace Master40.DB.GanttplanDB.Models
{
    public partial class RoutingOperationActivityPrt
    {
        public string RoutingId { get; set; }
        public string OperationId { get; set; }
        public string AlternativeId { get; set; }
        public long SplitId { get; set; }
        public long ActivityId { get; set; }
        public string PrtId { get; set; }
        public string GroupId { get; set; }
        public double? PrtAllocation { get; set; }
    }
}