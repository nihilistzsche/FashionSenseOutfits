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
open Microsoft.FSharp.Collections

open FashionSense.Framework.Interfaces.API
open ContentPatcher

open StardewModdingAPI
open StardewModdingAPI.Events

open StardewValley

open FashionSenseOutfits.Models

type private OutfitDataModel = Dictionary<string, OutfitData>

type public FashionSenseOutfits() =
    inherit Mod()
    static let __assetName: string = "nihilistzsche.FashionSenseOutfits/Outfits"
    static let mutable _fsApi: IApi = null
    static let mutable _cpApi: IContentPatcherAPI = null
    member val private _data: OutfitDataModel = null with get, set 
    member val private _cpConditionsReady = false with get, set
    member val private _lastEvent: Event = null with get, set
    member val private _seenInvalids = [] with get, set
    
    member private _.IsValid(requestedOutfitId: string): bool*string =
        if String.IsNullOrEmpty(requestedOutfitId) then
            (false, null)
        else
            let correctedId = _fsApi.GetOutfitIds().Value.FirstOrDefault(fun outfitId -> outfitId.Equals(requestedOutfitId, StringComparison.OrdinalIgnoreCase))
            (correctedId <> null, correctedId)

    member private this.RequestData(e: obj) =
        let isLocal = e.GetType().GetProperty("IsLocalPlayer")
        if isLocal = null || isLocal.GetValue(e) :?> bool then
            Game1.content.Load<OutfitDataModel>(__assetName) |> ignore
    
    member inline private _.ValidateAsset<'T when 'T : (member Name : IAssetName)>(e: 'T) =
        e.Name.IsEquivalentTo(__assetName)

    member private this.UpdateOutfit() =
        let requestedOutfitId = this._data["RequestedOutfit"].OutfitId
        let currentOutfitPair = _fsApi.GetCurrentOutfitId();
        let valid, correctedId = this.IsValid(requestedOutfitId)
        if valid then
            if not currentOutfitPair.Key || correctedId <> currentOutfitPair.Value then
                _fsApi.SetCurrentOutfitId(correctedId, this.ModManifest) |> ignore
        else if not (List.exists(fun elem -> elem = requestedOutfitId) this._seenInvalids) && requestedOutfitId <> "" then
            this._seenInvalids <- requestedOutfitId :: this._seenInvalids
            this.Monitor.Log($"Given outfit with ID {requestedOutfitId} is invalid.")
    
    member private this.OnGameLaunched(e: GameLaunchedEventArgs) =
        _fsApi <- this.Helper.ModRegistry.GetApi<IApi>("PeacefulEnd.FashionSense")
        _cpApi <- this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher")
        _cpApi.RegisterToken(this.ModManifest, 
            "CurrentOutfit", 
            Func<string[]>(fun () ->
                if not Context.IsWorldReady then
                    [| null |]
                else
                    let outfitPair = _fsApi.GetCurrentOutfitId()
                    [| if outfitPair.Key then outfitPair.Value else null |]
            )
        )
    
    member private this.OnUpdateTicked(e: UpdateTickedEventArgs) =
        if (this._lastEvent <> null && Game1.CurrentEvent = null) || (not this._cpConditionsReady && _cpApi.IsConditionsApiReady) then
            if not this._cpConditionsReady then this._cpConditionsReady <- _cpApi.IsConditionsApiReady
            this.RequestData(e)
        this._lastEvent <- Game1.CurrentEvent

    member private this.OnAssetRequested(e: AssetRequestedEventArgs) =
        if this.ValidateAsset(e) then
            e.LoadFrom((fun() -> OutfitDataModel([ KeyValuePair<string,OutfitData>("RequestedOutfit", OutfitData(String.Empty)) ]) :> obj), AssetLoadPriority.Medium)

    member private this.OnAssetReady(e: AssetReadyEventArgs) =
        if this.ValidateAsset(e) then
            this._data <- Game1.content.Load<OutfitDataModel>(__assetName)
            this.UpdateOutfit()
            
    override this.Entry(helper: IModHelper) =
        helper.Events.GameLoop.GameLaunched.Add(this.OnGameLaunched)
        helper.Events.GameLoop.DayStarted.Add(this.RequestData)
        helper.Events.GameLoop.TimeChanged.Add(this.RequestData)
        helper.Events.GameLoop.UpdateTicked.Add(this.OnUpdateTicked)
        helper.Events.Player.Warped.Add(this.RequestData)
        helper.Events.Content.AssetRequested.Add(this.OnAssetRequested)
        helper.Events.Content.AssetReady.Add(this.OnAssetReady)
