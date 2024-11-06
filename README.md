# CS2Gaming-ShopItem
 Shop Item Module for [CS2GamingAPI](https://github.com/oylsister/CS2GamingAPI/), After purchase any item in Shop-Core will trigger API Request from CS2GamingAPICore and set cooldown for that purchase.

[![Video](https://img.youtube.com/vi/bJz9z3PU_Os/maxresdefault.jpg)](https://www.youtube.com/watch?v=bJz9z3PU_Os)

## Requirement
- [Shop-Core](https://github.com/Ganter1234/Shop-Core)
- [CS2GamingAPI](https://github.com/oylsister/CS2GamingAPI/)

## Installation
- Simply drag all content in zip file into ``addons/counterstrikesharp/plugins/``

 On plugin load to the server, this plugin will start generate config file at ``addons/counterstrikesharp/configs/plugins/ShopItem/ShopItem.json``
 ```jsonc
{
  "ItemPrice": 5000 // price for purchase CS2GamingItem
}
 ```
