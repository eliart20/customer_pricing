using PX.Data;
using PX.Objects;
using PX.Objects.SO;
using PX.Objects.AR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PX.Objects.CR;

namespace CustomerPricing
{
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod extension should be constantly active
    public sealed class SOOrderExt : PXCacheExtension<PX.Objects.SO.SOOrder>
    {
        #region UsrOrderPriceClass
        [PXDBString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Order Price Class")]
        [PXSelector(typeof(ARPriceClass.priceClassID))]
        [PXDefault(
            typeof(Search<Location.cPriceClassID,
                   Where<Location.bAccountID, Equal<Current<SOOrder.customerID>>,
                     And<Location.locationID, Equal<Current<SOOrder.customerLocationID>>>>>),
            PersistingCheck = PXPersistingCheck.Nothing)]
        [PXFormula(typeof(Default<SOOrder.customerID, SOOrder.customerLocationID>))]
        [PXUIEnabled(typeof(Where<usrOverridePriceClass, Equal<True>>))]
        public string UsrOrderPriceClass { get; set; }
        public abstract class usrOrderPriceClass : PX.Data.BQL.BqlString.Field<usrOrderPriceClass> { }

        [PXDBBool]
        [PXUIField(DisplayName = "Override Price Class")]
        public bool? UsrOverridePriceClass { get; set;}
        public abstract class usrOverridePriceClass : PX.Data.BQL.BqlBool.Field<usrOverridePriceClass> { }

        #endregion
    }
}
