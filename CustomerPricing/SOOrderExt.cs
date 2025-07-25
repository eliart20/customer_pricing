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
    public sealed class SOOrderExt : PXCacheExtension<PX.Objects.SO.SOOrder>
    {
        #region UsrOrderPriceClass
        [PXDBString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Order Price Class")]
        [PXSelector(typeof(ARPriceClass.priceClassID))]
        public string UsrOrderPriceClass { get; set; }

        public abstract class usrOrderPriceClass : PX.Data.BQL.BqlString.Field<usrOrderPriceClass> { }

        #endregion
    }
}
