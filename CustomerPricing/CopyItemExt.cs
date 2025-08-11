using PX.Common;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;
using System;
using System.Collections;

namespace PX.Custom.IN
{
    [PXCacheName("Copy Item")]
    [Serializable]
    public class INCopyItemFilter : PXBqlTable, IBqlTable
    {
        #region InventoryCDNew
        public abstract class inventoryCDNew : BqlString.Field<inventoryCDNew> { }
        [PXString(60, IsUnicode = true, InputMask = "")]
        [PXUIField(DisplayName = "Item Name")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public string InventoryCDNew { get; set; }
        #endregion

        #region DescriptionNew
        public abstract class descriptionNew : BqlString.Field<descriptionNew> { }
        [PXString(256, IsUnicode = true)]
        [PXUIField(DisplayName = "Description")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public string DescriptionNew { get; set; }
        #endregion

        #region UsrIsbn13
        public abstract class usrIsbn13 : BqlString.Field<usrIsbn13> { }
        [PXString(32, IsUnicode = true)]
        [PXUIField(DisplayName = "UsrIsbn13")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public string UsrIsbn13 { get; set; }
        #endregion

        #region UsrIsbn10
        public abstract class usrIsbn10 : BqlString.Field<usrIsbn10> { }
        [PXString(32, IsUnicode = true)]
        [PXUIField(DisplayName = "UsrIsbn10")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public string UsrIsbn10 { get; set; }
        #endregion

        #region UsrCopyrightDate
        public abstract class usrCopyrightDate : BqlDateTime.Field<usrCopyrightDate> { }
        [PXDate]
        [PXUIField(DisplayName = "UsrCopyrightDate")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public DateTime? UsrCopyrightDate { get; set; }
        #endregion

        #region CopyPrices
        public abstract class copyPrices : BqlBool.Field<copyPrices> { }
        [PXBool]
        [PXUIField(DisplayName = "Copy Prices")]
        [PXUnboundDefault(false)]
        public bool? CopyPrices { get; set; }
        #endregion
    }

    public class InventoryItemMaint_CopyItemExt : PXGraphExtension<InventoryItemMaint>
    {
        public PXAction<InventoryItem> CopyItem;
        public PXFilter<INCopyItemFilter> CopyDialog;

        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Copy Item", MapEnableRights = PXCacheRights.Select, MapViewRights = PXCacheRights.Select)]
        protected virtual IEnumerable copyItem(PXAdapter adapter)
        {
            if (Base.Item.Current == null || PXLongOperation.Exists(Base))
                return adapter.Get();

            PXTrace.WriteInformation("CopyItem action started.");

            WebDialogResult result = CopyDialog.AskExt((graph, viewName) =>
            {
                var src = Base.Item.Current;
                var f = CopyDialog.Current ?? CopyDialog.Insert(new INCopyItemFilter());

                if (string.IsNullOrWhiteSpace(f.InventoryCDNew))
                    f.InventoryCDNew = (src.InventoryCD ?? string.Empty).Trim() + "-COPY";
                if (f.DescriptionNew == null)
                    f.DescriptionNew = src.Descr;

                object v;
                v = Base.Item.Cache.GetValue(src, "UsrCopyrightDate");
                if (v is DateTime dt) f.UsrCopyrightDate = dt;

                v = Base.Item.Cache.GetValue(src, "UsrIsbn13");
                if (v is string s13 && string.IsNullOrWhiteSpace(f.UsrIsbn13)) f.UsrIsbn13 = s13;

                v = Base.Item.Cache.GetValue(src, "UsrIsbn10");
                if (v is string s10 && string.IsNullOrWhiteSpace(f.UsrIsbn10)) f.UsrIsbn10 = s10;
            });

            if (result != WebDialogResult.OK)
                return adapter.Get();

            var filter = CopyDialog.Current;
            if (filter == null)
                return adapter.Get();

            if (string.IsNullOrWhiteSpace(filter.InventoryCDNew))
                throw new PXSetPropertyException<INCopyItemFilter.inventoryCDNew>("Item Name is required.");

            PXTrace.WriteInformation(
                "Copying item {0} ({1}) to new code {2}.",
                Base.Item.Current.InventoryID, Base.Item.Current.InventoryCD, filter.InventoryCDNew?.Trim());

            var cmd = new CopyParams
            {
                SrcInventoryID = Base.Item.Current.InventoryID,
                NewInventoryCD = filter.InventoryCDNew?.Trim(),
                NewDescription = filter.DescriptionNew,
                UsrIsbn13 = filter.UsrIsbn13,
                UsrIsbn10 = filter.UsrIsbn10,
                UsrCopyrightDate = filter.UsrCopyrightDate,
                CopyPrices = filter.CopyPrices == true
            };

            PXLongOperation.StartOperation(Base, () => Worker.Execute(cmd));
            return adapter.Get();
        }

        [Serializable]
        private class CopyParams
        {
            public int? SrcInventoryID { get; set; }
            public string NewInventoryCD { get; set; }
            public string NewDescription { get; set; }
            public string UsrIsbn13 { get; set; }
            public string UsrIsbn10 { get; set; }
            public DateTime? UsrCopyrightDate { get; set; }
            public bool CopyPrices { get; set; }
        }

        private static class Worker
        {
            public static void Execute(CopyParams p)
            {
                if (p?.SrcInventoryID == null)
                    throw new PXException("Source item is not specified.");

                using (var ts = new PXTransactionScope())
                {
                    var selGraph = new PXGraph();
                    var src = SelectFrom<InventoryItem>
                        .Where<InventoryItem.inventoryID.IsEqual<@P.AsInt>>
                        .View.Select(selGraph, p.SrcInventoryID).TopFirst
                        ?? throw new PXException("Source item {0} not found.", p.SrcInventoryID);

                    var newItem = CreateCopyOfItem(src, p);

                    if (newItem?.InventoryID == null || newItem.InventoryID <= 0)
                        throw new PXException("New item was not persisted. InventoryID={0}", newItem?.InventoryID);

                    PXTrace.WriteInformation("New item persisted. InventoryID={0}, InventoryCD={1}",
                        newItem.InventoryID, newItem.InventoryCD);

                    if (p.CopyPrices)
                    {
                        try
                        {
                            CopyItemBasePrices(src.InventoryID, newItem.InventoryID);

                            var ctx = PXGraph.CreateInstance<PXGraph>();
                            DateTime asOf = ctx.Accessinfo.BusinessDate ?? PXTimeZoneInfo.Today;

                            CopyActiveSalesPrices(src.InventoryID, newItem.InventoryID, asOf);
                        }
                        catch (Exception ex)
                        {
                            PXTrace.WriteError(ex);
                            throw;
                        }
                    }

                    ts.Complete();

                    var targetGraph = PXGraph.CreateInstance<InventoryItemMaint>();
                    targetGraph.Item.Current = SelectFrom<InventoryItem>
                        .Where<InventoryItem.inventoryID.IsEqual<@P.AsInt>>
                        .View.Select(targetGraph, newItem.InventoryID).TopFirst;

                    throw new PXRedirectRequiredException(targetGraph, true, "New Item");
                }
            }

            private static InventoryItem CreateCopyOfItem(InventoryItem src, CopyParams p)
            {
                var g = PXGraph.CreateInstance<InventoryItemMaint>();

                var copy = PXCache<InventoryItem>.CreateCopy(src);
                copy.InventoryID = null;
                copy.NoteID = null;
                copy.InventoryCD = p.NewInventoryCD;
                copy.Descr = p.NewDescription;

                // Insert -> update custom fields -> save
                copy = g.Item.Insert(copy);

                g.Item.Cache.SetValueExt(copy, "UsrIsbn13", p.UsrIsbn13);
                g.Item.Cache.SetValueExt(copy, "UsrIsbn10", p.UsrIsbn10);
                g.Item.Cache.SetValueExt(copy, "UsrCopyrightDate", p.UsrCopyrightDate);

                copy = g.Item.Update(copy);
                g.Actions.PressSave();

                // IMPORTANT: reselect persisted row to ensure positive key (avoid leaking temporary negative IDs)
                var persisted = g.Item.Current;
                if (persisted?.InventoryID == null || persisted.InventoryID <= 0)
                {
                    persisted = SelectFrom<InventoryItem>
                        .Where<InventoryItem.inventoryCD.IsEqual<@P.AsString>>
                        .View.Select(g, p.NewInventoryCD).TopFirst;
                }

                if (persisted?.InventoryID == null || persisted.InventoryID <= 0)
                    throw new PXException("Failed to reselect persisted item for {0}.", p.NewInventoryCD);

                return persisted;
            }

            private static void CopyItemBasePrices(int? srcInventoryID, int? dstInventoryID)
            {
                if (srcInventoryID == null || dstInventoryID == null)
                    return;
                if (dstInventoryID <= 0)
                    throw new PXException("Invalid dst InventoryID: {0}", dstInventoryID);

                var g = new PXGraph();

                var srcSettings = SelectFrom<InventoryItemCurySettings>
                    .Where<InventoryItemCurySettings.inventoryID.IsEqual<@P.AsInt>>
                    .View.Select(g, srcInventoryID);

                var cache = g.Caches<InventoryItemCurySettings>();

                foreach (InventoryItemCurySettings s in srcSettings)
                {
                    var existing = SelectFrom<InventoryItemCurySettings>
                        .Where<InventoryItemCurySettings.inventoryID.IsEqual<@P.AsInt>
                            .And<InventoryItemCurySettings.curyID.IsEqual<@P.AsString>>>
                        .View.Select(g, dstInventoryID, s.CuryID)
                        .TopFirst;

                    InventoryItemCurySettings clone = existing ?? new InventoryItemCurySettings
                    {
                        InventoryID = dstInventoryID,
                        CuryID = s.CuryID
                    };

                    clone.BasePrice = s.BasePrice;
                    clone.RecPrice = s.RecPrice;

                    if (existing == null)
                        cache.Insert(clone);
                    else
                        cache.Update(clone);
                }

                g.Persist();
            }

            private static void CopyActiveSalesPrices(int? srcInventoryID, int? dstInventoryID, DateTime asOfDate)
            {
                if (srcInventoryID == null || dstInventoryID == null)
                    return;
                if (dstInventoryID <= 0)
                    throw new PXException("Invalid dst InventoryID: {0}", dstInventoryID);

                var g = new PXGraph();

                var prices = SelectFrom<ARSalesPrice>
                    .Where<
                        ARSalesPrice.inventoryID.IsEqual<@P.AsInt>
                        .And<ARSalesPrice.effectiveDate.IsLessEqual<@P.AsDateTime>>
                        .And<
                            Brackets<
                                ARSalesPrice.expirationDate.IsNull
                                .Or<ARSalesPrice.expirationDate.IsGreaterEqual<@P.AsDateTime>>
                            >
                        >
                    >
                    .View.Select(g, srcInventoryID, asOfDate, asOfDate);

                var cache = g.Caches<ARSalesPrice>();

                foreach (ARSalesPrice p in prices)
                {
                    var np = new ARSalesPrice
                    {
                        PriceType = p.PriceType,
                        PriceCode = p.PriceCode,
                        InventoryID = dstInventoryID,
                        UOM = p.UOM,
                        BreakQty = p.BreakQty,
                        CuryID = p.CuryID,
                        SalesPrice = p.SalesPrice,
                        EffectiveDate = p.EffectiveDate,
                        ExpirationDate = p.ExpirationDate,
                        IsPromotionalPrice = p.IsPromotionalPrice,
                        TaxCategoryID = p.TaxCategoryID,
                    };

                    cache.Insert(np);
                }

                g.Persist();
            }
        }
    }
}
