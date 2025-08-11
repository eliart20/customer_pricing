using System;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.CM;
using PX.Objects.IN;
using PX.Objects.SO;

namespace PX.Objects.SO
{
    /// <summary>
    /// Adds read-only Default Price and calculated Customer Discount (%) to SOLine.
    /// </summary>

    public sealed class SOLineExt : PXCacheExtension<SOLine>
    {
            
        /* -------------------------------------------------------------
         * Default Price (UsrDefaultPrice)
         * -----------------------------------------------------------*/

        public abstract class usrDefaultPrice : BqlDecimal.Field<usrDefaultPrice> { }

        [PXDBBaseCury]
        [PXUIField(DisplayName = "Default Price", Enabled = false)]
        [PXDefault(
            typeof(Search<InventoryItemCurySettings.basePrice,
                   Where<InventoryItemCurySettings.inventoryID, Equal<Current<SOLine.inventoryID>>,
                     And<InventoryItemCurySettings.curyID, Equal<Current<SOOrder.curyID>>>>>),
            PersistingCheck = PXPersistingCheck.Nothing)]
        [PXFormula(typeof(Default<SOLine.inventoryID>))]
        public decimal? UsrDefaultPrice { get; set; }

        /* -------------------------------------------------------------
         * Customer Discount (%)  =  100 × (1 − UnitPrice / DefaultPrice)
         * -----------------------------------------------------------*/

        public abstract class usrCustomerDiscount : BqlDecimal.Field<usrCustomerDiscount> { }

        [PXDBDecimal(2)]
        [PXUIField(DisplayName = "Customer Discount (%)", Enabled = false)]
        [PXDependsOnFields(typeof(SOLine.curyUnitPrice), typeof(SOLineExt.usrDefaultPrice))]
        [PXFormula(
            typeof(Switch<
                    Case<Where<SOLineExt.usrDefaultPrice, Greater<decimal0>>,
                         Mult<decimal100,
                             Sub<decimal1,
                                 Div<SOLine.curyUnitPrice,
                                     SOLineExt.usrDefaultPrice>>>>,
                    decimal0>))]
        [PXDefault(TypeCode.Decimal, "0.0", PersistingCheck = PXPersistingCheck.Nothing)]
        public decimal? UsrCustomerDiscount { get; set; }

        public abstract class usrIsNetPrice : BqlBool.Field<usrIsNetPrice> { }
        [PXDBBool]
        [PXUIField(DisplayName = "Is Net Price", Enabled = false)]
        [PXDefault(true, PersistingCheck = PXPersistingCheck.Nothing)]
        public bool? UsrIsNetPrice { get; set; }
    }

    /* -------------------------------------------------------------
     * Decimal constants used in BQL expressions
     * -----------------------------------------------------------*/
    public class decimal0 : BqlDecimal.Constant<decimal0> { public decimal0() : base(0m) { } }
    public class decimal1 : BqlDecimal.Constant<decimal1> { public decimal1() : base(1m) { } }
    public class decimal100 : BqlDecimal.Constant<decimal100> { public decimal100() : base(100m) { } }
}
