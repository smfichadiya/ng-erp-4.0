﻿using System;

namespace Master40.DB.GanttPlanModel
{
    public partial class GptblProductionorderOperationActivityResourceIntervalUdf
    {
        public string ClientId { get; set; }
        public string ProductionorderId { get; set; }
        public string OperationId { get; set; }
        public string AlternativeId { get; set; }
        public int ActivityId { get; set; }
        public int SplitId { get; set; }
        public DateTime DateFrom { get; set; }
        public string ResourceId { get; set; }
        public int ResourceType { get; set; }
        public string UdfId { get; set; }
        public string Value { get; set; }
    }
}
