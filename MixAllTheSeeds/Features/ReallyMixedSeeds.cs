using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Crops;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;

namespace MixAllTheSeeds.Features;

public static class ReallyMixedSeeds
{
    internal static bool CanMix =>
        !ModEntry.config.Enable_ReallyMixedSeeds && !ModEntry.config.Enable_ReallyMixedFlowerSeeds;

    private static readonly MethodInfo HoeDirt_canPlantThisSeedHere = AccessTools.DeclaredMethod(
        typeof(HoeDirt),
        nameof(HoeDirt.canPlantThisSeedHere)
    );
    private static readonly MethodInfo Crop_ResolveSeedId = AccessTools.DeclaredMethod(
        typeof(Crop),
        nameof(Crop.ResolveSeedId)
    );
    private const string IE_ResolveSeed = "ItemExtensions.Patches.CropPatches:ResolveSeedId";
    private static readonly MethodInfo? IE_CropPatches_ResolveSeedId = AccessTools.DeclaredMethod(IE_ResolveSeed);
    internal static readonly Func<string, GameLocation, string>? IE_CropPatches_ResolveSeedId_Fn = AccessTools
        .DeclaredMethod(IE_ResolveSeed)
        ?.CreateDelegate<Func<string, GameLocation, string>>();
    private static int cropHash = -1;
    private static int objHash = -1;
    private static List<string>[]? cachedSeedLists = null;

    public static void Setup()
    {
        Toggle_Mixed();
    }

    public static void Toggle_Mixed()
    {
        if (CanMix)
        {
            cachedSeedLists = null;
            Unpatch();
            return;
        }
        Patch();
    }

    private static void Patch()
    {
        ModEntry.Log($"{nameof(ReallyMixedSeeds)}: Enabled", LogLevel.Info);
        ModEntry.harmony.Patch(
            original: Crop_ResolveSeedId,
            postfix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(Crop_ResolveSeedId_Postfix))
        );
        ModEntry.harmony.Patch(
            original: IE_CropPatches_ResolveSeedId,
            postfix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(Crop_ResolveSeedId_Postfix))
        );
        ModEntry.harmony.Patch(
            original: HoeDirt_canPlantThisSeedHere,
            prefix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(HoeDirt_canPlantThisSeedHere_Prefix))
            {
                priority = Priority.First,
            }
        );
    }

    private static void Unpatch()
    {
        ModEntry.Log($"{nameof(ReallyMixedSeeds)}: Disabled", LogLevel.Info);
        ModEntry.harmony.Unpatch(Crop_ResolveSeedId, HarmonyPatchType.Postfix, ModEntry.ModId);
        ModEntry.harmony.Unpatch(IE_CropPatches_ResolveSeedId, HarmonyPatchType.Postfix, ModEntry.ModId);
        ModEntry.harmony.Unpatch(HoeDirt_canPlantThisSeedHere, HarmonyPatchType.Prefix, ModEntry.ModId);
    }

    private static int GetSeedListIndex(bool onlyFlowers, bool anySeason, Season season)
    {
        int idx;
        if (anySeason)
            idx = 4;
        else
            idx = season switch
            {
                Season.Spring => 0,
                Season.Summer => 1,
                Season.Fall => 2,
                Season.Winter => 3,
                _ => 4,
            };
        if (onlyFlowers)
            idx += 5;
        return idx;
    }

    private static bool HoeDirt_canPlantThisSeedHere_Prefix(string itemId, ref bool __result)
    {
        // reduce amount of mixed seed checking
        __result = false;
        if (Game1.didPlayerJustClickAtAll())
            return true;
        __result = true;
        if (itemId == Crop.mixedSeedsId && ModEntry.config.Enable_ReallyMixedSeeds)
            return false;
        else if (itemId == "MixedFlowerSeeds" && ModEntry.config.Enable_ReallyMixedFlowerSeeds)
            return false;
        return true;
    }

    private static List<string>[] UpdateCachedSeedLists()
    {
        int newCropHash = Game1.cropData.GetHashCode();
        int newObjHash = Game1.objectData.GetHashCode();
        if (
            cachedSeedLists == null
            || cropHash != Game1.cropData.GetHashCode()
            || objHash != Game1.objectData.GetHashCode()
        )
        {
            cropHash = newCropHash;
            objHash = newObjHash;
            cachedSeedLists =
            [
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
                [],
            ];
            foreach ((string seedId, CropData cropData) in Game1.cropData)
            {
                if (!(cropData.Seasons?.Any() ?? false))
                    continue;
                if (string.IsNullOrEmpty(cropData.HarvestItemId))
                    continue;
                ParsedItemData parsedItemData = ItemRegistry.GetData(cropData.HarvestItemId);
                bool onlyFlowers = parsedItemData.Category == StardewValley.Object.flowersCategory;
                foreach (Season cropSeason in cropData.Seasons)
                {
                    cachedSeedLists[GetSeedListIndex(onlyFlowers, false, cropSeason)].Add(seedId);
                    cachedSeedLists[GetSeedListIndex(onlyFlowers, true, cropSeason)].Add(seedId);
                }
            }
        }
        return cachedSeedLists;
    }

    public static List<string> GetCachedSeedList(GameLocation location, bool onlyFlowers)
    {
        cachedSeedLists = UpdateCachedSeedLists();
        bool anySeason = !location.IsOutdoors || location.SeedsIgnoreSeasonsHere();
        Season season = location.GetSeason();
        int idx = GetSeedListIndex(onlyFlowers, anySeason, season);
        return cachedSeedLists[idx];
    }

    private static void Crop_ResolveSeedId_Postfix(string itemId, GameLocation location, ref string __result)
    {
        bool onlyFlowers;
        if (itemId == Crop.mixedSeedsId && ModEntry.config.Enable_ReallyMixedSeeds)
            onlyFlowers = false;
        else if (itemId == "MixedFlowerSeeds" && ModEntry.config.Enable_ReallyMixedFlowerSeeds)
            onlyFlowers = true;
        else
            return;
        List<string> matchingSeeds = GetCachedSeedList(location, onlyFlowers);
        if (matchingSeeds.Any())
        {
            __result = Random.Shared.ChooseFrom(matchingSeeds);
            ModEntry.LogDebug($"{nameof(ReallyMixedSeeds)}: '{itemId}' -> '{__result}'");
        }
    }
}
