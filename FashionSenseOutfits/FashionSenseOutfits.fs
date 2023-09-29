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
            
    member val private _data = null with get, set
    
    member private this.LoadData(e: System.Object) =
        let isLocal = e.GetType().GetProperty("IsLocalPlayer")
        if isLocal = null || isLocal.GetValue(e) :?> bool then
            this.Helper.GameContent.InvalidateCache(AssetName) |> ignore

    member private this.OnGameLaunched(e: GameLaunchedEventArgs) =
        _fsApi <- this.Helper.ModRegistry.GetApi<IApi>("PeacefulEnd.FashionSense")
        _cpApi <- this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher")
        _cpApi.RegisterToken(this.ModManifest, 
            "CurrentOutfit", 
            System.Func<string[]>(fun () ->
                if not Context.IsWorldReady then
                    [| null |]
                else
                    let outfitPair = _fsApi.GetCurrentOutfitId()
                    [| if outfitPair.Key then outfitPair.Value else null |]
            )
        )
        this._data = Game1.content.Load<OutfitDataModel>(AssetName) |> ignore

    member val private LastEvent: Event = null with get, set

    member private this.OnUpdateTicked(e: UpdateTickedEventArgs) =
        if this.LastEvent <> null && Game1.CurrentEvent = null then this.LoadAndUpdate(e)
        this.LastEvent <- Game1.CurrentEvent

    member private this.OnAssetRequested(e: AssetRequestedEventArgs) =
        if e <> null && e.Name <> null && e.Name.IsEquivalentTo(AssetName) then
            e.LoadFrom(Func<obj>(fun() -> 
                let baseData = OutfitDataModel()
                baseData["RequestedOutfit"] <- OutfitData(String.Empty)
                baseData)
            , AssetLoadPriority.Medium)

    member private this.GetRequestedOutfitId() =
        if this._data <> null && this._data.ContainsKey("RequestedOutfit") then
            this._data["RequestedOutfit"].OutfitId
        else
            null
    
    member private this.UpdateOutfit() =
        let requestedOutfitId = this.GetRequestedOutfitId()
        if requestedOutfitId <> null then
            let currentOutfitPair = _fsApi.GetCurrentOutfitId();
            let valid, correctedId = IsValid(requestedOutfitId)
            if valid then
                if not currentOutfitPair.Key || correctedId <> currentOutfitPair.Value then
                    this.Monitor.Log($"Applying outfit with ID {correctedId} via Fashion Sense API...")
                    _fsApi.SetCurrentOutfitId(correctedId, this.ModManifest) |> ignore
                else
                    this.Monitor.Log($"Skipping because the outfit with ID {correctedId} is already equipped.")
            else
                this.Monitor.Log($"Given outfit with ID {requestedOutfitId} is invalid.")
            
    member private this.LoadAndUpdate(e: obj) =
        this.LoadData(e)
        let task = async {
            let timer = new Timers.Timer(5000)
            let event = Async.AwaitEvent(timer.Elapsed) |> Async.Ignore
                
            timer.Start()
            Async.RunSynchronously(event)
            this.UpdateOutfit()
        }
                
        Async.Start(task)
        
    member private this.OnAssetReady(e: AssetReadyEventArgs) =
        if e <> null && e.Name <> null && e.Name.IsEquivalentTo(AssetName) then
            this.UpdateOutfit()
            
    override this.Entry(helper: IModHelper) =
        helper.Events.GameLoop.GameLaunched.Add(this.OnGameLaunched)
        helper.Events.GameLoop.SaveLoaded.Add(this.LoadData)
        helper.Events.GameLoop.DayStarted.Add(this.LoadAndUpdate)
        helper.Events.GameLoop.TimeChanged.Add(this.LoadAndUpdate)
        helper.Events.GameLoop.UpdateTicked.Add(this.OnUpdateTicked)
        helper.Events.Player.Warped.Add(this.LoadAndUpdate)
        helper.Events.Content.AssetRequested.Add(this.OnAssetRequested)
        helper.Events.Content.AssetReady.Add(this.OnAssetReady)
