using CustomerPricing;
using PX.Common;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.CM;
using PX.Objects.IN;
using PX.Objects.SO;
using System;

namespace PX.Objects.SO.Extensions
{
    /*───────────────────────────────────────────────────────────────────────────────
     *  PURPOSE
     *  -------
     *  1.  When a discount (manual or automatic) is applied to SOLine, force the
     *      UnitPrice to the item’s BasePrice and keep DiscPct / DiscAmt in sync.
     *  2.  When the user clears the discount by setting DiscPct OR DiscAmt to 0,
     *      automatically zero the companion field, clear ManualPrice, and revert
     *      UnitPrice to the default list / customer price.
     *  3.  Re-price every non-manual SOLine when the order’s UsrOrderPriceClass
     *      changes so that FindSalesPrice runs with the new class.
     *  4.  PXTrace diagnostics at every decision point.
     *  
     *  Compatible with C# 7.3 – no pattern-matching syntax is used.
     *────────────────────────────────────────────────────────────────────────────*/
    public sealed class SOOrderEntrySalePriceResetExt : PXGraphExtension<SOOrderEntry>
    {
        private bool _internalUpdate;
        public static bool IsActive() => true;

        #region Event wiring ----------------------------------------------------

        protected void _(Events.FieldUpdated<SOLine.discPct> e) =>
            Process(e.Row as SOLine, e.Cache, nameof(SOLine.discPct));

        protected void _(Events.FieldUpdated<SOLine.curyDiscAmt> e) =>
            Process(e.Row as SOLine, e.Cache, nameof(SOLine.curyDiscAmt));

        protected void _(Events.FieldUpdated<SOLine.discountID> e) =>
            Process(e.Row as SOLine, e.Cache, nameof(SOLine.discountID));

        protected void _(Events.FieldUpdated<SOLine.manualDisc> e) =>
            Process(e.Row as SOLine, e.Cache, nameof(SOLine.manualDisc));

        protected void _(Events.RowInserted<SOLine> e) =>
            Process(e.Row as SOLine, e.Cache, "RowInserted");

        /// <summary>
        /// Re-price all non-manual lines when the user changes UsrOrderPriceClass
        /// on the SOOrder header.
        /// </summary>
        protected void _(Events.FieldUpdated<SOOrderExt.usrOrderPriceClass> e)
        {
            if (_internalUpdate || e.Row == null) return;

            try
            {
                _internalUpdate = true;

                foreach (SOLine line in Base.Transactions.Select())
                {
                    if (line.InventoryID == null || line.ManualPrice == true)
                        continue;

                    PXCache lineCache = Base.Transactions.Cache;

                    lineCache.SetDefaultExt<SOLine.curyUnitPrice>(line);
                    lineCache.SetDefaultExt<SOLine.discPct>(line);
                    lineCache.SetDefaultExt<SOLine.curyDiscAmt>(line);

                    lineCache.Update(line);
                }
            }
            finally
            {
                _internalUpdate = false;
            }
        }

        #endregion

        /*───────────────────────────────────────────────────────────────────────*/
        private void Process(SOLine line, PXCache cache, string trigger)
        {
            if (_internalUpdate || line == null || line.IsFree == true)
                return;

            PXTrace.WriteInformation(
                $"[{trigger}] ENTER InvID={line.InventoryID}, UnitPrice={line.CuryUnitPrice}, " +
                $"DiscPct={line.DiscPct}, DiscAmt={line.CuryDiscAmt}, ManualPrice={line.ManualPrice}");
            if (line.InventoryID == null)
            {
                PXTrace.WriteInformation($"[{trigger}] InventoryID is null, skipping discount logic.");
                return;
            }

            /*----- 0. Did the user explicitly clear one of the discount fields? --------*/
            bool triggerCleared =
                   (trigger == nameof(SOLine.discPct) && (line.DiscPct ?? 0m) == 0m)
                || (trigger == nameof(SOLine.curyDiscAmt) && (line.CuryDiscAmt ?? 0m) == 0m);

            if (triggerCleared)
            {
                HandleDiscountRemoval(line, cache, trigger);
                return;
            }

            /*----- 1. Discount still active -------------------------------------------*/
            if (HasActiveDiscount(line))
                ApplyDiscountLogic(line, cache, trigger);
        }

        /*───────────────────────────────────────────────────────────────────────*/
        private void HandleDiscountRemoval(SOLine line, PXCache cache, string trigger)
        {
            try
            {
                _internalUpdate = true;

                /* Clear the companion discount field so BOTH are zero */
                if (trigger == nameof(SOLine.discPct) && (line.CuryDiscAmt ?? 0m) != 0m)
                {
                    cache.SetValueExt<SOLine.curyDiscAmt>(line, 0m);
                    PXTrace.WriteInformation($"[{trigger}] Companion DiscAmt → 0");
                }
                else if (trigger == nameof(SOLine.curyDiscAmt) && (line.DiscPct ?? 0m) != 0m)
                {
                    cache.SetValueExt<SOLine.discPct>(line, 0m);
                    PXTrace.WriteInformation($"[{trigger}] Companion DiscPct → 0");
                }

                /* Remove ManualPrice flag so price re-defaults */
                if (line.ManualPrice == true)
                    cache.SetValueExt<SOLine.manualPrice>(line, false);

                /* Re-default UnitPrice via field defaulting */
                object listPriceObj = null;
                cache.RaiseFieldDefaulting<SOLine.curyUnitPrice>(line, out listPriceObj);
                if (listPriceObj is decimal listPrice)
                    cache.SetValueExt<SOLine.curyUnitPrice>(line, listPrice);

                PXTrace.WriteInformation(
                    $"[{trigger}] Discount cleared → UnitPrice reset to {line.CuryUnitPrice}, ManualPrice={line.ManualPrice}");
            }
            finally
            {
                _internalUpdate = false;
            }
        }

        /*───────────────────────────────────────────────────────────────────────*/
        private void ApplyDiscountLogic(SOLine line, PXCache cache, string trigger)
        {
            decimal? basePrice =
                SelectFrom<InventoryItemCurySettings>
                    .Where<InventoryItemCurySettings.inventoryID.IsEqual<P.AsInt>>
                    .View.Select(Base, line.InventoryID)
                    .TopFirst?
                    .BasePrice;

            /*----- 2. BasePrice not found, cannot apply discount logic ---------*/
            if (!basePrice.HasValue)
            {
                PXTrace.WriteWarning($"[{trigger}] BasePrice not found for InventoryID={line.InventoryID}");
                return;
            }

            try
            {
                _internalUpdate = true;

                /* 1) Force BasePrice into UnitPrice */
                if (line.CuryUnitPrice != basePrice.Value)
                {
                    if (line.ManualPrice != true)
                        cache.SetValueExt<SOLine.manualPrice>(line, true);

                    cache.SetValueExt<SOLine.curyUnitPrice>(line, basePrice.Value);
                    PXTrace.WriteInformation($"[{trigger}] UnitPrice forced to BasePrice {basePrice.Value}");
                }

                /* 2) Determine which field the user just edited ----------------*/
                bool pctBased = trigger == nameof(SOLine.discPct);
                bool amtBased = trigger == nameof(SOLine.curyDiscAmt);

                if (!pctBased && !amtBased)
                {
                    pctBased = (line.DiscPct ?? 0m) != 0m;
                    amtBased = (line.CuryDiscAmt ?? 0m) != 0m && !pctBased;
                }

                decimal qty = line.OrderQty ?? 0m;
                decimal lineAmount = basePrice.Value * qty;

                if (pctBased)
                {
                    decimal newAmt = PXCurrencyAttribute.RoundCury(
                        cache, line, lineAmount * line.DiscPct.Value / 100m);

                    if (newAmt != (line.CuryDiscAmt ?? 0m))
                    {
                        cache.SetValueExt<SOLine.curyDiscAmt>(line, newAmt);
                        PXTrace.WriteInformation($"[{trigger}] pctBased → DiscAmt recalculated = {newAmt}");
                    }
                }
                else if (amtBased && lineAmount != 0m)
                {
                    decimal newPct = Math.Round(line.CuryDiscAmt.Value * 100m / lineAmount,
                        6, MidpointRounding.AwayFromZero);

                    if (newPct != (line.DiscPct ?? 0m))
                    {
                        cache.SetValueExt<SOLine.discPct>(line, newPct);
                        PXTrace.WriteInformation($"[{trigger}] amtBased → DiscPct recalculated = {newPct}");
                    }
                }
            }
            finally
            {
                _internalUpdate = false;
            }

            PXTrace.WriteInformation(
                $"[{trigger}] EXIT InvID={line.InventoryID}, UnitPrice={line.CuryUnitPrice}, " +
                $"DiscPct={line.DiscPct}, DiscAmt={line.CuryDiscAmt}");
        }

        /*───────────────────────────────────────────────────────────────────────*/
        private static bool HasActiveDiscount(SOLine l)
        {
            return l != null
                   && ((l.DiscPct ?? 0m) != 0m
                    || (l.CuryDiscAmt ?? 0m) != 0m);
        }
    }
}
