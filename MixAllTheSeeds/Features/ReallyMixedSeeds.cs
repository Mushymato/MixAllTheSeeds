using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Crops;
using StardewValley.Internal;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
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

    private static readonly MethodInfo? Tractor_TryGetHoeDirt = AccessTools.DeclaredMethod(
        "Pathoschild.Stardew.TractorMod.Framework.Attachments.BaseAttachment:TryGetHoeDirt"
    );

    private static int cropHash = -1;
    private static int objHash = -1;
    private static List<string>[]? cachedSeedLists = null;
    private static HashSet<string>? nonRareSeeds = null;

    public static void Setup()
    {
        Toggle_Mixed();
    }

    public static void Toggle_Mixed()
    {
        if (CanMix)
        {
            cachedSeedLists = null;
            nonRareSeeds = null;
            hoeDirtRef.Value.SetTarget(null);
            ModEntry.help.Events.GameLoop.DayStarted -= OnDayStarted;
            Unpatch();
            return;
        }
        ModEntry.help.Events.GameLoop.DayStarted += OnDayStarted;
        Patch();
    }

    private static void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        UpdateNonRareSeedList();
    }

    public static void UpdateNonRareSeedList()
    {
        if (!ModEntry.config.Mix_ExcludeRare)
        {
            nonRareSeeds = null;
            return;
        }
        nonRareSeeds = [];
        foreach (
            ISalable salable in ShopBuilder.GetShopStock("SeedShop").Keys.Concat(ShopBuilder.GetShopStock("Joja").Keys)
        )
        {
            if (salable is SObject obj && Game1.cropData.ContainsKey(obj.ItemId))
                nonRareSeeds.Add(obj.ItemId);
        }
        ModEntry.Log(string.Join(',', nonRareSeeds));
    }

    private static void Patch()
    {
        ModEntry.Log($"{nameof(ReallyMixedSeeds)}: Enabled", LogLevel.Info);
        ModEntry.harmony.Patch(
            original: HoeDirt_canPlantThisSeedHere,
            prefix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(HoeDirt_canPlantThisSeedHere_Prefix))
            {
                priority = Priority.First,
            }
        );
        ModEntry.harmony.Patch(
            original: Crop_ResolveSeedId,
            postfix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(Crop_ResolveSeedId_Postfix))
        );
        if (IE_CropPatches_ResolveSeedId != null)
        {
            ModEntry.Log($"Patch ItemExtensions: {IE_CropPatches_ResolveSeedId}");
            ModEntry.harmony.Patch(
                original: IE_CropPatches_ResolveSeedId,
                postfix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(Crop_ResolveSeedId_Postfix))
            );
        }
        if (Tractor_TryGetHoeDirt != null)
        {
            ModEntry.Log($"Patch TractorMod: {Tractor_TryGetHoeDirt}");
            ModEntry.harmony.Patch(
                original: Tractor_TryGetHoeDirt,
                postfix: new HarmonyMethod(typeof(ReallyMixedSeeds), nameof(Tractor_TryGetHoeDirt_Postfix))
            );
        }
    }

    private static void Unpatch()
    {
        ModEntry.Log($"{nameof(ReallyMixedSeeds)}: Disabled", LogLevel.Info);
        ModEntry.harmony.Unpatch(HoeDirt_canPlantThisSeedHere, HarmonyPatchType.Prefix, ModEntry.ModId);
        ModEntry.harmony.Unpatch(Crop_ResolveSeedId, HarmonyPatchType.Postfix, ModEntry.ModId);
        if (IE_CropPatches_ResolveSeedId != null)
        {
            ModEntry.Log($"Unpatch ItemExtensions: {IE_CropPatches_ResolveSeedId}");
            ModEntry.harmony.Unpatch(IE_CropPatches_ResolveSeedId, HarmonyPatchType.Postfix, ModEntry.ModId);
        }
        if (Tractor_TryGetHoeDirt != null)
        {
            ModEntry.Log($"Unpatch TractorMod: {Tractor_TryGetHoeDirt}");
            ModEntry.harmony.Unpatch(original: Tractor_TryGetHoeDirt, HarmonyPatchType.Postfix, ModEntry.ModId);
        }
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

    // yearns for the tick cache
    private static readonly PerScreen<WeakReference<HoeDirt?>> hoeDirtRef = new(() => new(null));

    private static bool HoeDirt_canPlantThisSeedHere_Prefix(HoeDirt __instance, string itemId, ref bool __result)
    {
        bool isMixedSeed =
            (ModEntry.config.Enable_ReallyMixedSeeds && itemId == Crop.mixedSeedsId)
            || (ModEntry.config.Enable_ReallyMixedFlowerSeeds && itemId == "MixedFlowerSeeds");
        hoeDirtRef.Value.SetTarget(__instance);
        // reduce amount of mixed seed checking by assuming can plant
        if (Game1.didPlayerJustClickAtAll())
            return true;
        if (!isMixedSeed)
            return true;
        GameLocation? location = __instance.Location;
        __result = location != null && (!location.IsOutdoors || (location.GetData()?.CanPlantHere ?? location.IsFarm));
        return false;
    }

    private static void Tractor_TryGetHoeDirt_Postfix(ref HoeDirt? dirt)
    {
        hoeDirtRef.Value.SetTarget(dirt);
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
                bool onlyFlowers = parsedItemData.Category == SObject.flowersCategory;
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

    public static void ShuffleInPlace<T>(this Random rand, List<T> listToShuffle)
    {
        int n = listToShuffle.Count;
        while (n > 1)
        {
            // https://en.wikipedia.org/wiki/Fisher%E2%80%93Yates_shuffle
            n--;
            int k = rand.Next(n + 1);
            (listToShuffle[n], listToShuffle[k]) = (listToShuffle[k], listToShuffle[n]);
        }
    }

    private static void Crop_ResolveSeedId_Postfix(string itemId, GameLocation location, ref string __result)
    {
        if (location == null)
            return;

        bool onlyFlowers;

        if (itemId == Crop.mixedSeedsId && ModEntry.config.Enable_ReallyMixedSeeds)
        {
            onlyFlowers = false;
        }
        else if (itemId == "MixedFlowerSeeds" && ModEntry.config.Enable_ReallyMixedFlowerSeeds)
        {
            onlyFlowers = true;
        }
        else
        {
            return;
        }

        List<string> matchingSeeds = GetCachedSeedList(location, onlyFlowers);
        if (!matchingSeeds.Any())
            return;
        if (hoeDirtRef.Value.TryGetTarget(out HoeDirt? hoeDirt) && hoeDirt != null && hoeDirt.Location == location)
        {
            Point tilePoint = hoeDirt.Tile.ToPoint();
            bool isGardenPot = location.objects.TryGetValue(hoeDirt.Tile, out SObject obj) && obj is IndoorPot;
            bool skipRaisedCrop =
                ModEntry.config.Mix_ExcludeRaised
                || Utility.doesRectangleIntersectTile(Game1.player.GetBoundingBox(), tilePoint.X, tilePoint.Y);
            Random.Shared.ShuffleInPlace(matchingSeeds);
            foreach (string randSeed in matchingSeeds)
            {
                if (!Game1.cropData.TryGetValue(randSeed, out CropData? cropData))
                    continue;
                if (skipRaisedCrop && cropData.IsRaised)
                    continue;
                if (ModEntry.config.Mix_ExcludeRegrowing && cropData.RegrowDays > 0)
                    continue;
                if (nonRareSeeds != null && !nonRareSeeds.Contains(randSeed))
                    continue;
                if (!location.CanPlantSeedsHere(randSeed, tilePoint.X, tilePoint.Y, isGardenPot, out _))
                    continue;
                __result = randSeed;
                break;
            }
        }
        else
        {
            __result = Random.Shared.ChooseFrom(matchingSeeds);
        }
        return;
    }
}
