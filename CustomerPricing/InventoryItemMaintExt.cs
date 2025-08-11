using System;
using System.Linq;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;

namespace CustomerPricing
{
    /*───────────────────── Price cascade helper ─────────────────────*/
    internal static class PriceCascade
    {
        /// <summary>“B” = base price row in ARSalesPrice.</summary>
        private sealed class PriceTypeBase : BqlString.Constant<PriceTypeBase>
        { public PriceTypeBase() : base("B") { } }

        /// <summary>
        /// Recalculates every non‑base <see cref="ARSalesPrice"/> row for the given inventory ID
        /// according to the UsrPricePercentOff field of <see cref="ARSalesPriceExt"/>.
        /// All changes are staged in <paramref name="graph"/>, caller controls the final Persist.
        /// </summary>
        public static void UpdateSalesPrices(PXGraph graph, int? inventoryID, decimal basePrice)
        {
            if (inventoryID == null) return;

            foreach (ARSalesPrice pr in SelectFrom<ARSalesPrice>
                     .Where<ARSalesPrice.inventoryID.IsEqual<P.AsInt>
                         .And<ARSalesPrice.priceType.IsNotEqual<PriceTypeBase>>>.View
                     .Select(graph, inventoryID))
            {
                decimal? pct = pr.GetExtension<ARSalesPriceExt>()?.UsrPricePercentOff;
                if (pct == null) continue;

                // round to four decimals – same precision ARSalesPrice.SalesPrice uses
                decimal newPrice = Math.Round(basePrice * (1 - pct.Value / 100m), 4,
                                              MidpointRounding.AwayFromZero);

                if (pr.SalesPrice != newPrice)
                {
                    pr.SalesPrice = newPrice;
                    graph.Caches<ARSalesPrice>().Update(pr);
                }
            }
        }
    }

    /*───────────────────── Stock items (IN202500) ────────────────────*/
    public sealed class InventoryItemMaintPriceCascadeExt
        : PXGraphExtension<PX.Objects.IN.InventoryItemMaint>
    {
        public static bool IsActive() => true;

        public delegate void PersistDelegate();
        [PXOverride]
        public void Persist(PersistDelegate baseMethod)
        {
            ApplyPriceCascade();
            baseMethod();
        }

        private void ApplyPriceCascade()
        {
            var cache = Base.Caches<InventoryItemCurySettings>();

            foreach (InventoryItemCurySettings row in cache.Inserted.Cast<InventoryItemCurySettings>()
                     .Concat(cache.Updated.Cast<InventoryItemCurySettings>()))
            {
                if (row?.BasePrice != null)
                    PriceCascade.UpdateSalesPrices(Base, row.InventoryID, row.BasePrice.Value);
            }
        }
    }

    /*───────────────────── Non‑stock items (IN203000) ────────────────*/
    public sealed class NonStockItemMaintPriceCascadeExt
        : PXGraphExtension<PX.Objects.IN.NonStockItemMaint>
    {
        public static bool IsActive() => true;

        public delegate void PersistDelegate();
        [PXOverride]
        public void Persist(PersistDelegate baseMethod)
        {
            ApplyPriceCascade();
            baseMethod();
        }

        private void ApplyPriceCascade()
        {
            var cache = Base.Caches<InventoryItemCurySettings>();

            foreach (InventoryItemCurySettings row in cache.Inserted.Cast<InventoryItemCurySettings>()
                     .Concat(cache.Updated.Cast<InventoryItemCurySettings>()))
            {
                if (row?.BasePrice != null)
                    PriceCascade.UpdateSalesPrices(Base, row.InventoryID, row.BasePrice.Value);
            }
        }
    }
}
