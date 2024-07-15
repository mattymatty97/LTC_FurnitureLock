FurnitureLock
============
[![GitHub Release](https://img.shields.io/github/v/release/mattymatty97/LTC_FurnitureLock?display_name=release&logo=github&logoColor=white)](https://github.com/mattymatty97/LTC_FurnitureLock/releases/latest)
[![GitHub Pre-Release](https://img.shields.io/github/v/release/mattymatty97/LTC_FurnitureLock?include_prereleases&display_name=release&logo=github&logoColor=white&label=preview)](https://github.com/mattymatty97/LTC_FurnitureLock/releases)  
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/mattymatty/FurnitureLock?style=flat&logo=thunderstore&logoColor=white&label=thunderstore)](https://thunderstore.io/c/lethal-company/p/mattymatty/FurnitureLock/)

### Nail Furniture into place

## Features:
- set default position to any movable furniture piece
- lock them into place ( prevent moving and/or storing )
- LethalConfig integration for easily copying positions
- Fully Server-Only, no client installation required

## Expected behaviour:
- ### furniture at default config:
  - no changes from vanilla
- ### furniture unlocked but with custom values
  - furniture will be placed at custom values only the first time it is spawned
- ### furniture locked
  - furniture will always be placed at the custom values ( if values are default will spawn at vanilla default )
  - any attempt at moving the furniture will result in it returning at the custom values
  - any attempt at storing the furniture will fail

## Notes:
- ### config change
  - all changes will apply immediately ( no restart required )
  - to apply lock try and move the furniture

Installation
------------

- Install [BepInEx](https://thunderstore.io/c/lethal-company/p/BepInEx/BepInExPack/)
- Unzip this mod into your `BepInEx/plugins` folder

Or use the mod manager to handle the installing for you.
