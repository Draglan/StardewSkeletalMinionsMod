using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Characters;
using StardewValley.Objects;

/* NOTE TO SELF ABOUT HARVESTING
 *  Crops can have one of two harvestMethods: 0 (picking) and 1 (scythe).
 *  The harvest() method (here with a custom implementation to add items 
 *  to the minion's inventory) returns true IF THE CROP SHOULD BE DESTROYED, 
 *  and returns false OTHERWISE.
 *  */


namespace StardewSkeletalMinionsMod
{
    public class HarvestingMinionTask : MinionTask
    {
        /* All of the crops to be harvested by this task. This should ideally be a small number,
         * and they should be right next to each other.*/
        private List<Point> cropLocations;
        private bool ownerReachedTask;

        private const int pickTime = 400;
        private int pickTimer;

        private bool animating;

        public int CropCount { get => cropLocations.Count; }

        public HarvestingMinionTask(GameLocation location, Point position, List<Point> cropLocations)
            : base(nameof(HarvestingMinionTask), position, location, null)
        {
            this.cropLocations = cropLocations;
        }

        public override void endBehavior(Character c, GameLocation location)
        {
            ownerReachedTask = true;
            pickTimer = pickTime;
        }

        public override bool isAtEnd(PathNode currentNode, Point endPoint, GameLocation location, Character c)
        {
            return Math.Sqrt(Math.Pow(currentNode.x - endPoint.X, 2) + Math.Pow(currentNode.y - endPoint.Y, 2)) <= 1;
        }

        protected override void updateTask(GameTime time, GameLocation location)
        {
            if (!ownerReachedTask && (cropLocations == null || cropLocations.Count == 0)) complete(time, location);

            // animate minion if harvesting
            if (animating && owner.sprite.Animate(time, 20, 4, 90))
            {
                animating = false;
                owner.sprite.CurrentFrame = 0;
            }

            if (ownerReachedTask)
            {
                pickTimer -= time.ElapsedGameTime.Milliseconds;

                // pick next crop
                if (pickTimer <= 0)
                {
                    if (cropLocations != null && cropLocations.Count > 0)
                    {
                        // get and remove the next crop location to be harvested
                        Point cropLoc = cropLocations[0];
                        cropLocations.RemoveAt(0);

                        // harvest the crop if there is one there
                        Vector2 key = new Vector2(cropLoc.X, cropLoc.Y);
                        if (location.terrainFeatures.ContainsKey(key))
                        {
                            if (location.terrainFeatures[key] is HoeDirt)
                            {
                                HoeDirt dirt = location.terrainFeatures[key] as HoeDirt;
                                if (dirt.readyForHarvest())
                                {
                                    if (customHarvest(dirt.crop, cropLoc.X, cropLoc.Y, dirt))
                                        dirt.destroyCrop(key, false);

                                    pickTimer = pickTime;
                                    animating = true;
                                }
                                else
                                    SkeletalMinionsMod.mod.Monitor.Log($"(?)Went through step w/o being able to harvest...");
                            }
                        }
                    }
                    else
                        complete(time, location);
                }
            }
        }


        public bool customHarvest(Crop crop, int xTile, int yTile, HoeDirt soil)
        {
            JunimoHarvester junimoHarvester = null;

            if (crop.dead)
                return junimoHarvester != null;
            if (crop.forageCrop)
            {
                StardewValley.Object @object = (StardewValley.Object)null;
                int howMuch = 3;
                if (crop.whichForageCrop == 1)
                    @object = new StardewValley.Object(399, 1, false, -1, 0);
                if (Game1.player.professions.Contains(16))
                    @object.quality = 4;
                else if (Game1.random.NextDouble() < (double)Game1.player.ForagingLevel / 30.0)
                    @object.quality = 2;
                else if (Game1.random.NextDouble() < (double)Game1.player.ForagingLevel / 15.0)
                    @object.quality = 1;
                Game1.stats.ItemsForaged += (uint)@object.Stack;
                if (junimoHarvester != null)
                {
                    junimoHarvester.tryToAddItemToHut((Item)@object);
                    return true;
                }
                if (owner.addItemToInventory(@object))
                {
                    Vector2 vector2 = new Vector2((float)xTile, (float)yTile);
                    //Game1.player.animateOnce(279 + Game1.player.facingDirection);
                    //Game1.player.canMove = false;
                    Game1.playSound("harvest");
                    DelayedAction.playSoundAfterDelay("coin", 260);
                    if (crop.regrowAfterHarvest == -1)
                    {
                        Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(17, new Vector2(vector2.X * (float)Game1.tileSize, vector2.Y * (float)Game1.tileSize), Color.White, 7, Game1.random.NextDouble() < 0.5, 125f, 0, -1, -1f, -1, 0));
                        Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(14, new Vector2(vector2.X * (float)Game1.tileSize, vector2.Y * (float)Game1.tileSize), Color.White, 7, Game1.random.NextDouble() < 0.5, 50f, 0, -1, -1f, -1, 0));
                    }
                    Game1.player.gainExperience(2, howMuch);
                    return true;
                }
                Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
            }
            else if (crop.currentPhase >= crop.phaseDays.Count - 1 && (!crop.fullyGrown || crop.dayOfCurrentPhase <= 0))
            {
                int num1 = 1;
                int num2 = 0;
                int num3 = 0;
                if (crop.indexOfHarvest == 0)
                    return true;
                Random random = new Random(xTile * 7 + yTile * 11 + (int)Game1.stats.DaysPlayed + (int)Game1.uniqueIDForThisGame);
                switch (soil.fertilizer)
                {
                    case 368:
                        num3 = 1;
                        break;
                    case 369:
                        num3 = 2;
                        break;
                }
                double num4 = 0.2 * ((double)Game1.player.FarmingLevel / 10.0) + 0.2 * (double)num3 * (((double)Game1.player.FarmingLevel + 2.0) / 12.0) + 0.01;
                double num5 = Math.Min(0.75, num4 * 2.0);
                if (random.NextDouble() < num4)
                    num2 = 2;
                else if (random.NextDouble() < num5)
                    num2 = 1;
                if (crop.minHarvest > 1 || crop.maxHarvest > 1)
                    num1 = random.Next(crop.minHarvest, Math.Min(crop.minHarvest + 1, crop.maxHarvest + 1 + Game1.player.FarmingLevel / crop.maxHarvestIncreasePerFarmingLevel));
                if (crop.chanceForExtraCrops > 0.0)
                {
                    while (random.NextDouble() < Math.Min(0.9, crop.chanceForExtraCrops))
                        ++num1;
                }
                if (crop.harvestMethod == 1)
                {
                    if (junimoHarvester == null)
                        DelayedAction.playSoundAfterDelay("daggerswipe", 150);
                    if (junimoHarvester != null && Utility.isOnScreen(junimoHarvester.getTileLocationPoint(), Game1.tileSize, junimoHarvester.currentLocation))
                        Game1.playSound("harvest");
                    if (junimoHarvester != null && Utility.isOnScreen(junimoHarvester.getTileLocationPoint(), Game1.tileSize, junimoHarvester.currentLocation))
                        DelayedAction.playSoundAfterDelay("coin", 260);
                    for (int index = 0; index < num1; ++index)
                    {
                        if (junimoHarvester != null)
                            junimoHarvester.tryToAddItemToHut((Item)new StardewValley.Object(crop.indexOfHarvest, 1, false, -1, num2));
                        else
                            Game1.createObjectDebris(crop.indexOfHarvest, xTile, yTile, -1, num2, 1f, (GameLocation)null);
                    }
                    if (crop.regrowAfterHarvest == -1)
                        return true;
                    crop.dayOfCurrentPhase = crop.regrowAfterHarvest;
                    crop.fullyGrown = true;
                }
                else
                {
                    if (junimoHarvester == null)
                    {
                        StardewValley.Farmer player = Game1.player;
                        StardewValley.Object @object;
                        if (!crop.programColored)
                        {
                            @object = new StardewValley.Object(crop.indexOfHarvest, 1, false, -1, num2);
                        }
                        else
                        {
                            @object = (StardewValley.Object)new ColoredObject(crop.indexOfHarvest, 1, crop.tintColor);
                            int num6 = num2;
                            @object.quality = num6;
                        }
                        int num7 = 0;
                        if (!owner.addItemToInventory(@object))
                        {
                            Game1.showRedMessage(Game1.content.LoadString("Strings\\StringsFromCSFiles:Crop.cs.588"));
                            goto label_86;
                        }
                    }
                    Vector2 vector2 = new Vector2((float)xTile, (float)yTile);
                    if (junimoHarvester == null)
                    {
                        //Game1.player.animateOnce(279 + Game1.player.facingDirection);
                        //Game1.player.canMove = false;
                    }
                    else
                    {
                        JunimoHarvester junimoHarvester1 = junimoHarvester;
                        StardewValley.Object @object;
                        if (!crop.programColored)
                        {
                            @object = new StardewValley.Object(crop.indexOfHarvest, 1, false, -1, num2);
                        }
                        else
                        {
                            @object = (StardewValley.Object)new ColoredObject(crop.indexOfHarvest, 1, crop.tintColor);
                            int num6 = num2;
                            @object.quality = num6;
                        }
                        junimoHarvester1.tryToAddItemToHut((Item)@object);
                    }
                    if (random.NextDouble() < (double)Game1.player.LuckLevel / 1500.0 + Game1.dailyLuck / 1200.0 + 9.99999974737875E-05)
                    {
                        num1 *= 2;
                        if (junimoHarvester == null || Utility.isOnScreen(junimoHarvester.getTileLocationPoint(), Game1.tileSize, junimoHarvester.currentLocation))
                            Game1.playSound("dwoop");
                    }
                    else if (crop.harvestMethod == 0)
                    {
                        if (junimoHarvester == null || Utility.isOnScreen(junimoHarvester.getTileLocationPoint(), Game1.tileSize, junimoHarvester.currentLocation))
                            Game1.playSound("harvest");
                        if (junimoHarvester == null || Utility.isOnScreen(junimoHarvester.getTileLocationPoint(), Game1.tileSize, junimoHarvester.currentLocation))
                            DelayedAction.playSoundAfterDelay("coin", 260);
                        if (crop.regrowAfterHarvest == -1 && (junimoHarvester == null || junimoHarvester.currentLocation.Equals((object)Game1.currentLocation)))
                        {
                            Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(17, new Vector2(vector2.X * (float)Game1.tileSize, vector2.Y * (float)Game1.tileSize), Color.White, 7, Game1.random.NextDouble() < 0.5, 125f, 0, -1, -1f, -1, 0));
                            Game1.currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(14, new Vector2(vector2.X * (float)Game1.tileSize, vector2.Y * (float)Game1.tileSize), Color.White, 7, Game1.random.NextDouble() < 0.5, 50f, 0, -1, -1f, -1, 0));
                        }
                    }
                    if (crop.indexOfHarvest == 421)
                    {
                        crop.indexOfHarvest = 431;
                        num1 = random.Next(1, 4);
                    }
                    for (int index = 0; index < num1 - 1; ++index)
                    {
                        if (junimoHarvester == null)
                            Game1.createObjectDebris(crop.indexOfHarvest, xTile, yTile, -1, 0, 1f, (GameLocation)null);
                        else
                            junimoHarvester.tryToAddItemToHut((Item)new StardewValley.Object(crop.indexOfHarvest, 1, false, -1, 0));
                    }
                    float num8 = (float)(16.0 * Math.Log(0.018 * (double)Convert.ToInt32(Game1.objectInformation[crop.indexOfHarvest].Split('/')[1]) + 1.0, Math.E));
                    if (junimoHarvester == null)
                        Game1.player.gainExperience(0, (int)Math.Round((double)num8));
                    if (crop.regrowAfterHarvest == -1)
                        return true;
                    crop.dayOfCurrentPhase = crop.regrowAfterHarvest;
                    crop.fullyGrown = true;
                }
            }
            label_86:
            return false;
        }

        private string invString()
        {
            string objStr(StardewValley.Object o)
            {
                string quality = "";
                switch (o.quality) {
                    case 0: quality = "regular"; break;
                    case 1: quality = "silver"; break;
                    case 2: quality = "gold"; break;
                    case 3: quality = "iridium"; break;
                }
                return o.Name + "x" + o.Stack + ":" + quality;
            }

            string inv = "inventory{ ";
            if (owner.inventory == null) return "null_inventory";
            if (owner.inventory.Count > 0)
                inv += objStr(owner.inventory[0]);

            for (int i=1; i<owner.inventory.Count; ++i)
            {
                inv += ", " + objStr(owner.inventory[i]);
            }

            return inv + " }";
        }
    }
}
