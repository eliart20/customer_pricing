using PX.Data;
using PX.Objects.AR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomerPricing
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public sealed class ARSalesPriceExt: PXCacheExtension<ARSalesPrice>
    {
        #region UsrPricePercentOff
        [PXDBDecimal]
        [PXUIField(DisplayName = "Percent Off Base")]
        [PXUIEnabled(typeof(Where<ARSalesPrice.inventoryID, IsNotNull>))]
        public decimal? UsrPricePercentOff { get; set; }
        public abstract class usrPricePercentOff : PX.Data.BQL.BqlDecimal.Field<usrPricePercentOff> { }
        #endregion


        #region SalesPrice
        [PXMergeAttributes(Method = MergeMethod.Append)]
        [PXUIEnabled(typeof(Where<ARSalesPriceExt.usrPricePercentOff, IsNull>))]
        public new decimal? SalesPrice { get; set; }
        public abstract class salesPrice : PX.Data.BQL.BqlDecimal.Field<salesPrice> { }
        #endregion
    }
}
