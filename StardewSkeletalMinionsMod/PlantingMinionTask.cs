using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using xTile.Dimensions;

namespace StardewSkeletalMinionsMod
{
    public class PlantingMinionTask : MinionTask
    {

        public enum SprinklerTypes
        {
            Standard, Quality, Iridium
        }

        private enum Stages
        {
            Walking, Hoeing, Watering, Fertilizing, Planting, Done
        }

        private SprinklerTypes sprinklerType;

        private int seedIndex;
        private int fertilizerIndex;

        private const int stageTime = 1000;
        private int stageTimer;
        Stages stage;
        bool castingAnimation;

        public int SeedIndex { get => seedIndex; }
        public int FertilizerIndex { get => fertilizerIndex; }

        private static bool[,] standardSprinklerMap = {
            { false,false,false,false,false },
            { false,false,true,false,false },
            { false,true,false,true,false },
            { false,false,true,false,false },
            { false,false,false,false,false }
        };

        private static bool[,] qualitySprinklerMap = {
            { false,false,false,false,false },
            {  true,true,true,true,true },
            { false,true,false,true,false },
            { true,true,true,true,true },
            { false,false,false,false,false }
        };

        private static bool[,] iridiumSprinklerMap = {
            { true,true,true,true,true },
            { true,true,true,true,true },
            { true,true,false,true,true },
            { true,true,true,true,true },
            { true,true,true,true,true }
        };

        public PlantingMinionTask(GameLocation location, Point position, SprinklerTypes sprinklerType, int seedsRequired, int seedIndex, int fertilizerIndex)
            : base(nameof(PlantingMinionTask), position, location, new List<KeyValuePair<int, int>>())
        {
            this.sprinklerType = sprinklerType;
            stage = Stages.Walking;
            requiredItems.Add(new KeyValuePair<int, int>(seedIndex, seedsRequired)); // note that fertilizer is optional
            this.seedIndex = seedIndex;
            this.fertilizerIndex = fertilizerIndex;
        }


        protected override void updateTask(GameTime time, GameLocation location)
        {

            if (castingAnimation && owner.sprite.Animate(time, 20, 4, 150))
            {
                castingAnimation = false;
                owner.sprite.CurrentFrame = 0;
            }

            if (stage != Stages.Walking && stage != Stages.Done)
            {
                stageTimer -= time.ElapsedGameTime.Milliseconds;
                
                if (stageTimer <= 0)
                {
                    castingAnimation = true;
                    performStage();
                    stageTimer = stageTime;
                }
            }
            else if (stage == Stages.Done)
            {
                stageTimer -= time.ElapsedGameTime.Milliseconds;

                if (stageTimer <= 0)
                {
                    // mark the task as complete
                    complete(time, location);
                }
            }
        } 

        public override bool isAtEnd(PathNode currentNode, Point endPoint, GameLocation location, Character c)
        {
            return Math.Sqrt( Math.Pow(currentNode.x - endPoint.X, 2) + Math.Pow(currentNode.y - endPoint.Y, 2) ) <= 1;
        }

        public override void endBehavior(Character c, GameLocation location)
        {
            stage = Stages.Hoeing;
            stageTimer = stageTime;
        }

        protected override void onTaskComplete(GameTime time, GameLocation location)
        {
        }

        public static bool[,] getSprinklerMapFromType(SprinklerTypes sprinkler)
        {
            bool[,] map;
            switch (sprinkler)
            {
                case SprinklerTypes.Standard:
                    map = standardSprinklerMap;
                    break;

                case SprinklerTypes.Quality:
                    map = qualitySprinklerMap;
                    break;

                case SprinklerTypes.Iridium:
                    map = iridiumSprinklerMap;
                    break;

                default:
                    map = iridiumSprinklerMap;
                    break;
            }

            return map;
        }

        private void plantAtCurrentSprinkler()
        {
            if (owner.getItemInInventoryCount(seedIndex) <= 0) return;

            // choose which sprinkler type to base our hoeing pattern on
            bool[,] map = getSprinklerMapFromType(sprinklerType);
            bool done = false;

            for (int tileX = position.X - 2; tileX <= position.X + 2 && !done; ++tileX)
            {
                for (int tileY = position.Y - 2; tileY <= position.Y + 2 && !done; ++tileY)
                {
                    if (map[tileX - position.X + 2, tileY - position.Y + 2])
                    {
                        Vector2 key = new Vector2(tileX, tileY);

                        if (owner.currentLocation.terrainFeatures.ContainsKey(key))
                        {
                            if (owner.currentLocation.terrainFeatures[key] != null && owner.currentLocation.terrainFeatures[key] is HoeDirt)
                            {
                                HoeDirt dirt = (HoeDirt)owner.currentLocation.terrainFeatures[key];

                                if (dirt.canPlantThisSeedHere(seedIndex, tileX, tileY))
                                {
                                    if (owner.getItemInInventoryCount(seedIndex) > 0)
                                    {
                                        if (dirt.plant(seedIndex, tileX, tileY, owner.farmerOwner))
                                            owner.subtractItemFromInventory(seedIndex, 1);
                                    }

                                    // if the minion runs out of seeds, stop planting
                                    if (owner.getItemInInventoryCount(seedIndex) <= 0)
                                    {
                                        Game1.showRedMessage("One of your minions has run out of seeds.");
                                        done = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Game1.playSound("yoba");
        }

        private void hoeAtCurrentSprinkler()
        {
            // choose which sprinkler type to base our hoeing pattern on
            bool[,] map = getSprinklerMapFromType(sprinklerType);

            // Hoe the tiles around the sprinkler, ignoring those tiles which are not part of the sprinkler's
            // pattern.
            for (int tileX = position.X - 2; tileX <= position.X + 2; ++tileX)
            {
                for (int tileY = position.Y - 2; tileY <= position.Y + 2; ++tileY)
                {
                    if (map[tileX - position.X + 2, tileY - position.Y + 2])
                    {
                        // hoe the dirt at this tile if able
                        if (owner.currentLocation.doesTileHaveProperty(tileX, tileY, "Diggable", "Back") != null
                            && !customIsTileOccupied(owner.currentLocation, new Vector2(tileX, tileY), owner.name)
                            && owner.currentLocation.isTilePassable(new Location(tileX, tileY), Game1.viewport))
                        {
                            owner.currentLocation.terrainFeatures.Add(new Vector2(tileX, tileY), new HoeDirt(Game1.isRaining && owner.currentLocation.isOutdoors ? 1 : 0));
                        }
                    }
                }
            }

            Game1.playSound("hoeHit");
        }

        private void waterAtCurrentSprinkler()
        {
            bool[,] map = getSprinklerMapFromType(sprinklerType);

            for (int tileX = position.X - 2; tileX <= position.X + 2; ++tileX)
            {
                for (int tileY = position.Y - 2; tileY <= position.Y + 2; ++tileY)
                {
                    if (map[tileX - position.X + 2, tileY - position.Y + 2])
                    {
                        Vector2 key = new Vector2(tileX, tileY);

                        if (owner.currentLocation.terrainFeatures.ContainsKey(key))
                            if (owner.currentLocation.terrainFeatures[key] != null && owner.currentLocation.terrainFeatures[key] is HoeDirt)
                                (owner.currentLocation.terrainFeatures[key] as HoeDirt).state = 1;
                    }
                }
            }

            Game1.playSound("wateringCan");
        }

        private void fertilizeAtCurrentSprinkler()
        {
            if (owner.getItemInInventoryCount(fertilizerIndex) <= 0) return;

            bool[,] map = getSprinklerMapFromType(sprinklerType);
            bool done = false;

            for (int tileX = position.X - 2; tileX <= position.X + 2 && !done; ++tileX)
            {
                for (int tileY = position.Y - 2; tileY <= position.Y + 2 && !done; ++tileY)
                {
                    if (map[tileX - position.X + 2, tileY - position.Y + 2])
                    {
                        Vector2 key = new Vector2(tileX, tileY);

                        if (owner.currentLocation.terrainFeatures.ContainsKey(key))
                        {
                            if (owner.currentLocation.terrainFeatures[key] != null && owner.currentLocation.terrainFeatures[key] is HoeDirt)
                            {
                                HoeDirt dirt = (HoeDirt)owner.currentLocation.terrainFeatures[key];

                                if (dirt.canPlantThisSeedHere(seedIndex, tileX, tileY))
                                {
                                    if (owner.getItemInInventoryCount(fertilizerIndex) > 0)
                                    {
                                        if (dirt.plant(fertilizerIndex, tileX, tileY, owner.farmerOwner, true))
                                            owner.subtractItemFromInventory(fertilizerIndex, 1);
                                    }

                                    // if the minion runs out of fertilizer, stop planting but don't completely stop the minion
                                    if (owner.getItemInInventoryCount(fertilizerIndex) <= 0)
                                    {
                                        Game1.showRedMessage("One of your minions has run out of fertilizer.");
                                        done = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Game1.playSound("yoba");
        }

        //Perform stage and advance to next stage
        private void performStage()
        {
            switch (stage)
            {
                case Stages.Hoeing:
                    hoeAtCurrentSprinkler();
                    stage = Stages.Watering;
                    break;

                case Stages.Watering:
                    waterAtCurrentSprinkler();
                    stage = Stages.Fertilizing;
                    break;

                case Stages.Fertilizing:
                    fertilizeAtCurrentSprinkler();
                    stage = Stages.Planting;
                    break;

                case Stages.Planting:
                    plantAtCurrentSprinkler();
                    stage = Stages.Done;
                    break;

                default:
                    break;
            }
        }

        // "Custom" implementation of isTileOccupied; allows us to ignore characters with the given name.
        // This is identical to the base isTileOccupied method, but is necessary since some GameLocation
        // inheritors override it with slightly different functionality.
        private bool customIsTileOccupied(GameLocation location, Vector2 tileLocation, string characterToIgnore = "")
        {
            StardewValley.Object @object;
            location.objects.TryGetValue(tileLocation, out @object);
            Microsoft.Xna.Framework.Rectangle rectangle = new Microsoft.Xna.Framework.Rectangle((int)tileLocation.X * Game1.tileSize + 1, (int)tileLocation.Y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2);
            Microsoft.Xna.Framework.Rectangle boundingBox;
            for (int index = 0; index < location.characters.Count; ++index)
            {
                if (location.characters[index] != null && !location.characters[index].name.Equals(characterToIgnore))
                {
                    boundingBox = location.characters[index].GetBoundingBox();
                    if (boundingBox.Intersects(rectangle))
                        return true;
                }
            }
            if (location.terrainFeatures.ContainsKey(tileLocation) && rectangle.Intersects(location.terrainFeatures[tileLocation].getBoundingBox(tileLocation)))
                return true;
            if (location.largeTerrainFeatures != null)
            {
                foreach (LargeTerrainFeature largeTerrainFeature in location.largeTerrainFeatures)
                {
                    boundingBox = largeTerrainFeature.getBoundingBox();
                    if (boundingBox.Intersects(rectangle))
                        return true;
                }
            }
            return @object != null;
        }

    }
}
