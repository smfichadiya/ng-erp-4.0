﻿using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Master40.DB.DB.Models
{
    public interface IDemandToProvider
    {
        int Id { get; set; }
        int ArticleId { get; set; }
        Article Article { get; set; }
        decimal Quantity { get; set; }
        int? DemandRequesterId { get; set; }
        DemandToProvider DemandRequester { get; set; }
        List<DemandToProvider> DemandProvider { get; set; }
        State State { get; set; }

    }

    

    /// <summary>
    /// derived Class for Damand to DemandProvider
    /// To Access a specific Demand use:
    /// var purchaseDemands = context.Demands.OfType<DemandPurchase>().ToList();
    /// </summary>
    public class DemandToProvider : BaseEntity, IDemandToProvider
    {
        public int ArticleId { get; set; }
        public Article Article { get; set; }
        [Required]
        public decimal Quantity { get; set; }
        public int? DemandRequesterId { get; set; }

        public DemandToProvider DemandRequester { get; set; }
        public virtual List<DemandToProvider> DemandProvider { get; set; }

        [Required]
        public int StateId { get; set; }

        public State State { get; set; } 
        //public ICollection<ArticleToDemand> ArtilceToDemand { get; set; }
    }

    public class DemandOrderPart : DemandToProvider
    {
        public int OrderPartId { get; set; }
        public OrderPart OrderPart { get; set; }
    }
    public class DemandProductionOrderBom : DemandToProvider
    {
        public int? ProductionOrderBomId { get; set; }
        public ProductionOrderBom ProductionOrderBom { get; set; }
    }
    public class DemandStock : DemandToProvider
    {
        public int StockId { get; set; }
        public Stock Stock { get; set; }
    }
    public class DemandProviderStock : DemandToProvider
    {
        public int StockId { get; set; }
        public Stock Stock { get; set; }
    }
    public class DemandProviderPurchasePart : DemandToProvider
    {
        public int PurchasePartId { get; set; }
        public PurchasePart PurchasePart { get; set; }
    }
    public class DemandProviderProductionOrder : DemandToProvider
    {
        public int ProductionOrderId { get; set; }
        public ProductionOrder ProductionOrder { get; set; }
    }
}

/*
namespace Master40.DB.Models
{
    public class DemandToProviderTest : IDemand, IProvider
    {
        public int RequesterId { get; set; }
        public int ProviderId { get; set; }
        public string Source { get; set; }
    }
}
*/