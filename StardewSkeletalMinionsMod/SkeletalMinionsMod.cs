using System;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley.TerrainFeatures;
using StardewValley.Menus;
using System.Collections.Generic;
using StardewValley.Objects;
using StardewValley.Locations;

namespace StardewSkeletalMinionsMod
{
    public class SkeletalMinionsMod : Mod
    {
        public static Mod mod;
        public static MinionTaskPool taskPool;
        public static ModConfig config;
        
        public override void Entry(IModHelper helper)
        {
            mod = this;
            taskPool = new MinionTaskPool();
            SkeletonWand.loadSkeletonWandTextures();

            config = Helper.ReadConfig<ModConfig>();

            StardewModdingAPI.Events.SaveEvents.AfterLoad += SaveEvents_AfterLoad;
            StardewModdingAPI.Events.ControlEvents.KeyPressed += ControlEvents_KeyPressed;
            StardewModdingAPI.Events.ControlEvents.MouseChanged += ControlEvents_MouseChanged;
            StardewModdingAPI.Events.SaveEvents.BeforeSave += SaveEvents_BeforeSave;
            StardewModdingAPI.Events.MenuEvents.MenuChanged += MenuEvents_MenuChanged;

            //helper.ConsoleCommands.Add("growallcrops", "Completely grow all crops in the current location.", GrowAllCrops);
            //helper.ConsoleCommands.Add("seeds", "Add seeds to your inventory.", AddSeedsToInventory);
            helper.ConsoleCommands.Add("wandmode", "Toggle Skeleton Wand wand mode.", toggleWandMode);
        }

        /* Add the skeleton wand to Marlon's store */
        private void MenuEvents_MenuChanged(object sender, StardewModdingAPI.Events.EventArgsClickableMenuChanged e)
        {
            if (e.NewMenu is ShopMenu)
            {
                ShopMenu shop = e.NewMenu as ShopMenu;
                if (shop.portraitPerson != null && shop.portraitPerson.name.Equals("Marlon"))
                {
                    bool completedSkeletonTask = Game1.stats.getMonstersKilled("Skeleton") + Game1.stats.getMonstersKilled("Skeleton Mage") >= 50;
                    if (completedSkeletonTask && !doesPlayerHaveSkeletonWandAnywhere())
                    {
                        Dictionary<Item, int[]> itemPriceAndStock = Helper.Reflection.GetPrivateValue<Dictionary<Item, int[]>>(shop, "itemPriceAndStock");
                        List<Item> forSale = Helper.Reflection.GetPrivateValue<List<Item>>(shop, "forSale");

                        SkeletonWand skeletonWand = new SkeletonWand();
                        itemPriceAndStock.Add(skeletonWand, new int[2] { config.SkeletonWandPrice, 1 });
                        forSale.Add(skeletonWand);
                    }
                }
            }
        }

        private bool doesPlayerHaveSkeletonWandAnywhere()
        {
            // check inventory
            foreach (Item item in Game1.player.items)
            {
                if (item is SkeletonWand) return true;
            }

            // check all chests, debris everywhere in the world
            foreach (GameLocation l in Game1.locations)
            {
                foreach (StardewValley.Object o in l.Objects.Values)
                {
                    if (o is Chest)
                    {
                        foreach (Item item in (o as Chest).items)
                        {
                            if (item is SkeletonWand) return true;
                        }
                    }
                }

                foreach (Debris debris in l.debris)
                {
                    if (debris.item is SkeletonWand) return true;
                }
            }

            return false;
        }

        private void toggleWandMode(string command, string[] args)
        {
            foreach (Item o in Game1.player.items) {
                if (o is SkeletonWand) {
                    (o as SkeletonWand).goToNextWandMode();
                }
            }
        }

        private void GrowAllCrops(string command, string[] args)
        {
            for (int tileX = 0; tileX < Game1.currentLocation.map.Layers[0].LayerWidth; ++tileX)
            {
                for (int tileY = 0; tileY < Game1.currentLocation.map.Layers[0].LayerHeight; ++tileY)
                {
                    Vector2 key = new Vector2(tileX, tileY);
                    if (Game1.currentLocation.terrainFeatures.ContainsKey(key))
                    {
                        if (Game1.currentLocation.terrainFeatures[key] is HoeDirt)
                        {
                            HoeDirt dirt = Game1.currentLocation.terrainFeatures[key] as HoeDirt;
                            if (dirt.crop != null) dirt.crop.growCompletely();
                        }
                    }
                }
            }
        }

        private void AddSeedsToInventory(string command, string[] args)
        {
            Game1.player.addItemToInventory(new StardewValley.Object(498, 999));
            Game1.player.addItemToInventory(new StardewValley.Object(369, 999));
        }

        private void SaveEvents_BeforeSave(object sender, EventArgs e)
        {
            // destroy all minions and return their items to their owners
            foreach (GameLocation l in Game1.locations)
            {
                for (int i = l.characters.Count - 1; i>=0; --i)
                {
                    if (l.characters[i] is SkeletalMinion)
                    {
                        SkeletalMinion minion = l.characters[i] as SkeletalMinion;
                        minion.returnItemsToOwner();
                        l.characters.Remove(minion);
                    }
                }
            }
        }

        private void ControlEvents_MouseChanged(object sender, StardewModdingAPI.Events.EventArgsMouseStateChanged e)
        {
            if (!Context.IsWorldReady) return;

            if (e.PriorState.MiddleButton == ButtonState.Pressed)
            {
                if (e.NewState.MiddleButton == ButtonState.Released && Game1.player.CurrentTool != null && Game1.player.CurrentTool is SkeletonWand && Game1.activeClickableMenu == null)
                {
                    (Game1.player.CurrentTool as SkeletonWand).goToNextWandMode();
                }
            }
            
        }

        private void ControlEvents_KeyPressed(object sender, StardewModdingAPI.Events.EventArgsKeyPressed e)
        {
            //if (!Context.IsWorldReady) return;

            //// place fully grown crops to test harvesting capabilities
            //if (e.KeyPressed == Keys.B)
            //{
            //    if (Game1.player.ActiveObject==null || (Game1.player.ActiveObject != null && Game1.player.ActiveObject.category != StardewValley.Object.SeedsCategory))
            //    {
            //        Game1.showRedMessage("Please select a seed as your active object.");
            //        return;
            //    }
            //    else
            //    {
            //        int seedIndex = Game1.player.ActiveObject.parentSheetIndex;

            //        for (int tileX = 0; tileX < Game1.currentLocation.map.Layers[0].LayerWidth; ++tileX)
            //        {
            //            for (int tileY = 0; tileY < Game1.currentLocation.map.Layers[0].LayerHeight; ++tileY)
            //            {
            //                if (Game1.currentLocation.doesTileHaveProperty(tileX, tileY, "Diggable", "Back") != null)
            //                {
            //                    Vector2 key = new Vector2(tileX, tileY);
            //                    Game1.currentLocation.makeHoeDirt(key);

            //                    if (Game1.currentLocation.terrainFeatures.ContainsKey(key))
            //                    {
            //                        HoeDirt dirt = Game1.currentLocation.terrainFeatures[key] as HoeDirt;
            //                        if (dirt != null/* && dirt.canPlantThisSeedHere(seedIndex, tileX, tileY)*/)
            //                        {
            //                            dirt.plant(seedIndex, tileX, tileY, Game1.player);
            //                            dirt.crop.growCompletely();
            //                        }
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}

            //if (e.KeyPressed == Keys.Z)
            //{
            //    this.Monitor.Log($"Your location: ({Game1.player.getTileLocationPoint()})");

            //}
        }

        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            //if (Game1.player.hasItemWithNameThatContains("Skeleton Wand") == null)
                //Game1.player.addItemToInventoryBool(new SkeletonWand());
        }
    }
}
