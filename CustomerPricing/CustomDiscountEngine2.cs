using System;
using System.Collections.Generic;
using System.Linq;
using PX.Common;           // PXTrace
using PX.Data;
using PX.Objects.AR;
using PX.Objects.Common.Discount;
using PX.Objects.SO;

/* avoid name clash with the struct DiscountCode in your project */
using ARDiscount = PX.Objects.AR.ARDiscount;

namespace CustomerPricing
{
    /*──────────────────── 1 ─ Priority list (editable) ───────────────────────*/
    internal static class DiscountRank
    {
        public static readonly ApplicableToCombination[] Ordered =
        {
            ApplicableToCombination.Customer              | ApplicableToCombination.InventoryItem,          // CI
            ApplicableToCombination.Customer              | ApplicableToCombination.InventoryPriceClass,    // CP
            ApplicableToCombination.Customer              | ApplicableToCombination.Branch,                 // CB
            ApplicableToCombination.Customer,                                                               // CU
            ApplicableToCombination.CustomerPriceClass,                                                     // CE
            ApplicableToCombination.CustomerPriceClass    | ApplicableToCombination.Branch,                // PB
            ApplicableToCombination.CustomerPriceClass    | ApplicableToCombination.InventoryPriceClass,    // PP
            ApplicableToCombination.InventoryItem,                                                           // IN
            ApplicableToCombination.InventoryPriceClass,                                                     // IE
            ApplicableToCombination.InventoryItem         | ApplicableToCombination.CustomerPriceClass,     // PI
            ApplicableToCombination.Warehouse,                                                               // WH
            ApplicableToCombination.Customer              | ApplicableToCombination.Warehouse,              // WC
            ApplicableToCombination.CustomerPriceClass    | ApplicableToCombination.Warehouse,              // WE
            ApplicableToCombination.InventoryItem         | ApplicableToCombination.Warehouse,              // WI
            ApplicableToCombination.InventoryPriceClass   | ApplicableToCombination.Warehouse,              // WP
            ApplicableToCombination.Branch,                                                                 // BR
            ApplicableToCombination.Vendor,                                                                 // VE
            ApplicableToCombination.InventoryItem         | ApplicableToCombination.Vendor,                 // VI
            ApplicableToCombination.InventoryPriceClass   | ApplicableToCombination.Vendor,                 // VP
            ApplicableToCombination.Location,                                                               // VL
            ApplicableToCombination.InventoryItem         | ApplicableToCombination.Location,               // LI
            ApplicableToCombination.Unconditional                                                            // UN
        };

        public static readonly Dictionary<ApplicableToCombination, int> Rank =
            Ordered.Select((c, i) => new { c, i })
                   .ToDictionary(x => x.c, x => x.i);
    }

    /*────────── 2 ─ Entity codes that reference Customer Price Class ─────────*/
    internal static class PriceClassEntities
    {
        public static readonly HashSet<string> Codes = new HashSet<string>
        {
            "CE", "PB", "PP", "PI", "WE", "CP"           // ← added "CP"
        };
    }

    /*────────── 3 ─ Mapping from ARDiscount.applicableTo → enum value ────────*/
    internal static class ApplicableToMapper
    {
        private static readonly Dictionary<string, ApplicableToCombination> Map =
            new Dictionary<string, ApplicableToCombination>
            {
                ["CU"] = ApplicableToCombination.Customer,
                ["IN"] = ApplicableToCombination.InventoryItem,
                ["CE"] = ApplicableToCombination.CustomerPriceClass,
                ["IE"] = ApplicableToCombination.InventoryPriceClass,
                ["CI"] = ApplicableToCombination.Customer | ApplicableToCombination.InventoryItem,
                ["CP"] = ApplicableToCombination.Customer | ApplicableToCombination.InventoryPriceClass,
                ["PI"] = ApplicableToCombination.InventoryItem | ApplicableToCombination.CustomerPriceClass,
                ["PB"] = ApplicableToCombination.CustomerPriceClass | ApplicableToCombination.Branch,
                ["PP"] = ApplicableToCombination.CustomerPriceClass | ApplicableToCombination.InventoryPriceClass,
                ["CB"] = ApplicableToCombination.Customer | ApplicableToCombination.Branch,
                ["WH"] = ApplicableToCombination.Warehouse,
                ["WC"] = ApplicableToCombination.Customer | ApplicableToCombination.Warehouse,
                ["WE"] = ApplicableToCombination.CustomerPriceClass | ApplicableToCombination.Warehouse,
                ["WI"] = ApplicableToCombination.InventoryItem | ApplicableToCombination.Warehouse,
                ["WP"] = ApplicableToCombination.InventoryPriceClass | ApplicableToCombination.Warehouse,
                ["BR"] = ApplicableToCombination.Branch,
                ["VE"] = ApplicableToCombination.Vendor,
                ["VI"] = ApplicableToCombination.InventoryItem | ApplicableToCombination.Vendor,
                ["VP"] = ApplicableToCombination.InventoryPriceClass | ApplicableToCombination.Vendor,
                ["VL"] = ApplicableToCombination.Location,
                ["LI"] = ApplicableToCombination.InventoryItem | ApplicableToCombination.Location,
                ["UN"] = ApplicableToCombination.Unconditional
            };

        public static ApplicableToCombination FromString(string code) =>
            code != null && Map.TryGetValue(code, out ApplicableToCombination cmb)
                ? cmb
                : ApplicableToCombination.Unconditional;
    }

    /*────────────── 5 ─ DiscountEngine extension with overrides ──────────────*/
    // Acuminator disable once PX1016 ExtensionDoesNotDeclareIsActiveMethod
    public class DiscountEngineExt
        : PXGraphExtension<DiscountEngine<SOLine, SOOrderDiscountDetail>>
    {
        private readonly Dictionary<string, ApplicableToCombination> _codeCache =
            new Dictionary<string, ApplicableToCombination>();

        private ApplicableToCombination GetApplicableTo(string discountID, PXGraph graph)
        {
            if (_codeCache.TryGetValue(discountID, out ApplicableToCombination combo))
                return combo;

            ARDiscount dac = PXSelectReadonly<
                                 ARDiscount,
                                 Where<ARDiscount.discountID,
                                     Equal<Required<ARDiscount.discountID>>>>
                             .Select(graph, discountID)
                             .TopFirst;

            combo = ApplicableToMapper.FromString(dac?.ApplicableTo);
            _codeCache[discountID] = combo;

            PXTrace.WriteInformation($"[DiscountEngineExt] Cached ApplicableTo for {discountID} = {combo}");
            return combo;
        }

        public delegate HashSet<DiscountSequenceKey> SelectAppEntitiesDelegate(
            PXGraph graph,
            HashSet<KeyValuePair<object, string>> entities,
            string discountType,
            bool skipManual,
            bool appliedToDR);

        [PXOverride]
        public virtual HashSet<DiscountSequenceKey> SelectApplicableEntityDiscounts(   // public for wrapper
            PXGraph graph,
            HashSet<KeyValuePair<object, string>> entities,
            string discountType,
            bool skipManual,
            bool appliedToDR,
            SelectAppEntitiesDelegate baseMethod)
        {
            string orderPC = (graph as SOOrderEntry)
                                ?.Document.Current?
                                .GetExtension<SOOrderExt>()?
                                .UsrOrderPriceClass;

            PXTrace.WriteInformation($"[DiscountEngineExt] Enter SelectApplicableEntityDiscounts – orderPC={orderPC ?? "<null>"}");

            string EntitySetToString(HashSet<KeyValuePair<object, string>> set) =>
                string.Join(", ", set.Select(e => $"{e.Value}:{e.Key}"));

            HashSet<DiscountSequenceKey> Run(HashSet<KeyValuePair<object, string>> ents)
            {
                PXTrace.WriteInformation($"[DiscountEngineExt] Calling baseMethod with entities [{EntitySetToString(ents)}]");
                HashSet<DiscountSequenceKey> res = baseMethod(graph, ents, discountType, skipManual, appliedToDR);
                PXTrace.WriteInformation($"[DiscountEngineExt] baseMethod returned [{string.Join(", ", res.Select(s => $"{s.DiscountID}/{s.DiscountSequenceID}"))}]");
                return res;
            }

            HashSet<DiscountSequenceKey> seqs;

            if (!string.IsNullOrEmpty(orderPC))
            {
                // swap key for all price‑class based entities
                HashSet<KeyValuePair<object, string>> swapped =
                    new HashSet<KeyValuePair<object, string>>(
                        entities.Select(e =>
                            PriceClassEntities.Codes.Contains(e.Value)
                                ? new KeyValuePair<object, string>(orderPC, e.Value)
                                : e));

                // ensure CE entity present when no price‑class entities existed
                if (!swapped.Any(e => e.Value == "CE"))
                    swapped.Add(new KeyValuePair<object, string>(orderPC, "CE"));

                PXTrace.WriteInformation($"[DiscountEngineExt] Swapped entities [{EntitySetToString(swapped)}]");

                seqs = Run(swapped);

                if (seqs.Count == 0)
                {
                    PXTrace.WriteInformation("[DiscountEngineExt] No sequences matched swapped entities – falling back to original entities");
                    seqs = Run(entities);
                }
            }
            else
            {
                seqs = Run(entities);
            }

            if (seqs.Count <= 1)
                return seqs;

            /* ---- 5‑b. Keep only sequences with highest priority rank ---- */
            int bestRank = seqs
                .Select(s => DiscountRank.Rank.TryGetValue(
                                GetApplicableTo(s.DiscountID, graph), out int r)
                             ? r
                             : int.MaxValue)
                .Min();

            PXTrace.WriteInformation($"[DiscountEngineExt] Best rank={bestRank}");

            HashSet<DiscountSequenceKey> finalSeqs = seqs.Where(s =>
                       DiscountRank.Rank.TryGetValue(
                           GetApplicableTo(s.DiscountID, graph), out int r) && r == bestRank)
                       .ToHashSet();

            PXTrace.WriteInformation($"[DiscountEngineExt] Final sequences [{string.Join(", ", finalSeqs.Select(s => $"{s.DiscountID}/{s.DiscountSequenceID}"))}]");

            return finalSeqs;
        }
    }
}
