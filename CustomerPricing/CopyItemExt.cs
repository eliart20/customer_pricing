#pragma warning disable PX1050
#pragma warning disable PX1016
#pragma warning disable PX1072
using PX.Common;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.AR;
using PX.Objects.CR;
using PX.Objects.IN;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CustomerPricing;          // PriceCascade.Suppress()

namespace PX.Custom.IN
{
    [PXCacheName("Copy Item")]
    [Serializable]
    public class INCopyItemFilter : PXBqlTable, IBqlTable
    {
        #region InventoryCDNew
        public abstract class inventoryCDNew : BqlString.Field<inventoryCDNew> { }
        [PXString(10, IsUnicode = true)]
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
        [PXString(35, IsUnicode = true)]
        [PXUIField(DisplayName = "ISBN 13")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public string UsrIsbn13 { get; set; }
        #endregion
        #region UsrIsbn10
        public abstract class usrIsbn10 : BqlString.Field<usrIsbn10> { }
        [PXString(32, IsUnicode = true)]
        [PXUIField(DisplayName = "ISBN 10")]
        [PXDefault(PersistingCheck = PXPersistingCheck.Nothing)]
        public string UsrIsbn10 { get; set; }
        #endregion
        #region UsrCopyrightDate
        public abstract class usrCopyrightDate : BqlDateTime.Field<usrCopyrightDate> { }
        [PXDate]
        [PXUIField(DisplayName = "Copyright Date")]
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

    // Custom class to handle redirect after PXLongOperation completes
    public class RedirectToItemCustomInfo : IPXCustomInfo
    {
        private readonly int? _inventoryID;
        private readonly string _inventoryCD;
        
        public RedirectToItemCustomInfo(int? inventoryID, string inventoryCD)
        {
            _inventoryID = inventoryID;
            _inventoryCD = inventoryCD;
        }
        
        public void Complete(PXLongRunStatus status, PXGraph graph)
        {
            if (status == PXLongRunStatus.Completed && _inventoryID != null)
            {
                // Create a fresh graph instance for the redirect
                InventoryItemMaint redirectGraph = PXGraph.CreateInstance<InventoryItemMaint>();
                
                // Force database read to ensure the item exists
                InventoryItem newItem = PXSelect<InventoryItem,
                    Where<InventoryItem.inventoryID, Equal<Required<InventoryItem.inventoryID>>>>
                    .Select(redirectGraph, _inventoryID);
                
                if (newItem != null)
                {
                    // Set the current item explicitly
                    redirectGraph.Item.Current = newItem;
                    redirectGraph.Item.Cache.IsDirty = false;
                    
                    // Throw redirect to new window/tab
                    throw new PXRedirectRequiredException(redirectGraph, true, "Item Copied")
                    {
                        Mode = PXBaseRedirectException.WindowMode.New
                    };
                }
                else
                {
                    // If item not found, show error message
                    throw new PXException($"Could not find the newly created item {_inventoryCD}");
                }
            }
        }
    }

    public class InventoryItemMaint_CopyItemExt : PXGraphExtension<InventoryItemMaint>
    {
        private const string SkipSetDefaultSiteSlot = "PX.Custom.IN.CopyItem.SkipSetDefaultSite";

        public PXAction<InventoryItem> CopyItem;
        public PXFilter<INCopyItemFilter> CopyDialog;

        #region Site-default suppression
        public delegate void SetDefaultSiteIDDelegate(InventoryItem row, bool allCurrencies);
        [PXOverride]
        public virtual void SetDefaultSiteID(InventoryItem row, bool allCurrencies, SetDefaultSiteIDDelegate baseMethod)
        {
            bool suppress = (row?.InventoryID ?? 0) < 0 || PXContext.GetSlot<string>(SkipSetDefaultSiteSlot) != null;
            if (!suppress)
                baseMethod(row, allCurrencies);
        }
        protected void _(Events.FieldDefaulting<InventoryItemCurySettings, InventoryItemCurySettings.dfltSiteID> e)
        { if (e.Row?.InventoryID < 0) { e.NewValue = null; e.Cancel = true; } }
        protected void _(Events.FieldVerifying<InventoryItemCurySettings, InventoryItemCurySettings.dfltSiteID> e)
        { if (e.Row?.InventoryID < 0) { e.NewValue = null; e.Cancel = true; } }
        protected void _(Events.RowInserted<InventoryItemCurySettings> e)
        { if (e.Row?.InventoryID < 0 && e.Row.DfltSiteID != null) e.Cache.SetValue<InventoryItemCurySettings.dfltSiteID>(e.Row, null); }
        #endregion

        #region UI Action
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
                object v = Base.Item.Cache.GetValue(src, "UsrCopyrightDate");
                if (v is DateTime dt) f.UsrCopyrightDate = dt;
                v = Base.Item.Cache.GetValue(src, "UsrIsbn13");
                if (v is string s13 && string.IsNullOrWhiteSpace(f.UsrIsbn13)) f.UsrIsbn13 = s13;
                v = Base.Item.Cache.GetValue(src, "UsrIsbn10");
                if (v is string s10 && string.IsNullOrWhiteSpace(f.UsrIsbn10)) f.UsrIsbn10 = s10;
            });

            if (result != WebDialogResult.OK)
                return adapter.Get();

            var ftr = CopyDialog.Current;
            if (ftr == null || string.IsNullOrWhiteSpace(ftr.InventoryCDNew))
                throw new PXSetPropertyException<INCopyItemFilter.inventoryCDNew>("Item Name is required.");

            var cmd = new CopyParams
            {
                SrcInventoryID = Base.Item.Current.InventoryID,
                NewInventoryCD = ftr.InventoryCDNew.Trim(),
                NewDescription = ftr.DescriptionNew,
                UsrIsbn13 = ftr.UsrIsbn13,
                UsrIsbn10 = ftr.UsrIsbn10,
                UsrCopyrightDate = ftr.UsrCopyrightDate,
                CopyPrices = ftr.CopyPrices == true
            };

            // Store the new inventory CD for the custom info
            string newInventoryCD = cmd.NewInventoryCD;

            PXLongOperation.StartOperation(Base, delegate()
            {
                try
                {
                    var newInventoryID = Worker.Execute(cmd);
                    PXTrace.WriteInformation("REDIRECT: Worker completed, newInventoryID={0}", newInventoryID);
                    
                    if (newInventoryID != null)
                    {
                        // Set custom info to handle redirect after operation completes
                        // This ensures the long operation completes before redirect
                        PXLongOperation.SetCustomInfo(new RedirectToItemCustomInfo(newInventoryID, newInventoryCD));
                    }
                    else
                    {
                        throw new PXException("Failed to create new item.");
                    }
                }
                catch (Exception ex)
                {
                    PXTrace.WriteError(ex);
                    throw new PXOperationCompletedException(ex.Message);
                }
            });

            return adapter.Get();
        }
        #endregion

        #region DTO
        [Serializable]
        private sealed class CopyParams
        {
            public int? SrcInventoryID { get; set; }
            public string NewInventoryCD { get; set; }
            public string NewDescription { get; set; }
            public string UsrIsbn13 { get; set; }
            public string UsrIsbn10 { get; set; }
            public DateTime? UsrCopyrightDate { get; set; }
            public bool CopyPrices { get; set; }
        }
        #endregion

        private static class Worker
        {
            /*--- price-type literals ---*/
            private const string PT_BASE = "B";
            private const string PT_CUSTOMER = "C";
            private const string PT_CUSTOMER_CLASS = "P";
            private const string PT_ALL_CUSTOMERS = "A";
            /*---------------------------*/

            internal static int? Execute(CopyParams p)
            {
                if (p?.SrcInventoryID == null) throw new PXException("Source item not found.");

                PXTrace.WriteInformation("COPY-ITEM start: SrcID={0}, NewCD={1}, CopyPrices={2}",
                    p.SrcInventoryID, p.NewInventoryCD, p.CopyPrices);

                var itemGraph = PXGraph.CreateInstance<InventoryItemMaint>();
                var bizDate = itemGraph.Accessinfo.BusinessDate ?? PXTimeZoneInfo.Now;

                using (var ts = new PXTransactionScope())
                {
                    PXContext.SetSlot<string>(SkipSetDefaultSiteSlot, "1");
                    try
                    {
                        #region Clone item
                        var src = SelectFrom<InventoryItem>
                                  .Where<InventoryItem.inventoryID.IsEqual<@P.AsInt>>
                                  .View.ReadOnly.Select(itemGraph, p.SrcInventoryID)
                                  .TopFirst
                                  ?? throw new PXException("Source item disappeared.");

                        var newItem = (InventoryItem)itemGraph.Item.Cache.CreateCopy(src);
                        newItem.InventoryID = null;
                        newItem.NoteID = null;
                        newItem.InventoryCD = p.NewInventoryCD;
                        newItem.Descr = p.NewDescription;
                        newItem = itemGraph.Item.Insert(newItem);

                        var cache = itemGraph.Item.Cache;
                        cache.SetValueExt(newItem, "UsrIsbn13", p.UsrIsbn13);
                        cache.SetValueExt(newItem, "UsrIsbn10", p.UsrIsbn10);
                        cache.SetValueExt(newItem, "UsrCopyrightDate", p.UsrCopyrightDate);
                        itemGraph.Item.Update(newItem);
                        itemGraph.Actions.PressSave();
                        int? newID = newItem.InventoryID;
                        PXTrace.WriteInformation("Clone saved: NewID={0}", newID);
                        #endregion

                        if (p.CopyPrices)
                            UpsertActiveSalesPrices(newID, p.SrcInventoryID, bizDate);
                        else
                            ZeroDefaultPrices(itemGraph, newID);

                        ts.Complete();
                        PXTrace.WriteInformation("COPY-ITEM done: NewID={0}", newID);
                        return newID;
                    }
                    finally
                    {
                        PXContext.SetSlot<string>(SkipSetDefaultSiteSlot, null);
                    }
                }
            }

            #region Price helpers
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
                PXTrace.WriteInformation("Base prices zeroed for item {0}", inventoryID);
            }

            private static string DumpRow(PXCache cache, object row)
            {
                return string.Join(", ",
                    cache.Fields
                         .Select(f => $"{f}={cache.GetValue(row, f) ?? "<null>"}"));
            }

            private static void UpsertActiveSalesPrices(int? newID, int? srcID, DateTime asOf)
            {
                if (newID == null || srcID == null) return;

                PXTrace.WriteInformation("PRICE-COPY start: SrcID={0} → NewID={1}, AsOf={2:d}", srcID, newID, asOf);

                int chosen = 0, inserted = 0, failed = 0;
                var errors = new List<string>();

                using (PriceCascade.Suppress())
                {
                    var g = PXGraph.CreateInstance<ARSalesPriceMaint>();
                    var spCache = g.Caches<ARSalesPrice>();

                    var srcRows = SelectFrom<ARSalesPrice>
                                  .Where<ARSalesPrice.inventoryID.IsEqual<@P.AsInt>>
                                  .View.ReadOnly.Select(g, srcID)
                                  .RowCast<ARSalesPrice>()
                                  .Where(r => IsActive(r, asOf))
                                  .ToList();

                    chosen = srcRows.Count;
                    PXTrace.WriteInformation("PRICE-COPY picked {0} active rows", chosen);

                    foreach (var src in srcRows)
                    {
                        PXTrace.WriteInformation("SRC: {0}",
                            DumpRow(spCache, src));
                        try
                        {
                            var dst = (ARSalesPrice)spCache.CreateInstance();

                            dst.InventoryID = newID;
                            dst.PriceType = src.PriceType;

                            /* key-field normalisation */
                            if (src.PriceType == PT_CUSTOMER_CLASS)
                            {
                                string classCode = !string.IsNullOrWhiteSpace(src.CustPriceClassID)
                                                 ? src.CustPriceClassID
                                                 : src.PriceCode;

                                if (string.IsNullOrWhiteSpace(classCode))
                                    throw new PXException("Source price row lacks class code.");

                                dst.PriceClassID = classCode;
                                dst.PriceCode = classCode;
                            }
                            else if (src.PriceType == PT_CUSTOMER)
                            {
                                dst.CustomerID = src.CustomerID;
                                dst.PriceCode = !string.IsNullOrWhiteSpace(src.PriceCode)
                                               ? src.PriceCode
                                               : ResolveCustomerCode(g, src.CustomerID);
                            }
                            else /* base / all customers */
                            {
                                dst.PriceCode = string.Empty;
                            }

                            dst.UOM = src.UOM;
                            dst.CuryID = src.CuryID ?? string.Empty;
                            dst.BreakQty = src.BreakQty;
                            dst.SalesPrice = src.SalesPrice;
                            dst.SiteID = src.SiteID;
                            dst.EffectiveDate = src.EffectiveDate;
                            dst.ExpirationDate = src.ExpirationDate;

                            spCache.Insert(dst);
                            inserted++;
                            PXTrace.WriteInformation("PRICE-COPY inserted row: PT={0} PC={1} Price={2}",
                                dst.PriceType, dst.PriceCode, dst.SalesPrice);
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            errors.Add(ex.Message);
                            PXTrace.WriteError(ex);
                        }
                    }
                    g.Persist();
                }
                PXTrace.WriteInformation("PRICE-COPY done. Selected={0}, Inserted={1}, Failed={2}", chosen, inserted, failed);
                if (failed > 0)
                    PXTrace.WriteInformation("PRICE-COPY failures:\n{0}", string.Join("\n", errors));
            }

            private static string ResolveCustomerCode(PXGraph g, int? customerID)
            {
                if (customerID == null) return string.Empty;
                var cust = SelectFrom<BAccountR>
                           .Where<BAccountR.bAccountID.IsEqual<@P.AsInt>>
                           .View.ReadOnly.Select(g, customerID)
                           .TopFirst;
                return cust?.AcctCD?.Trim() ?? customerID.Value.ToString();
            }

            private static bool IsActive(ARSalesPrice sp, DateTime asOf) =>
                (sp.EffectiveDate == null || sp.EffectiveDate.Value.Date <= asOf.Date) &&
                (sp.ExpirationDate == null || sp.ExpirationDate.Value.Date >= asOf.Date);
            #endregion
        }
    }
}