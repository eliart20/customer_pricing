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
    public sealed class ARPriceClass_Ext : PXCacheExtension<ARPriceClass>
    {
        #region ParentPriceClassID
        [PXDBString(10, IsUnicode = true)]
        [PXUIField(DisplayName = "Parent Price Class")]
        [PXSelector(typeof(Search<ARPriceClass.priceClassID>))]
        public string ParentPriceClassID { get; set; }
        public abstract class  parentPriceClassID : PX.Data.BQL.BqlInt.Field<parentPriceClassID> { }

        #endregion
    }
}
