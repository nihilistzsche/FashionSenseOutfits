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

open FashionSenseOutfits.Models

open StardewModdingAPI
open StardewModdingAPI.Events

open StardewValley

type private OutfitDataModel = Dictionary<string, OutfitData>

type public FashionSenseOutfits() =
    inherit Mod()
    let AssetName: string = "nihilistzsche.FashionSenseOutfits/Outfits"
    let mutable _fsApi: IApi = null
    let mutable _cpApi: IContentPatcherAPI = null

    let IsValid(requestedOutfitId: string): bool*string =
        if String.IsNullOrEmpty(requestedOutfitId) then
            (false, null)
        else
            let outfitIds = _fsApi.GetOutfitIds().Value
            let correctedId = outfitIds.FirstOrDefault(fun outfitId -> outfitId.Equals(requestedOutfitId, StringComparison.OrdinalIgnoreCase))
            (correctedId <> null, correctedId)
            
    member val private _data: OutfitDataModel = null with get, set
    
    member val private _cpConditionsReady = false with get, set
    
    member private this.LoadData(e: obj) =
        let isLocal = e.GetType().GetProperty("IsLocalPlayer")
        if isLocal = null || isLocal.GetValue(e) :?> bool then
            this._data <- Game1.content.Load<OutfitDataModel>(AssetName)

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
        
    member val private _lastEvent: Event = null with get, set
    
    member private this.OnUpdateTicked(e: UpdateTickedEventArgs) =
        let mutable _load = false
        if this._lastEvent <> null && Game1.CurrentEvent = null then _load <- true
        if not this._cpConditionsReady && _cpApi.IsConditionsApiReady then
            this._cpConditionsReady <- true
            _load <- true
        if _load then
            this.LoadData(e)
        this._lastEvent <- Game1.CurrentEvent

    member private this.OnAssetRequested(e: AssetRequestedEventArgs) =
        if e <> null && e.Name <> null && e.Name.IsEquivalentTo(AssetName) then
            e.LoadFrom(Func<obj>(fun() -> 
                let baseData = OutfitDataModel()
                baseData["RequestedOutfit"] <- OutfitData(String.Empty)
                baseData)
            , AssetLoadPriority.Medium)

    member private this._requestedOutfitId: string =
        this._data <- Game1.content.Load<OutfitDataModel>(AssetName)
        this._data["RequestedOutfit"].OutfitId
           
    member val private _seenInvalids = [] with get, set
    
    member private this.UpdateOutfit() =
        let requestedOutfitId = this._requestedOutfitId
        let currentOutfitPair = _fsApi.GetCurrentOutfitId();
        let valid, correctedId = IsValid(requestedOutfitId)
        if valid then
            if not currentOutfitPair.Key || correctedId <> currentOutfitPair.Value then
                _fsApi.SetCurrentOutfitId(correctedId, this.ModManifest) |> ignore
        else if not (List.exists(fun elem -> elem = requestedOutfitId) this._seenInvalids) && requestedOutfitId <> "" then
            this._seenInvalids <- requestedOutfitId :: this._seenInvalids
            this.Monitor.Log($"Given outfit with ID {requestedOutfitId} is invalid.")
            
    member private this.OnAssetReady(e: AssetReadyEventArgs) =
        if e <> null && e.Name <> null && e.Name.IsEquivalentTo(AssetName) then
            this.UpdateOutfit()
            
    override this.Entry(helper: IModHelper) =
        helper.Events.GameLoop.GameLaunched.Add(this.OnGameLaunched)
        helper.Events.GameLoop.DayStarted.Add(this.LoadData)
        helper.Events.GameLoop.TimeChanged.Add(this.LoadData)
        helper.Events.GameLoop.UpdateTicked.Add(this.OnUpdateTicked)
        helper.Events.Player.Warped.Add(this.LoadData)
        helper.Events.Content.AssetRequested.Add(this.OnAssetRequested)
        helper.Events.Content.AssetReady.Add(this.OnAssetReady)
