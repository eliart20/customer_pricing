using System;
using PX.Objects.AR;
using PX.Data;

namespace CustomerPricing
{
    [Serializable]
    [PXCacheName("PriceHierarchyDisplay")]
    public class PriceHierarchyDisplay : PXBqlTable, IBqlTable
    {
        #region InventoryCd
        [PXDBString(30, IsUnicode = true, InputMask = "")]
        [PXUIField(DisplayName = "Inventory Cd")]
        public virtual string InventoryCd { get; set; }
        public abstract class inventoryCd : PX.Data.BQL.BqlString.Field<inventoryCd> { }
        #endregion

        #region InventoryID
        [PXDBInt()]
        [PXUIField(DisplayName = "Inventory ID")]
        public virtual int? InventoryID { get; set; }
        public abstract class inventoryID : PX.Data.BQL.BqlInt.Field<inventoryID> { }
        #endregion

        #region BasePrice
        [PXDBDecimal()]
        [PXUIField(DisplayName = "Base Price")]
        public virtual Decimal? BasePrice { get; set; }
        public abstract class basePrice : PX.Data.BQL.BqlDecimal.Field<basePrice> { }
        #endregion

        #region PriceClassID
        [PXDBString(10, IsUnicode = true, InputMask = "")]
        [PXUIField(DisplayName = "Price Class ID")]
        [PXSelector(typeof(Search<ARPriceClass.priceClassID>),
            typeof(ARPriceClass.priceClassID),
            typeof(ARPriceClass.description))]
        public virtual string PriceClassID { get; set; }
        public abstract class priceClassID : PX.Data.BQL.BqlString.Field<priceClassID> { }
        #endregion


        #region SourcePrice
        [PXDBString(10, IsUnicode = true, InputMask = "")]
        [PXUIField(DisplayName = "Source Price")]
        public virtual string SourcePrice { get; set; }
        public abstract class sourcePrice : PX.Data.BQL.BqlString.Field<sourcePrice> { }
        #endregion

        #region Price
        [PXDBDecimal()]
        [PXUIField(DisplayName = "Price")]
        public virtual Decimal? Price { get; set; }
        public abstract class price : PX.Data.BQL.BqlDecimal.Field<price> { }
        #endregion

        #region BreakQty
        [PXDBDecimal()]
        [PXUIField(DisplayName = "Break Qty")]
        public virtual Decimal? BreakQty { get; set; }
        public abstract class breakQty : PX.Data.BQL.BqlDecimal.Field<breakQty> { }
        #endregion

        #region CuryID
        [PXDBString(5, IsUnicode = true, InputMask = "")]
        [PXUIField(DisplayName = "Cury ID")]
        public virtual string CuryID { get; set; }
        public abstract class curyID : PX.Data.BQL.BqlString.Field<curyID> { }
        #endregion
    }
}