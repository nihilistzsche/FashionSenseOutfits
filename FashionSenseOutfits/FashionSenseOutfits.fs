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

open System
open System.Collections.Generic
open System.Linq

open FashionSense.Framework.Interfaces.API
open ContentPatcher

open FashionSenseOutfits.Models

open StardewModdingAPI
open StardewModdingAPI.Events

open StardewValley


type public FashionSenseOutfits() =
    inherit Mod()
    let AssetName: System.String = "nihilistzsche.FashionSenseOutfits/Outfits"
    let mutable _fsApi: IApi = null
    let mutable _cpApi: IContentPatcherAPI = null
    let mutable _data: Dictionary<System.String, OutfitData> = null
    let mutable _lastEvent: Event = null

    let IsValid(requestedOutfitId: System.String): bool*System.String =
        if System.String.IsNullOrEmpty(requestedOutfitId) then
            (false, null)
        else
            let outfitIds = _fsApi.GetOutfitIds().Value
            let correctedId = outfitIds.FirstOrDefault(fun outfitId -> outfitId.Equals(requestedOutfitId, StringComparison.OrdinalIgnoreCase))
            (correctedId <> null, correctedId)

    let helper = base.Helper
    let modManifest = base.ModManifest
    let monitor = base.Monitor

    let LoadData(e: System.Object) =
        let isLocal = e.GetType().GetProperty("IsLocalPlayer")
        if isLocal = null || isLocal.GetValue(e) :?> bool then
            helper.GameContent.InvalidateCache(AssetName) |> ignore
            _data <- Game1.content.Load<Dictionary<System.String, OutfitData>>(AssetName)

    let OnGameLaunched(e: GameLaunchedEventArgs) =
        _fsApi <- helper.ModRegistry.GetApi<IApi>("PeacefulEnd.FashionSense")
        _cpApi <- helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher")
        _cpApi.RegisterToken(modManifest, 
            "CurrentOutfit", 
            fun () ->
                if not Context.IsWorldReady then
                    [ null ]
                else
                    let outfitPair = _fsApi.GetCurrentOutfitId()
                    [ if outfitPair.Key then outfitPair.Value else null ]
        )

    let OnTimeChanged(e: TimeChangedEventArgs) =
        if e.NewTime % 100 = 0 then LoadData(e)

    let OnUpdateTicked(e: UpdateTickedEventArgs) =
        if _lastEvent <> null && Game1.CurrentEvent = null then LoadData(e)
        _lastEvent <- Game1.CurrentEvent

    let OnAssetRequested(e: AssetRequestedEventArgs) =
        if e.Name.IsEquivalentTo(AssetName) then
            e.LoadFrom(fun() -> 
                let baseData = new Dictionary<System.String, OutfitData>()
                let baseOutfitData = new OutfitData(System.String.Empty)
                baseData.["RequestedOutfit"] <- baseOutfitData
                baseData
            , AssetLoadPriority.Medium)

    let OnAssetReady(e: AssetReadyEventArgs) =
        if e.Name.IsEquivalentTo(AssetName) then
            let requestedOutfitId = _data["RequestedOutfit"].OutfitId
            let currentOutfitPair = _fsApi.GetCurrentOutfitId();
            let (valid, correctedId) = IsValid(requestedOutfitId)
            if valid then
                if not currentOutfitPair.Key || correctedId <> currentOutfitPair.Value then
                    monitor.Log($"Applying outfit with ID {correctedId} via Fashion Sense API...")
                    _fsApi.SetCurrentOutfitId(correctedId, modManifest) |> ignore
                else
                    monitor.Log($"Skipping because the outfit with ID {correctedId} is already equipped.")
            else
                monitor.Log($"Given outfit with ID {requestedOutfitId} is invalid.")

    override this.Entry(helper: IModHelper) =
        helper.Events.GameLoop.GameLaunched.Add(OnGameLaunched)
        helper.Events.GameLoop.SaveLoaded.Add(LoadData)
        helper.Events.GameLoop.DayStarted.Add(LoadData)
        helper.Events.GameLoop.TimeChanged.Add(OnTimeChanged)
        helper.Events.GameLoop.UpdateTicked.Add(OnUpdateTicked)
        helper.Events.Player.Warped.Add(LoadData)
        helper.Events.Content.AssetRequested.Add(OnAssetRequested)
        helper.Events.Content.AssetReady.Add(OnAssetReady)

   


