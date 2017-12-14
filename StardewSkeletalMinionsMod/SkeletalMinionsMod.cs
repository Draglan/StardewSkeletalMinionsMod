using System;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley.TerrainFeatures;

namespace StardewSkeletalMinionsMod
{
    public class SkeletalMinionsMod : Mod
    {
        public static Mod mod;
        public static MinionTaskPool taskPool;
        
        public override void Entry(IModHelper helper)
        {
            mod = this;
            taskPool = new MinionTaskPool();
            SkeletonWand.loadSkeletonWandTextures();

            StardewModdingAPI.Events.SaveEvents.AfterLoad += SaveEvents_AfterLoad;
            StardewModdingAPI.Events.ControlEvents.KeyPressed += ControlEvents_KeyPressed;
            StardewModdingAPI.Events.ControlEvents.MouseChanged += ControlEvents_MouseChanged;
            StardewModdingAPI.Events.SaveEvents.BeforeSave += SaveEvents_BeforeSave;

            helper.ConsoleCommands.Add("growallcrops", "Completely grow all crops in the current location.", GrowAllCrops);
            helper.ConsoleCommands.Add("seeds", "Add seeds to your inventory.", AddSeedsToInventory);
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
            if (!Context.IsWorldReady) return;

            // place fully grown crops to test harvesting capabilities
            if (e.KeyPressed == Keys.B)
            {
                if (Game1.player.ActiveObject==null || (Game1.player.ActiveObject != null && Game1.player.ActiveObject.category != StardewValley.Object.SeedsCategory))
                {
                    Game1.showRedMessage("Please select a seed as your active object.");
                    return;
                }
                else
                {
                    int seedIndex = Game1.player.ActiveObject.parentSheetIndex;

                    for (int tileX = 0; tileX < Game1.currentLocation.map.Layers[0].LayerWidth; ++tileX)
                    {
                        for (int tileY = 0; tileY < Game1.currentLocation.map.Layers[0].LayerHeight; ++tileY)
                        {
                            if (Game1.currentLocation.doesTileHaveProperty(tileX, tileY, "Diggable", "Back") != null)
                            {
                                Vector2 key = new Vector2(tileX, tileY);
                                Game1.currentLocation.makeHoeDirt(key);

                                if (Game1.currentLocation.terrainFeatures.ContainsKey(key))
                                {
                                    HoeDirt dirt = Game1.currentLocation.terrainFeatures[key] as HoeDirt;
                                    if (dirt != null/* && dirt.canPlantThisSeedHere(seedIndex, tileX, tileY)*/)
                                    {
                                        dirt.plant(seedIndex, tileX, tileY, Game1.player);
                                        dirt.crop.growCompletely();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (e.KeyPressed == Keys.Z)
            {
                this.Monitor.Log($"Your location: ({Game1.player.getTileLocationPoint()})");

            }
        }

        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            if (Game1.player.hasItemWithNameThatContains("Skeleton Wand") == null)
                Game1.player.addItemToInventoryBool(new SkeletonWand());
        }
    }
}
