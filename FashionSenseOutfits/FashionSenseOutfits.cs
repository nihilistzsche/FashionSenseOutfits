// Copyright (C) 2023 Nihilistzsche
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.



namespace FashionSenseOutfits
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FashionSense.Framework.Interfaces.API;
    using Models;
    using StardewModdingAPI;
    using StardewModdingAPI.Events;
    using StardewValley;

    using OutfitDataModel = System.Collections.Generic.Dictionary<string, Models.OutfitData>;

    /// <summary>The mod entry point.</summary>
    // ReSharper disable once UnusedMember.Global
    public class FashionSenseOutfits : Mod
    {
        private const string AssetName = "nihilistzsche.FashionSenseOutfits/Outfits";
        private static IApi _fsApi;
        private static OutfitDataModel _data;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.TimeChanged += OnTimeChanged;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Content.AssetReady += OnAssetReady;
        }

        private static (bool Valid, string CorrectedID) IsValid(string outfitId)
        {
            if (string.IsNullOrEmpty(outfitId))
            {
                return (false, null);
            }

            var outfitIDs = _fsApi.GetOutfitIds().Value;
            var correctedId = outfitIDs.FirstOrDefault(x => x.Equals(outfitId, StringComparison.OrdinalIgnoreCase));
            return (correctedId != null, correctedId);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            _fsApi = Helper.ModRegistry.GetApi<IApi>("PeacefulEnd.FashionSense");
        }

        private void LoadData(dynamic e)
        {
            // ReSharper disable once InvertIf
            if (e.GetType().GetProperty("IsLocalPlayer") == null || e.IsLocalPlayer)
            {
                Helper.GameContent.InvalidateCache(AssetName);
                _data = Game1.content.Load<Dictionary<string, OutfitData>>(AssetName);
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e) => LoadData(e);

        private void OnDayStarted(object sender, DayStartedEventArgs e) => LoadData(e);

        private void OnWarped(object sender, WarpedEventArgs e) => LoadData(e);

        private void OnTimeChanged(object sender, TimeChangedEventArgs e)
        {
            if (e.NewTime % 100 == 0)
            {
                LoadData(e);
            }
        }

        // ReSharper disable once MemberCanBeMadeStatic.Local
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo(AssetName))
            {
                e.LoadFrom(
                    () => new OutfitDataModel
                    {
                        ["CurrentOutfit"] = new() { OutfitId = string.Empty },
                    },
                    AssetLoadPriority.Medium);
            }
        }

        private void OnAssetReady(object sender, AssetReadyEventArgs e)
        {
            // ReSharper disable once InvertIf
            if (e.Name.IsEquivalentTo(AssetName))
            {
                _data = Game1.content.Load<OutfitDataModel>(AssetName);
                UpdateOutfit();
            }
        }

        private void UpdateOutfit()
        {
            var currentOutfitId = _data["CurrentOutfit"].OutfitId;
            var currentOutfitPair = _fsApi.GetCurrentOutfitId();
            var (valid, correctedId) = IsValid(currentOutfitId);
            if (!valid)
            {
                Monitor.Log($"Given outfit with ID {currentOutfitId} is invalid.");
                return;
            }

            if (currentOutfitPair.Key && correctedId == currentOutfitPair.Value)
            {
                Monitor.Log($"Skipping because the outfit with ID {correctedId} is already equipped.");
            }
            else
            {
                Monitor.Log($"Applying outfit with ID {correctedId} via Fashion Sense API...");
                _fsApi.SetCurrentOutfitId(correctedId, ModManifest);
            }
        }
    }
}