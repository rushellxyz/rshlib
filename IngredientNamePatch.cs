using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace RshLib
{
    [HarmonyPatch(typeof(PlayerCamera), "IngredientTextForRecipe")]
    internal class IngredientNamePatch
    {
        static bool Prefix(PlayerCamera __instance, ref string __result, Recipe recipe, List<Item> its = null)
        {
            __result = "";
            if (its == null)
            {
                its = recipe.GetItemsForRecipeThorough();
            }
            for (int i = 0; i < recipe.items.Count; i++)
            {
                RecipeItem recit = recipe.items[i];
                __result += "<color=#FFFFFF>";
                __result += (its[i] ? "<sprite index=23>" : "<sprite index=24>");
                __result = (recit.specific ? ((!recit.isLiquid) ? (__result + Item.GetItem(recit.specificId).fullName + "\n") : (__result + Locale.GetOther(recit.specificId) + "\n")) : ((!recit.isLiquid) ? (__result + Locale.GetOther((recit.quality.id == "hammering" || recit.quality.id == "cutting") ? "craftanytool" : "craftanyitem") + "\n") : (__result + Locale.GetOther("craftanyliquid") + "\n")));
                __result += "<color=#666666>";
                if (recit.isLiquid)
                {
                    if (!recit.specific)
                    {
                        __result = __result + Locale.GetOther("craftliquidquality").Replace("<1>", recit.quality.amount.ToString("0.#")).Replace("<2>", recit.quality.LocaleName) + "\n";
                    }
                    else if (recit.minimumCondition > 0f)
                    {
                        __result = __result + Locale.GetOther("craftml").Replace("<>", recit.minimumCondition.ToString("0.#")) + "\n";
                    }
                    continue;
                }
                if (!recit.specific)
                {
                    __result += Locale.GetOther("craftitemquality").Replace("<1>", recit.quality.amount.ToString("0.#")).Replace("<2>", recit.quality.LocaleName);
                    KeyValuePair<CraftingQuality, string> keyValuePair = Recipes.QualityExamples.FirstOrDefault((KeyValuePair<CraftingQuality, string> x) => x.Key.id == recit.quality.id && x.Key.amount == recit.quality.amount);
                    if (keyValuePair.Value != null)
                    {
                        __result += Locale.GetOther("craftexample").Replace("<>", Locale.GetItem(keyValuePair.Value));
                    }
                    __result += "\n";
                }
                if (recit.minimumCondition > 0f)
                {
                    __result = __result + Locale.GetOther("craftcondition").Replace("<>", (recit.minimumCondition * 100f).ToString("0.#")) + "\n";
                }
            }
            return false;
        }
    }
}
