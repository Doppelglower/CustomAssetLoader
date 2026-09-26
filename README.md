***CustomAssetLoader***

Allows the loading of your own custom bundles completely independent from anything in game (unlike Motions)

**What can currently be replaced/added?**

- Gameobjects
- Sprites
- BattleEffectLists
- FMod banks
- Shaders (immersive plagiarism reference)

**How do I utilize this in my mod?**

All bundles must reside within a mod's `custom_bundles` folder.

Alongside this, each bundle must come with a `asset_manifest.json` file, which can then mark objects and match them to the previously discussed types. (For custom Sounds, use `custom_banks` instead, or just use CSound you MF)

**How do I create a manifest?**

- The example's on the Github, but for convenience, I will go over it:

```json
{
  "overrides": [
    {
      "label": "SD_Personality",
      "resourceId": "10805_Ishmeal_FairyAppearance",
      "bundle": "__data",
      "assetPath": "Assets/Resources_moved/Prefab/SD/Personality/10805_Ishmeal_FairyAppearance.prefab",
      "assetType": "GameObject"
    }
  ]
}
```

Label: The category of the object (i.e abnormalities, personalities, etc etc)
resourceId: The specific ID that is sent to the game (for custom IDs, you use "SD_Personality" on the label, then input your resourceId)
bundle: The bundle name within the folder.
assetPath: The path inside of the bundle that leads to the requested asset.
assetType: The previous types of GameObject, Sprite, BattleEffectList, etc. Defaults to GameObject.

**How do I find which Label I need???**

Labels and resourceId pairs are used in game, so you can either look at existing mods or enable verbose logging in the config settings.

**How do I add FMod Sound banks?**

1. Build the FMOD project and export the `.bank` files.
2. Put them in the `custom_banks` folder.
3. Make sure the `.strings` is present alongside the bank, then you should be able to use them.

**How do I use shader remapping?**

Fawk you. Use immersive plagiarism because its cooler.
