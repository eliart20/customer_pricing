// ───────────────────────────────────────────────────────────────────────────
// CustomDiscountEngineExt.cs
//   • Customer-class entities (CE/CP/PP/PB) are replaced with the header field
//     SOOrderExt.UsrOrderPriceClass when selecting applicable discount
//     sequences.
//   • When a DiscountID is entered on a SOLine the graph:
//       1. Retrieves InventoryItem.BasePrice.
//       2. Writes the price with SetValue (silent, no PXPriceCostAttribute).
//       3. Flags ManualPrice = true with SetValueExt (external call).
//       4. Executes the base DiscountID handler so the engine sees the
//          overridden price with ManualPrice already set.
//
// Compilation target: Acumatica 2023 R2 (C# 7.3)
// ───────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using CustomerPricing;
using PX.Data;
using PX.Data.BQL;
using PX.Data.BQL.Fluent;
using PX.Objects.Common.Discount;
using PX.Objects.CS;
using PX.Objects.IN;
using PX.Objects.SO;

namespace PX.Objects.SO
{
    public sealed class DiscountEngineExt
        : PXGraphExtension<DiscountEngine<SOLine, SOOrderDiscountDetail>>
    {

    }
}
