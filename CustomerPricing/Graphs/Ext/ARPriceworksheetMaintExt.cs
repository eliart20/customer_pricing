using System;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;

namespace CustomerPricing
{
    /// <summary>
    /// • Keeps <see cref="ARPriceWorksheetDetail.PendingPrice"/> in sync with
    ///   <see cref="ARPriceWorkSheetDetailExt.UsrPricePercentOff"/> on the worksheet line.
    /// • Propagates the percent-off value to <see cref="ARSalesPriceExt.UsrPricePercentOff"/>
    ///   when the worksheet is released.
    /// • During “Copy Prices”:
    ///   - Copies <c>UsrPricePercentOff</c> from the source <see cref="ARSalesPrice"/> into
    ///     both <c>UsrSourcePercentOff</c> **and** <c>UsrPricePercentOff</c> on the new
    ///     worksheet detail.
    ///   - If the source percent-off is absent, sets <c>PendingPrice = CurrentPrice</c>.
    /// </summary>
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod – always active.
    public sealed class ARPriceWorksheetMaintExt : PXGraphExtension<ARPriceWorksheetMaint>
    {
        /*=====================================================================*/
        /* 1 ▪ FIELD-UPDATED – recalc pending price                            */
        /*=====================================================================*/

        protected void _(Events.FieldUpdated<
            ARPriceWorksheetDetail,
            ARPriceWorkSheetDetailExt.usrPricePercentOff> e)
        {
            ARPriceWorksheetDetail row = e.Row;
            if (row?.InventoryID == null) return;

            decimal? pct = row.GetExtension<ARPriceWorkSheetDetailExt>()?.UsrPricePercentOff;
            if (pct == null) return;

            decimal? basePrice =
                SelectFrom<InventoryItemCurySettings>
                    .Where<InventoryItemCurySettings.inventoryID.IsEqual<P.AsInt>>
                    .View
                    .Select(Base, row.InventoryID)
                    .TopFirst?
                    .BasePrice;

            if (basePrice == null) return;

            decimal newPrice = basePrice.Value * (1m - pct.Value / 100m);
            e.Cache.SetValueExt<ARPriceWorksheetDetail.pendingPrice>(row, newPrice);
        }

        /*=====================================================================*/
        /* 2 ▪ COPY HELPERS – SalesPrice ⇆ Worksheet                           */
        /*=====================================================================*/

        private static void CopyPercentOffToNew(ARPriceWorksheetDetail src, ARSalesPrice dest)
        {
            decimal? pct = src.GetExtension<ARPriceWorkSheetDetailExt>()?.UsrPricePercentOff;
            if (pct == null) return;

            PXCache<ARSalesPrice>
                .GetExtension<ARSalesPriceExt>(dest)
                .UsrPricePercentOff = pct;
        }

        private void CopyPercentOffToExisting(ARPriceWorksheetDetail src, ARSalesPrice dest)
        {
            decimal? pct = src.GetExtension<ARPriceWorkSheetDetailExt>()?.UsrPricePercentOff;
            if (pct == null) return;

            Base.Caches<ARSalesPrice>()
                .SetValueExt<ARSalesPriceExt.usrPricePercentOff>(dest, pct);
        }

        /*=====================================================================*/
        /* 3 ▪ NEW ARSalesPrice ROWS                                           */
        /*=====================================================================*/

        [PXOverride]
        public ARSalesPrice CreateSalesPrice(
            ARPriceWorksheetDetail priceLine,
            bool? isPromotional,
            bool? isFairValue,
            bool? isProrated,
            DateTime? effectiveDate,
            DateTime? expirationDate,
            string taxCalcMode,
            Func<ARPriceWorksheetDetail, bool?, bool?, bool?, DateTime?, DateTime?, string, ARSalesPrice> baseMethod)
        {
            ARSalesPrice sp = baseMethod(priceLine, isPromotional, isFairValue, isProrated,
                                         effectiveDate, expirationDate, taxCalcMode);

            CopyPercentOffToNew(priceLine, sp);
            return sp;
        }

        /*=====================================================================*/
        /* 4 ▪ EXISTING ARSalesPrice ROWS                                      */
        /*=====================================================================*/

        [PXOverride]
        public void UpdateSalesPriceFromPriceLine(
            ARSalesPrice salesPrice,
            ARPriceWorksheetDetail priceLine,
            Action<ARSalesPrice, ARPriceWorksheetDetail> baseMethod)
        {
            baseMethod(salesPrice, priceLine);
            CopyPercentOffToExisting(priceLine, salesPrice);
        }

        /*=====================================================================*/
        /* 5 ▪ WORKSHEET LINE FROM SALES PRICE (COPY PRICES)                   */
        /*=====================================================================*/

        public delegate ARPriceWorksheetDetail
            CreateWorksheetDetailFromSalesPriceOnCopyingDelegate(
                ARSalesPrice salesPrice,
                CopyPricesFilter copyFilter,
                string destinationPriceCode);

        [PXOverride]
        public ARPriceWorksheetDetail CreateWorksheetDetailFromSalesPriceOnCopying(
            ARSalesPrice salesPrice,
            CopyPricesFilter copyFilter,
            string destinationPriceCode,
            CreateWorksheetDetailFromSalesPriceOnCopyingDelegate baseMethod)
        {
            ARPriceWorksheetDetail detail =
                baseMethod(salesPrice, copyFilter, destinationPriceCode);

            if (detail == null)
                return null;

            ARPriceWorkSheetDetailExt dtExt =
                PXCache<ARPriceWorksheetDetail>.GetExtension<ARPriceWorkSheetDetailExt>(detail);
            ARSalesPriceExt spExt =
                PXCache<ARSalesPrice>.GetExtension<ARSalesPriceExt>(salesPrice);

            /*----------------------------------------------------------
             * Populate both custom fields on the new worksheet line.
             *---------------------------------------------------------*/
            dtExt.UsrSourcePercentOff = spExt?.UsrPricePercentOff;
            dtExt.UsrPricePercentOff = dtExt.UsrSourcePercentOff;

            /*----------------------------------------------------------
             * If no percent-off is available, keep the price unchanged
             * by copying CurrentPrice → PendingPrice.
             *---------------------------------------------------------*/
            if (dtExt.UsrSourcePercentOff == null)
                detail.PendingPrice = detail.CurrentPrice;

            /*  No explicit cache.Update – insertion handled by core. */
            return detail;
        }
    }
}
