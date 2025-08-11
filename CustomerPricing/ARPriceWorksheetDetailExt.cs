using PX.Data;
using PX.Data.BQL;
using PX.Objects.AR;
using PX.Objects.SO;
using PX.Objects.CM;
using PX.Objects.IN;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomerPricing
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public sealed class ARPriceWorkSheetDetailExt : PXCacheExtension<PX.Objects.AR.ARPriceWorksheetDetail>
    {

        public abstract class usrDefaultPrice : BqlDecimal.Field<usrDefaultPrice> { }

        [PXDBBaseCury]
        [PXUIField(DisplayName = "Default Price", Enabled = false)]
        [PXDefault(
            typeof(Search<InventoryItemCurySettings.basePrice,
                   Where<InventoryItemCurySettings.inventoryID, Equal<Current<ARPriceWorksheetDetail.inventoryID>>,
                     And<InventoryItemCurySettings.curyID, Equal<Current<ARPriceWorksheetDetail.curyID>>>>>),
            PersistingCheck = PXPersistingCheck.Nothing)]
        [PXFormula(typeof(Default<ARPriceWorksheetDetail.inventoryID>))]
        public decimal? UsrDefaultPrice { get; set; }

        #region UsrSourcePercentOff
        [PXDBDecimal]
        [PXUIField(DisplayName = "Source Percent Off Base")]
        [PXUIEnabled(typeof(False))]
        public decimal? UsrSourcePercentOff { get; set; }
        public abstract class usrSourcePercentOff : PX.Data.BQL.BqlDecimal.Field<usrSourcePercentOff> { }
        #endregion

        #region UsrPricePercentOff
        [PXDBDecimal]
        [PXUIField(DisplayName = "Percent Off Base")]
        [PXUIEnabled(typeof(Where<ARPriceWorksheetDetail.inventoryID, IsNotNull>))]
        public decimal? UsrPricePercentOff { get; set; }
        public abstract class usrPricePercentOff : PX.Data.BQL.BqlDecimal.Field<usrPricePercentOff> { }
        #endregion

        #region PendingPrice
        [PXMergeAttributes(Method = MergeMethod.Append)]
        [PXUIEnabled(typeof(Where<ARPriceWorkSheetDetailExt.usrPricePercentOff, IsNull>))]
        public new decimal? PendingPrice { get; set; }
        public abstract class pendingPrice : PX.Data.BQL.BqlDecimal.Field<pendingPrice> { }

        #endregion

    }
}
