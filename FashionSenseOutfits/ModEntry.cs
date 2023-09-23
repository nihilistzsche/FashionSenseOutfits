using FashionSense.Framework.Interfaces.API;

using FashionSenseOutfits.Models;

using StardewModdingAPI;
using StardewModdingAPI.Events;

using StardewValley;

using System;
using System.Collections.Generic;
using System.Linq;

namespace FashionSenseOutfits
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        internal static IApi fsApi;
        internal static Dictionary<string, ModData> data;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Content.AssetReady += OnAssetReady;
        }

        private (bool Valid, string CorrectedID) IsValid(string outfitID)
        {
            if (outfitID != null && outfitID != "")
            {
                var outfitIDs = fsApi.GetOutfitIds().Value;
                var correctedID = outfitIDs.Where(x => x.Equals(outfitID, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                return (correctedID != null, correctedID);
            }
            return (false, null);
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            fsApi = Helper.ModRegistry.GetApi<IApi>("PeacefulEnd.FashionSense");
        }

        private void LoadData(dynamic e)
        {
            if (e.GetType().GetProperty("IsLocalPlayer") == null || e.IsLocalPlayer)
            {
                Helper.GameContent.InvalidateCache("nihilistzsche.FashionSenseOutfits/Outfits");
                data = Game1.content.Load<Dictionary<string, ModData>>("nihilistzsche.FashionSenseOutfits/Outfits");
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => LoadData(e);

        private void OnDayStarted(object? sender, DayStartedEventArgs e) => LoadData(e);

        private void OnWarped(object? sender, WarpedEventArgs e) => LoadData(e);

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo("nihilistzsche.FashionSenseOutfits/Outfits"))
            {
                e.LoadFrom(() =>
                {
                    return new Dictionary<string, ModData>
                    {
                        ["CurrentOutfit"] = new ModData { OutfitID = "" }
                    };
                }, AssetLoadPriority.Medium);
            }
        }

        private void OnAssetReady(object? sender, AssetReadyEventArgs e)
        {
            if (e.Name.IsEquivalentTo("nihilistzsche.FashionSenseOutfits/Outfits"))
            {
                data = Game1.content.Load<Dictionary<string, ModData>>("nihilistzsche.FashionSenseOutfits/Outfits");
                UpdateOutfit();
            }
        }

        private void UpdateOutfit()
        {
            var currentOutfitID = data["CurrentOutfit"].OutfitID;
            var CurrentOutfitPair = fsApi.GetCurrentOutfitId();
            var (Valid, CorrectedID) = IsValid(currentOutfitID);
            if (!Valid)
            {
                Monitor.Log($"Given outfit {currentOutfitID} is invalid.");
                return;
            }
            else if (currentOutfitID != CorrectedID)
            {
                currentOutfitID = CorrectedID;
            }
            if (CurrentOutfitPair.Key && currentOutfitID == CurrentOutfitPair.Value)
            {
                Monitor.Log($"Skipping because outfit ({currentOutfitID}) is already equipped.");
            }
            else
            {
                Monitor.Log($"Applying outfit {currentOutfitID} via Fashion Sense API...");
                fsApi.SetCurrentOutfitId(currentOutfitID, ModManifest);
            }
        }
    }
}
