using System;
using System.Collections.Generic;
using PX.Data;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;
using PX.Objects.SO;

namespace CustomerPricing
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod – extension is always active
    public sealed class ARSalesPriceMaintExt : PXGraphExtension<ARSalesPriceMaint>
    {
        /// <summary>
        /// Re‑calculates <see cref="ARSalesPrice.SalesPrice"/> after <c>UsrPricePercentOff</c> changes.
        /// </summary>
        protected void _(Events.FieldUpdated<ARSalesPriceExt.usrPricePercentOff> e)
        {
            var row = (ARSalesPrice)e.Row;
            if (row?.InventoryID == null) return;

            decimal? pct = row.GetExtension<ARSalesPriceExt>()?.UsrPricePercentOff;
            if (pct == null) return;

            decimal? basePrice =
                SelectFrom<InventoryItemCurySettings>
                    .Where<InventoryItemCurySettings.inventoryID
                        .IsEqual<PX.Data.BQL.P.AsInt>>
                    .View.Select(Base, row.InventoryID)
                    .TopFirst?.BasePrice;

            if (basePrice == null) return;

            decimal newPrice = basePrice.Value * (1m - pct.Value / 100m);
            e.Cache.SetValueExt<ARSalesPrice.salesPrice>(row, newPrice);
        }

        #region Order‑price‑class aware price search
        public delegate ARSalesPriceMaint.SalesPriceItem FindSalesPriceDelegate(
            PXCache sender, string custPriceClass, int? customerID, int? inventoryID,
            string lotSerialNbr, int? siteID, string baseCuryID, string curyID,
            decimal? quantity, string UOM, DateTime date, bool isFairValue, string taxCalcMode);

        [PXOverride]
        public ARSalesPriceMaint.SalesPriceItem FindSalesPrice(
            PXCache sender, string custPriceClass, int? customerID, int? inventoryID,
            string lotSerialNbr, int? siteID, string baseCuryID, string curyID,
            decimal? quantity, string UOM, DateTime date, bool isFairValue,
            string taxCalcMode, FindSalesPriceDelegate baseMethod)
        {
            // ------------------------------------------------------------------
            // 1. Determine effective starting classes: Order → Customer (distinct)
            // ------------------------------------------------------------------
            string orderPriceClass = TryGetOrderPriceClass(sender);
            string[] startClasses = string.IsNullOrWhiteSpace(orderPriceClass) ||
                                    string.Equals(orderPriceClass, custPriceClass, StringComparison.OrdinalIgnoreCase)
                ? new[] { custPriceClass }                                    // only customer class
                : new[] { orderPriceClass, custPriceClass };                  // search order class first, then customer

            // ------------------------------------------------------------------
            // 2. Traverse each chain upward (child → parents) until a “P/C” price
            // ------------------------------------------------------------------
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ARSalesPriceMaint.SalesPriceItem fallback = null;

            foreach (string rootClass in startClasses)
            {
                string nextClass = rootClass;
                while (!string.IsNullOrWhiteSpace(nextClass) && visited.Add(nextClass))
                {
                    var candidate = baseMethod(sender, nextClass, customerID, inventoryID, lotSerialNbr,
                                               siteID, baseCuryID, curyID, quantity, UOM, date,
                                               isFairValue, taxCalcMode);

                    // Accept class/customer specific prices only (“P” / “C”)
                    if (candidate != null && (candidate.PriceType == "P" || candidate.PriceType == "C"))
                    {
                        // Replace the problematic line with the following code  
                        return candidate;
                    }

                    if (fallback == null && candidate != null)                 // remember first base/default price
                        fallback = candidate;

                    // Climb to parent class.
                    var pc = ARPriceClass.PK.Find(Base, nextClass);
                    nextClass = pc?.GetExtension<ARPriceClassExt>()?.ParentPriceClassID;
                }
            }

            return fallback;                                                   // base price or null
        }

        /// <summary>
        /// Fetches <c>UsrOrderPriceClass</c> from the current <see cref="SOOrder"/>
        /// when the calling graph is <see cref="SOOrderEntry"/> (or hosts an SOOrder cache).
        /// Returns <c>null</c> if unavailable.
        /// </summary>
        private static string TryGetOrderPriceClass(PXCache sender)
        {
            // Direct graph cast (standard SOOrderEntry).
            if (sender.Graph is SOOrderEntry soGraph &&
                soGraph.Document.Current != null)
            {
                return soGraph.Document.Current.GetExtension<SOOrderExt>()?.UsrOrderPriceClass;
            }

            // Fallback: attempt to read current SOOrder from any cache in the graph.
            if (sender.Graph.Caches<SOOrder>()?.Current is SOOrder order)
            {
                return order.GetExtension<SOOrderExt>()?.UsrOrderPriceClass;
            }

            return null;
        }
        #endregion
    }
}
