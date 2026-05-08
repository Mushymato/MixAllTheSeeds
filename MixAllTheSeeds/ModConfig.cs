using MixAllTheSeeds.Features;
using MixAllTheSeeds.Integration;
using StardewModdingAPI;

namespace MixAllTheSeeds;

public sealed class ModConfig
{
    public bool Enable_ReallyMixedSeeds { get; set; } = true;
    public bool Enable_ReallyMixedFlowerSeeds { get; set; } = true;
    public bool Mix_ExcludeRegrowing { get; set; } = false;
    public bool Mix_ExcludeRare { get; set; } = false;
    public bool Mix_ExcludeRaised { get; set; } = false;
    public bool Enable_SeedMakerUnmixes { get; set; } = true;

    public void Reset()
    {
        Enable_ReallyMixedSeeds = true;
        Enable_ReallyMixedFlowerSeeds = true;
        Mix_ExcludeRegrowing = false;
        Mix_ExcludeRare = false;
        Mix_ExcludeRaised = false;
        Enable_SeedMakerUnmixes = true;
    }

    public void Register(IModHelper helper, IManifest mod)
    {
        if (
            helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu")
            is not IGenericModConfigMenuApi gmcm
        )
        {
            return;
        }
        gmcm.Register(
            mod,
            Reset,
            () =>
            {
                helper.WriteConfig(this);
            }
        );
        gmcm.AddBoolOption(
            mod,
            () => Enable_ReallyMixedSeeds,
            (value) =>
            {
                bool checkBefore = ReallyMixedSeeds.CanMix;
                Enable_ReallyMixedSeeds = value;
                if (ReallyMixedSeeds.CanMix != checkBefore)
                    ReallyMixedSeeds.Toggle_Mixed();
            },
            I18n.Config_EnableReallyMixedSeeds_Name,
            I18n.Config_EnableReallyMixedSeeds_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => Enable_ReallyMixedFlowerSeeds,
            (value) =>
            {
                bool checkBefore = ReallyMixedSeeds.CanMix;
                Enable_ReallyMixedFlowerSeeds = value;
                if (ReallyMixedSeeds.CanMix != checkBefore)
                    ReallyMixedSeeds.Toggle_Mixed();
            },
            I18n.Config_EnableReallyMixedFlowerSeeds_Name,
            I18n.Config_EnableReallyMixedFlowerSeeds_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => Mix_ExcludeRegrowing,
            (value) => Mix_ExcludeRegrowing = value,
            I18n.Config_MixExcludeRegrowing_Name,
            I18n.Config_MixExcludeRegrowing_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => Mix_ExcludeRaised,
            (value) => Mix_ExcludeRaised = value,
            I18n.Config_MixExcludeRaised_Name,
            I18n.Config_MixExcludeRaised_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => Mix_ExcludeRare,
            (value) =>
            {
                bool checkBefore = Mix_ExcludeRare;
                Mix_ExcludeRare = value;
                if (checkBefore != Mix_ExcludeRare)
                    ReallyMixedSeeds.UpdateNonRareSeedList();
            },
            I18n.Config_MixExcludeRare_Name,
            I18n.Config_MixExcludeRare_Desc
        );
        gmcm.AddBoolOption(
            mod,
            () => Enable_SeedMakerUnmixes,
            (value) =>
            {
                bool checkBefore = Enable_SeedMakerUnmixes;
                Enable_SeedMakerUnmixes = value;
                if (Enable_SeedMakerUnmixes != checkBefore)
                    UnmixTheseSeeds.Toggle();
            },
            I18n.Config_EnableSeedMakerUnmixes_Name,
            I18n.Config_EnableSeedMakerUnmixes_Desc
        );
    }
}
