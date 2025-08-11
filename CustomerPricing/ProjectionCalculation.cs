using CustomerPricing;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Data.Description.GI;
using PX.Objects.AR;
using PX.Objects.CM;
using PX.Objects.IN;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
// allow @P placeholder in BQL
//using static PX.Data.BQL.BqlPlaceholder;

namespace CustomerPricing
    {
    /* ============================================================ *
	 *  FILTER DAC (top‑level class to avoid IBqlTableSys errors)   *
	 * ============================================================ */
    [Serializable]
    [PXCacheName("Price Filter")]
    public class PriceFilter : PXBqlTable, IBqlTable 
    {
        #region AsOfDate
        public abstract class asOfDate : BqlDateTime.Field<asOfDate> { }
        [PXDate]
        [PXDefault(typeof(AccessInfo.businessDate))]
        [PXUIField(DisplayName = "As‑Of Date")]
        public DateTime? AsOfDate { get; set; }
        #endregion

        #region CustPriceClassID
        public abstract class custPriceClassID : BqlString.Field<custPriceClassID> { }
        [PXString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Customer Price Class")]
        [PXSelector(typeof(Search<ARPriceClass.priceClassID>))]
        public string CustPriceClassID { get; set; }
        #endregion
    }

    /* ============================================================ *
	 *  RESULT DAC (virtual, non‑persisted)                         *
	 * ============================================================ */
    [Serializable]
    [PXVirtual]
    [PXCacheName("Resolved Price")]
    public class PriceResult : PXBqlTable, IBqlTable
    {
        #region InventoryID
        public abstract class inventoryID : BqlInt.Field<inventoryID> { }
        [Inventory(DisplayName = "Inventory ID")]
        public int? InventoryID { get; set; }
        #endregion

        #region RequestedPriceClassID
        public abstract class requestedPriceClassID : BqlString.Field<requestedPriceClassID> { }
        [PXString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Requested Class")]
        public string RequestedPriceClassID { get; set; }
        #endregion

        #region MatchedPriceClassID
        public abstract class matchedPriceClassID : BqlString.Field<matchedPriceClassID> { }
        [PXString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Matched Class/BAS")]
        public string MatchedPriceClassID { get; set; }
        #endregion

        #region UOM
        public abstract class uOM : BqlString.Field<uOM> { }
        [PXString(6, IsUnicode = false)]
        [PXUIField(DisplayName = "UOM")]
        public string UOM { get; set; }
        #endregion

        #region CuryID
        public abstract class curyID : BqlString.Field<curyID> { }
        [PXString(5, IsUnicode = false)]
        [PXUIField(DisplayName = "Currency")]
        public string CuryID { get; set; }
        #endregion

        #region BreakQty
        public abstract class breakQty : BqlDecimal.Field<breakQty> { }
        [PXQuantity]
        [PXUIField(DisplayName = "Break Qty")]
        public decimal? BreakQty { get; set; }
        #endregion

        #region SalesPrice
        public abstract class salesPrice : BqlDecimal.Field<salesPrice> { }
        [PXPriceCost]
        [PXUIField(DisplayName = "Sale Price")]
        public decimal? SalesPrice { get; set; }
        #endregion
    }

    /* ============================================================ *
	 *  GRAPH                                                        *
	 * ============================================================ */
    public class PriceHierarchyMaint : PXGraph<PriceHierarchyMaint>
    {
        /* ---- constants for ARSalesPrice.priceType ---------------- */
        private const string PriceTypeBase = "B";   // BASE
        private const string PriceTypePriceClass = "P";   // Price Class

        /* ---- views ---------------------------------------------- */
        public PXCancel<PriceFilter> Cancel;
        public PXFilter<PriceFilter> Filter;
        public PXFilteredProcessing<PriceResult, PriceFilter> Prices;

        /* ======================================================== *
		 *  Prices delegate – resolves hierarchy + BASE fallback    *
		 * ======================================================== */
        protected IEnumerable prices()
        {
            PriceFilter f = Filter.Current;
            if (f?.AsOfDate == null)
                yield break;

            DateTime asOf = f.AsOfDate.Value;
            string reqCls = f.CustPriceClassID?.Trim();

            /* 1) Build price‑class chain (requested → root) */
            List<string> chain = BuildChain(reqCls);  // empty list when reqCls = null

            /* 2) Iterate every stock item */
            foreach (InventoryItem item in
                     PXSelect<InventoryItem,
                              Where<InventoryItem.stkItem, Equal<True>>>.Select(this))
            {
                var candidates = new List<ARSalesPrice>();

                /* 2a) ‘P’ rows for every class in the chain */
                foreach (string cls in chain)
                {
                    foreach (ARSalesPrice sp in
                             SelectFrom<ARSalesPrice>
                             .Where<ARSalesPrice.inventoryID.IsEqual<P.AsInt>
                                .And<ARSalesPrice.priceType.IsEqual<P.AsString>>
                                .And<ARSalesPrice.custPriceClassID.IsEqual<P.AsString>>
                                .And<ARSalesPrice.effectiveDate.IsLessEqual<P.AsDateTime>>
                                .And<Where<ARSalesPrice.expirationDate.IsNull.
                                       Or<ARSalesPrice.expirationDate.IsGreaterEqual<P.AsDateTime>>>>>
                             .View.Select(this, item.InventoryID, PriceTypePriceClass, cls, asOf, asOf))
                    {
                        candidates.Add(sp);
                    }
                }

                /* 2b) explicit BASE rows */
                foreach (ARSalesPrice sp in
                         SelectFrom<ARSalesPrice>
                         .Where<ARSalesPrice.inventoryID.IsEqual<P.AsInt>
                            .And<ARSalesPrice.priceType.IsEqual<P.AsString>>
                            .And<ARSalesPrice.effectiveDate.IsLessEqual<P.AsDateTime>>
                            .And<Where<ARSalesPrice.expirationDate.IsNull.
                                   Or<ARSalesPrice.expirationDate.IsGreaterEqual<P.AsDateTime>>>>>
                         .View.Select(this, item.InventoryID, PriceTypeBase, asOf, asOf))
                {
                    candidates.Add(sp);
                }

                /* 2c) BasePrice fallback from InventoryItemCurySettings when no explicit BASE */
                bool needBaseFallback = !candidates.Any(p => p.PriceType == PriceTypeBase);
                if (needBaseFallback)
                {
                    foreach (InventoryItemCurySettings cur in
                             PXSelect<InventoryItemCurySettings,
                                      Where<InventoryItemCurySettings.inventoryID, Equal<Required<InventoryItemCurySettings.inventoryID>>>>.
                                      Select(this, item.InventoryID))
                    {
                        if (cur.BasePrice == null)
                            continue;

                        candidates.Add(new ARSalesPrice
                        {
                            InventoryID = item.InventoryID,
                            PriceType = PriceTypeBase,
                            UOM = item.BaseUnit,
                            CuryID = cur.CuryID,
                            BreakQty = 0m,
                            SalesPrice = cur.BasePrice
                        });
                    }
                }

                if (candidates.Count == 0)
                    continue; // no price at all – skip

                /* 3) Pick cheapest class row; if none, BASE row */
                var resolved = candidates
                    .GroupBy(p => new { p.UOM, p.CuryID, p.BreakQty })
                    .Select(g =>
                    {
                        ARSalesPrice win = g.OrderBy(p =>
                        {
                            if (p.PriceType == PriceTypeBase)
                                return int.MaxValue;           // sort BASE after any class row
                            int depth = chain.IndexOf(p.CustPriceClassID);
                            return depth < 0 ? int.MaxValue - 1 : depth; // deeper = larger #
                        })
                            .ThenBy(p => p.SalesPrice ?? decimal.MaxValue)
                            .First();

                        return new PriceResult
                        {
                            InventoryID = win.InventoryID,
                            RequestedPriceClassID = reqCls,
                            MatchedPriceClassID = win.PriceType == PriceTypeBase ? "BASE" : win.CustPriceClassID,
                            UOM = win.UOM,
                            CuryID = win.CuryID,
                            BreakQty = win.BreakQty,
                            SalesPrice = win.SalesPrice
                        };
                    });

                foreach (PriceResult r in resolved)
                    yield return r;
            }
        }

        /* ======================================================== *
		 *  Helper – walk ARPriceClassExt.ParentPriceClassID chain  *
		 * ======================================================== */
        private List<string> BuildChain(string leaf)
        {
            var chain = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string current = leaf;
            while (!string.IsNullOrEmpty(current) && visited.Add(current))
            {
                chain.Add(current);

                ARPriceClass parentRow =
                    PXSelect<ARPriceClass,
                             Where<ARPriceClass.priceClassID, Equal<Required<ARPriceClass.priceClassID>>>>.
                             Select(this, current);
                if (parentRow == null)
                    break;

                ARPriceClassExt ext = parentRow.GetExtension<ARPriceClassExt>();
                current = ext?.ParentPriceClassID;
            }
            return chain;
        }
    }
}
