using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace CaravanReadiness.State
{
    internal static class TransferGroupIdentity
    {
        internal static string SignatureFor(TransferableOneWay transferable)
        {
            Thing outer = transferable?.AnyThing;
            if (outer == null)
            {
                return null;
            }

            Thing inner = outer.GetInnerIfMinified();
            var signature = new StringBuilder();
            Append(signature, outer.def?.defName);
            Append(signature, inner?.def?.defName);
            Append(signature, inner?.Stuff?.defName);

            if (inner != null && inner.TryGetQuality(out QualityCategory quality))
            {
                Append(signature, "quality=" + (int)quality);
            }
            if (inner?.def?.useHitPoints == true &&
                inner.def.healthAffectsPrice)
            {
                Append(signature, "hp=" + inner.HitPoints);
            }
            if (inner is Apparel apparel)
            {
                Append(signature, "corpse=" + apparel.WornByCorpse);
            }

            AppendPawnIdentity(signature, inner);
            AppendIngredientIdentity(signature, inner);
            AppendGeneIdentity(signature, inner);

            if (inner != null &&
                (inner.def.tradeNeverStack ||
                 !TransferableUtility.CanStack(inner)))
            {
                Append(signature, "thing=" + outer.thingIDNumber);
            }

            return signature.ToString();
        }

        internal static ThingDef DisplayDefinitionFor(
            TransferableOneWay transferable)
        {
            return transferable?.AnyThing?.GetInnerIfMinified()?.def;
        }

        private static void AppendPawnIdentity(
            StringBuilder signature,
            Thing thing)
        {
            Pawn pawn = thing is Corpse corpse
                ? corpse.InnerPawn
                : thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            Append(signature, "pawnDef=" + pawn.def?.defName);
            Append(signature, "kind=" + pawn.kindDef?.defName);
            Append(signature, "gender=" + (int)pawn.gender);
            Append(signature, "life=" + pawn.ageTracker?.CurLifeStageIndex);
            Append(signature, "age=" + pawn.ageTracker?.AgeBiologicalYears);
        }

        private static void AppendIngredientIdentity(
            StringBuilder signature,
            Thing thing)
        {
            CompIngredients ingredients = thing?.TryGetComp<CompIngredients>();
            if (ingredients == null ||
                !ingredients.Props.performMergeCompatibilityChecks)
            {
                return;
            }

            IEnumerable<string> tags = ingredients.MergeCompatibilityTags
                .OrderBy(tag => tag, System.StringComparer.Ordinal);
            Append(signature, "ingredients=" + string.Join(",", tags));
            if (ingredients.Props.splitTransferableFoodKind)
            {
                Append(signature, "food=" + (int)FoodUtility.GetFoodKind(thing));
            }
        }

        private static void AppendGeneIdentity(
            StringBuilder signature,
            Thing thing)
        {
            GeneSet genes = null;
            if (thing is Genepack genepack)
            {
                genes = genepack.GeneSet;
            }
            else if (thing is Xenogerm xenogerm)
            {
                genes = xenogerm.GeneSet;
            }
            if (genes == null)
            {
                return;
            }

            Append(signature, "geneLabel=" + genes.Label);
            Append(signature, "genes=" + string.Join(",",
                genes.GenesListForReading
                    .Select(gene => gene.defName)
                    .OrderBy(name => name, System.StringComparer.Ordinal)));
        }

        private static void Append(StringBuilder target, string value)
        {
            string safe = value ?? string.Empty;
            target.Append(safe.Length).Append(':').Append(safe).Append('|');
        }
    }
}
