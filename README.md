# FashionSenseOutfits
FashionSenseOutfits adds a Content Patcher endpoint at nihilistzsche.FashionSenseOutfits/Outfits that you can target to change the current Fashion Sense outfit based on Content Patcher conditions and tokens.

It will do nothing on its own, you must have an additional Content Patcher content pack targeting the "nihilistzsche.FashionSenseOutfits/Outfits" endpoint. 

As of now it will only be able to change the outfit at the start of a new day, I may add additional functionality later to add more gradual control of the outfit.

The following example shows how to use it to set a seasonal outfit with the name "Spring", "Summer", "Fall", or "Winter".
Create a new mod folder.
Add a manifest.json file with the following template:
```json
{
   "Name": "[CP] <Your mod name>",
   "Author": "<Your name>",
   "Version": "0.1.1",
   "Description": "<Description of what the mod does>",
   "UniqueID": "<Your name>.<Mod name>",
   "MinimumApiVersion": "3.13.0",
   "UpdateKeys": [],
   "ContentPackFor": {
      "UniqueID": "Pathoschild.ContentPatcher",
      "MinimumVersion": "1.3.1"
   },
   "Dependencies": [
	{ "UniqueID": "nihilistzsche.FashionSenseOutfits", "IsRequired": true }
  ]
}
```
Replace everything that is inside < > with the proper information, do not have any < or > in the file.

Then add a content.json file that targets nihilistzsche.FashionSenseOutfits/Outfits, using the following template:
```json
{
  "Format": "1.29.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "nihilistzsche.FashionSenseOutfits/Outfits",
      "Fields": {
        "CurrentOutfit": {
          	"OutfitID": "{{Season}}"
        }
	  }
	}
  ]
}
```
Visit the Content Patcher website for more information on tokens, EditData actions, and conditions to see what you can do.

The OutfitID is matched using case insensitivity, and will unpredictably select one if you have outfits with the same name but different cases.
I think that is a rare enough situation that I can live with the caveat.