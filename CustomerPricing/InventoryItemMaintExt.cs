using System;
using PX.Data;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;

namespace CustomerPricing
{
    /*───────────────────── Price cascade helper ─────────────────────*/
    internal static class PriceCascade
    {
        private sealed class PriceTypeBase : PX.Data.BQL.BqlString.Constant<PriceTypeBase>
        { public PriceTypeBase() : base("B") { } }

        public static void Execute(int? inventoryID, decimal basePrice)
        {
            if (inventoryID == null)
                return;

            var priceGraph = PXGraph.CreateInstance<ARSalesPriceMaint>();

            foreach (ARSalesPrice pr in
                     SelectFrom<ARSalesPrice>
                     .Where<ARSalesPrice.inventoryID.IsEqual<PX.Data.BQL.P.AsInt>
                         .And<ARSalesPrice.priceType.IsNotEqual<PriceTypeBase>>>.View
                     .Select(priceGraph, inventoryID))
            {
                decimal? pct = pr.GetExtension<ARSalesPriceExt>()?.UsrPricePercentOff;
                if (pct == null) continue;

                decimal newPrice = basePrice * (1 - pct.Value / 100);
                PXTrace.WriteInformation("Updating price for inventory ID {0} to {1} (base price: {2}, percent off: {3})",
                    pr.InventoryID, newPrice, basePrice, pct);
                if (pr.SalesPrice != newPrice)
                {
                    pr.SalesPrice = newPrice;
                    priceGraph.Caches<ARSalesPrice>().Update(pr);
                }
            }
            priceGraph.Actions.PressSave();
        }
    }

    /*───────────────────── Stock items (IN202500) ────────────────────*/
    public sealed class InventoryItemMaintPriceCascadeExt : PXGraphExtension<PX.Objects.IN.InventoryItemMaint>
    {
        protected void _(Events.FieldUpdated<InventoryItemCurySettings.basePrice> e)
        {
            var row = (InventoryItemCurySettings)e.Row;
            if (row?.BasePrice != null)
                PriceCascade.Execute(row.InventoryID, row.BasePrice.Value);
        }
    }

    /*───────────────────── Non‑stock items (IN203000) ────────────────*/
    public sealed class NonStockItemMaintPriceCascadeExt : PXGraphExtension<PX.Objects.IN.NonStockItemMaint>
    {
        protected void _(Events.FieldUpdated<InventoryItemCurySettings.basePrice> e)
        {
            var row = (InventoryItemCurySettings)e.Row;
            if (row?.BasePrice != null)
                PriceCascade.Execute(row.InventoryID, row.BasePrice.Value);
        }
    }
}
