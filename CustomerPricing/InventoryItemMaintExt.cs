using System;
using System.Linq;
using PX.Common;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;

namespace CustomerPricing
{
    /*───────────────────── Price cascade helper ─────────────────────*/
    public static class PriceCascade
    {
        internal const string SkipCascadeSlot = "CustomerPricing.PriceCascade.Skip";
        public static bool IsSuppressed => PXContext.GetSlot<string>(SkipCascadeSlot) != null;
        public static IDisposable Suppress() => new SlotScope(SkipCascadeSlot, "1");

        private sealed class SlotScope : IDisposable
        {
            private readonly string _key;
            private readonly string _prev;
            public SlotScope(string key, string value)
            {
                _key = key;
                _prev = PXContext.GetSlot<string>(key);
                PXContext.SetSlot(key, value);
            }
            public void Dispose() => PXContext.SetSlot(_key, _prev);
        }

        private sealed class PriceTypeBase : BqlString.Constant<PriceTypeBase>
        { public PriceTypeBase() : base("B") { } }

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

                decimal newPrice = Math.Round(basePrice * (1 - pct.Value / 100m), 4, MidpointRounding.AwayFromZero);

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
            if (!PriceCascade.IsSuppressed)
                ApplyPriceCascade();
            baseMethod();
        }

        private void ApplyPriceCascade()
        {
            var cache = Base.Caches<InventoryItemCurySettings>();

            foreach (InventoryItemCurySettings row in cache.Updated.Cast<InventoryItemCurySettings>())
            {
                var oldBase = (decimal?)cache.GetValueOriginal<InventoryItemCurySettings.basePrice>(row);
                if (row?.BasePrice == null || oldBase == null || row.BasePrice == oldBase || row.BasePrice <= 0)
                    continue;

                PXTrace.WriteInformation("Cascade: InventoryID={0} Cury={1} BasePrice changed {2} -> {3}",
                    row.InventoryID, row.CuryID, oldBase, row.BasePrice);

                PriceCascade.UpdateSalesPrices(Base, row.InventoryID, row.BasePrice.Value);
            }
        }
    }

    /*───────────────────── Non-stock items (IN203000) ────────────────*/
    public sealed class NonStockItemMaintPriceCascadeExt
        : PXGraphExtension<PX.Objects.IN.NonStockItemMaint>
    {
        public static bool IsActive() => true;

        public delegate void PersistDelegate();
        [PXOverride]
        public void Persist(PersistDelegate baseMethod)
        {
            if (!PriceCascade.IsSuppressed)
                ApplyPriceCascade();
            baseMethod();
        }

        private void ApplyPriceCascade()
        {
            var cache = Base.Caches<InventoryItemCurySettings>();

            foreach (InventoryItemCurySettings row in cache.Updated.Cast<InventoryItemCurySettings>())
            {
                var oldBase = (decimal?)cache.GetValueOriginal<InventoryItemCurySettings.basePrice>(row);
                if (row?.BasePrice == null || oldBase == null || row.BasePrice == oldBase)
                    continue;

                PXTrace.WriteInformation("Cascade(NS): InventoryID={0} Cury={1} BasePrice changed {2} -> {3}",
                    row.InventoryID, row.CuryID, oldBase, row.BasePrice);

                PriceCascade.UpdateSalesPrices(Base, row.InventoryID, row.BasePrice.Value);
            }
        }
    }
}
