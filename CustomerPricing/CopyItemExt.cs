// PX.Custom.IN/InventoryItemMaint_CopyItemExt.cs
#pragma warning disable PX1050
#pragma warning disable PX1016
#pragma warning disable PX1072
using PX.Common;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.IN;
using System;
using System.Collections;
using System.Linq;
using CustomerPricing; // for PriceCascade.Suppress()

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
        private const string SkipSetDefaultSiteSlot = "PX.Custom.IN.CopyItem.SkipSetDefaultSite";

        public PXAction<InventoryItem> CopyItem;
        public PXFilter<INCopyItemFilter> CopyDialog;

        // Prevent default-site logic on temporary rows
        public delegate void SetDefaultSiteIDDelegate(InventoryItem row, bool allCurrencies);
        [PXOverride]
        public virtual void SetDefaultSiteID(InventoryItem row, bool allCurrencies, SetDefaultSiteIDDelegate baseMethod)
        {
            bool suppress = (row?.InventoryID ?? 0) < 0 || PXContext.GetSlot<string>(SkipSetDefaultSiteSlot) != null;
            if (!suppress) baseMethod(row, allCurrencies);
        }

        protected void _(Events.FieldDefaulting<InventoryItemCurySettings, InventoryItemCurySettings.dfltSiteID> e)
        {
            if (e.Row?.InventoryID < 0)
            {
                e.NewValue = null;
                e.Cancel = true;
            }
        }
        protected void _(Events.FieldVerifying<InventoryItemCurySettings, InventoryItemCurySettings.dfltSiteID> e)
        {
            if (e.Row?.InventoryID < 0)
            {
                e.NewValue = null;
                e.Cancel = true;
            }
        }
        protected void _(Events.RowInserted<InventoryItemCurySettings> e)
        {
            if (e.Row?.InventoryID < 0 && e.Row.DfltSiteID != null)
                e.Cache.SetValue<InventoryItemCurySettings.dfltSiteID>(e.Row, null);
        }

        [PXButton(CommitChanges = true)]
        [PXUIField(DisplayName = "Copy Item", MapEnableRights = PXCacheRights.Select, MapViewRights = PXCacheRights.Select)]
        protected virtual IEnumerable copyItem(PXAdapter adapter)
        {
            if (Base.Item.Current == null || PXLongOperation.Exists(Base))
                return adapter.Get();

            var result = CopyDialog.AskExt((_, __) =>
            {
                var src = Base.Item.Current;
                var f = CopyDialog.Current ?? CopyDialog.Insert(new INCopyItemFilter());
                if (string.IsNullOrWhiteSpace(f.InventoryCDNew))
                    f.InventoryCDNew = (src.InventoryCD ?? string.Empty).Trim() + "-COPY";
                if (f.DescriptionNew == null)
                    f.DescriptionNew = src.Descr;

                object v = Base.Item.Cache.GetValue(src, "UsrCopyrightDate");
                if (v is DateTime dt) f.UsrCopyrightDate = dt;

                v = Base.Item.Cache.GetValue(src, "UsrIsbn13");
                if (v is string s13 && string.IsNullOrWhiteSpace(f.UsrIsbn13)) f.UsrIsbn13 = s13;

                v = Base.Item.Cache.GetValue(src, "UsrIsbn10");
                if (v is string s10 && string.IsNullOrWhiteSpace(f.UsrIsbn10)) f.UsrIsbn10 = s10;
            });

            if (result != WebDialogResult.OK)
                return adapter.Get();

            var filter = CopyDialog.Current;
            if (filter == null || string.IsNullOrWhiteSpace(filter.InventoryCDNew))
                throw new PXSetPropertyException<INCopyItemFilter.inventoryCDNew>("Item Name is required.");

            var cmd = new CopyParams
            {
                SrcInventoryID = Base.Item.Current.InventoryID,
                NewInventoryCD = filter.InventoryCDNew.Trim(),
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
                    throw new PXException("Source item not found.");

                InventoryItemMaint graph = PXGraph.CreateInstance<InventoryItemMaint>();
                DateTime bizDate = graph.Accessinfo.BusinessDate ?? PXTimeZoneInfo.Now;

                using (var ts = new PXTransactionScope())
                {
                    PXContext.SetSlot<string>(SkipSetDefaultSiteSlot, "1");
                    try
                    {
                        InventoryItem src =
                            SelectFrom<InventoryItem>
                            .Where<InventoryItem.inventoryID.IsEqual<@P.AsInt>>
                            .View.Select(graph, p.SrcInventoryID)
                            .TopFirst
                            ?? throw new PXException("Source item was deleted.");

                        // Clone
                        InventoryItem newItem = (InventoryItem)graph.Item.Cache.CreateCopy(src);
                        newItem.InventoryID = null;
                        newItem.NoteID = null;
                        newItem.InventoryCD = p.NewInventoryCD;
                        newItem.Descr = p.NewDescription;

                        newItem = graph.Item.Insert(newItem);

                        PXCache itemCache = graph.Item.Cache;
                        itemCache.SetValueExt(newItem, "UsrIsbn13", p.UsrIsbn13);
                        itemCache.SetValueExt(newItem, "UsrIsbn10", p.UsrIsbn10);
                        itemCache.SetValueExt(newItem, "UsrCopyrightDate", p.UsrCopyrightDate);
                        graph.Item.Update(newItem);
                        graph.Actions.PressSave();

                        int? newID = newItem.InventoryID;

                        if (p.CopyPrices)
                            UpsertActiveSalesPrices(newID, p.SrcInventoryID, bizDate);
                        else
                            ZeroDefaultPrices(graph, newID);

                        ts.Complete();
                    }
                    finally
                    {
                        PXContext.SetSlot<string>(SkipSetDefaultSiteSlot, null);
                    }
                }
            }

            private static void ZeroDefaultPrices(InventoryItemMaint g, int? inventoryID)
            {
                foreach (InventoryItemCurySettings s in
                         SelectFrom<InventoryItemCurySettings>
                         .Where<InventoryItemCurySettings.inventoryID.IsEqual<@P.AsInt>>
                         .View.Select(g, inventoryID).RowCast<InventoryItemCurySettings>())
                {
                    s.BasePrice = 0m;
                    g.ItemCurySettings.Update(s);
                }
                g.Actions.PressSave();
            }

            private static void UpsertActiveSalesPrices(int? newID, int? srcID, DateTime asOf)
            {
                if (newID == null || srcID == null) return;

                using (PriceCascade.Suppress())
                {
                    PXGraph g = new PXGraph();
                    PXCache spCache = g.Caches<ARSalesPrice>();

                    var active = SelectFrom<ARSalesPrice>
                                 .Where<ARSalesPrice.inventoryID.IsEqual<@P.AsInt>>
                                 .View.Select(g, srcID)
                                 .RowCast<ARSalesPrice>()
                                 .Where(sp => IsActive(sp, asOf));

                    foreach (ARSalesPrice src in active)
                    {
                        ARSalesPrice dst = (ARSalesPrice)spCache.CreateInstance();

                        dst.InventoryID = newID;
                        dst.PriceType = src.PriceType;
                        dst.PriceCode = src.PriceCode;
                        dst.UOM = src.UOM;
                        dst.CuryID = src.CuryID;
                        dst.BreakQty = src.BreakQty;
                        dst.SalesPrice = src.SalesPrice;
                        dst.SiteID = src.SiteID;
                        dst.EffectiveDate = src.EffectiveDate;
                        dst.ExpirationDate = src.ExpirationDate;
                        dst.CustomerID = src.CustomerID;
                        dst.PriceClassID = src.PriceClassID;

                        spCache.Insert(dst);
                    }
                    g.Persist();
                }
            }

            private static bool IsActive(ARSalesPrice sp, DateTime asOf)
            {
                bool dateOk = (sp.EffectiveDate == null || sp.EffectiveDate.Value.Date <= asOf.Date) &&
                              (sp.ExpirationDate == null || sp.ExpirationDate.Value.Date >= asOf.Date);
                return dateOk && (sp.SalesPrice == null || sp.SalesPrice >= 0m);
            }
        }
    }
}
