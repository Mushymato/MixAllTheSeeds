using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Machines;
using SObject = StardewValley.Object;

namespace MixAllTheSeeds.Features;

public static class UnmixTheseSeeds
{
    private static IAssetName IE_SeedsAssetName =>
        field ??= ModEntry.help.GameContent.ParseAssetName("Mods/mistyspring.ItemExtensions/MixedSeeds");

    public static void Setup()
    {
        ModEntry.help.Events.Content.AssetRequested += OnAssetRequested;
    }

    public static void Toggle()
    {
        ModEntry.help.GameContent.InvalidateCache("Data\\Machines");
    }

    private static void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo("Data\\Machines"))
        {
            e.Edit(Edit_Machines, AssetEditPriority.Default);
        }
    }

    private static void Edit_Machines(IAssetData asset)
    {
        if (!ModEntry.config.Enable_SeedMakerUnmixes)
            return;
        IDictionary<string, MachineData> data = asset.AsDictionary<string, MachineData>().Data;
        if (!data.TryGetValue("(BC)25", out MachineData? seedMaker))
            return;
        HashSet<string> mixedSeeds = [Crop.mixedSeedsQId, "(O)MixedFlowerSeeds"];
        if (ModEntry.help.GameContent.DoesAssetExist<dynamic>(IE_SeedsAssetName))
        {
            dynamic IE_Seeds = ModEntry.help.GameContent.Load<dynamic>(IE_SeedsAssetName);
            foreach (string key in IE_Seeds.Keys)
                mixedSeeds.Add(key);
        }
        seedMaker.OutputRules.Insert(
            0,
            new MachineOutputRule()
            {
                Triggers = mixedSeeds
                    .Select(qId => new MachineOutputTriggerRule()
                    {
                        Id = $"ItemPlacedInMachine_{qId}",
                        Trigger = MachineOutputTrigger.ItemPlacedInMachine,
                        RequiredItemId = qId,
                    })
                    .ToList(),
                OutputItem =
                [
                    new()
                    {
                        Id = $"{ModEntry.ModId}_Unmix",
                        OutputMethod = $"{typeof(UnmixTheseSeeds).AssemblyQualifiedName}:{nameof(OutputUnmixSeeds)}",
                    },
                ],
                MinutesUntilReady = 10,
            }
        );
    }

    public static Item? OutputUnmixSeeds(
        SObject machine,
        Item inputItem,
        bool probe,
        MachineItemOutput outputData,
        Farmer player,
        out int? overrideMinutesUntilReady
    )
    {
        overrideMinutesUntilReady = null;
        GameLocation location = machine.Location ?? player.currentLocation ?? Utility.getHomeOfFarmer(player);
        if (location == null)
            return null;
        string outputSeed = Crop.ResolveSeedId(inputItem.ItemId, location);
        if (outputSeed == inputItem.ItemId && ReallyMixedSeeds.IE_CropPatches_ResolveSeedId_Fn != null)
        {
            try
            {
                outputSeed = ReallyMixedSeeds.IE_CropPatches_ResolveSeedId_Fn(inputItem.ItemId, location);
            }
            catch
            {
                return null;
            }
        }
        if (outputSeed == inputItem.ItemId)
            return null;
        return ItemRegistry.Create<SObject>(outputSeed);
    }
}
