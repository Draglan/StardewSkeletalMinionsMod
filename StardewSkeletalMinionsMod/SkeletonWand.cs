using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;
using System.Diagnostics;
using System.Xml.Serialization;
using CustomElementHandler;
using StardewValley.Tools;
using StardewValley.Objects;

/*
    LESSONS LEARNED :D
    ------------------
    (1) Not all maps have all layers. For example, the 
        greenhouse does not have a "Paths" layer.
*/

namespace StardewSkeletalMinionsMod
{
    public class SkeletonWand : Tool, ISaveElement
    {
        private static Texture2D wandTexture;
        private static Texture2D seedAttachmentTexture;
        private static Texture2D fertilizerAttachmentTexture;
        public WandMode wandMode;

        private struct SprinklerInfo
        {
            public Point position;
            public PlantingMinionTask.SprinklerTypes type;
            public int seedsRequired;
        }

        /* Modes of functionality for the wand */
        public enum WandMode
        {
            Planting, Harvesting
        }

        public SkeletonWand()
        {
            build();
        }

        public static void loadSkeletonWandTextures()
        {
            wandTexture = SkeletalMinionsMod.mod.Helper.Content.Load<Texture2D>(@"Assets/skeletonwand.png");
            seedAttachmentTexture = SkeletalMinionsMod.mod.Helper.Content.Load<Texture2D>(@"Assets/seedattachment.png");
            fertilizerAttachmentTexture = SkeletalMinionsMod.mod.Helper.Content.Load<Texture2D>(@"Assets/fertilizerattachment.png");
        }

        protected override string loadDescription()
        {
            return "An ancient and powerful wand.";
        }

        protected override string loadDisplayName()
        {
            string s = "Skeleton Wand";
            switch (wandMode)
            {
                case WandMode.Harvesting:
                    s += " (Harvest)";
                    break;

                case WandMode.Planting:
                    s += " (Plant)";
                    break;
            }

            return s;
        }

        public override bool canBeDropped()
        {
            return false;
        }

        public override bool canBeGivenAsGift()
        {
            return false;
        }

        public override bool canBeTrashed()
        {
            return false;
        }

        public override void DoFunction(GameLocation location, int x, int y, int power, StardewValley.Farmer who)
        {
            switch (wandMode)
            {
                case WandMode.Planting:
                {
                    if (location.name.Equals("Greenhouse") || location.name.Equals("Farm"))
                    {
                        if (attachments[0] != null)
                        {
                            // get seed, fertilizer count
                            int numSeeds = attachments[0].Stack;
                            int seedIndex = attachments[0].parentSheetIndex;
                            int numFertilizer = 0;
                            int fertilizerIndex = 0;

                            // can't plant if the season is wrong
                            Crop crop = new Crop(seedIndex, 0, 0);
                            if (!location.name.Equals("Greenhouse") && !crop.seasonsToGrowIn.Contains(Game1.currentSeason))
                            {
                                Game1.showRedMessage("You can't grow that seed outside at this time of year.");
                                return;
                            }

                            // get fertilizer info if any is attached
                            if (attachments[1] != null)
                            {
                                numFertilizer = attachments[1].Stack;
                                fertilizerIndex = attachments[1].parentSheetIndex;
                            }

                            // spawn the skeletons
                            spawnMinionsForPlanting(location, who, seedIndex, ref numSeeds, fertilizerIndex, ref numFertilizer);

                            // update seed, fertilizer stack count in wand to reflect what was taken by the minions
                            if (numSeeds <= 0) attachments[0] = null;
                            else attachments[0].Stack = numSeeds;

                            if (attachments[1] != null)
                            {
                                if (numFertilizer <= 0) attachments[1] = null;
                                else attachments[1].Stack = numFertilizer;
                            }
                        }
                        else
                            Game1.showRedMessage("The Skeleton Wand needs seeds.");
                    }
                    else
                        Game1.showRedMessage("The wand doesn't seem to do much here.");
                    break;
                }

                case WandMode.Harvesting:
                    spawnMinionsForHarvesting(location, who);
                    break;
            }
        }

        private void spawnMinionsForPlanting(GameLocation location, StardewValley.Farmer who, int seedIndex, ref int numSeeds, int fertilizerIndex, ref int numFertilizer)
        {
            const int sprinklersPerMinion = 15;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            // clear previous tasks
            SkeletalMinionsMod.taskPool.clearTasks(location,
                new List<string>
            {
                nameof(PlantingMinionTask)
            });

            // find all eligible sprinklers
            List<SprinklerInfo> sprinklers = findAllSprinklers(location);

            int numSkeletons = sprinklers.Count / sprinklersPerMinion;
            if (numSkeletons == 0) numSkeletons = 1;

            int totalSeedCount = numSeeds;
            int totalFertilizerCount = numFertilizer;

            // create tasks from sprinklers
            foreach (SprinklerInfo sprinkler in sprinklers)
            {
                PlantingMinionTask task = new PlantingMinionTask(location, sprinkler.position, sprinkler.type, sprinkler.seedsRequired, seedIndex, fertilizerIndex);
                SkeletalMinionsMod.taskPool.addTask(task);
            }

            // spawn skeletons
            for (int i=0; i<numSkeletons; ++i)
            {
                Point spawnPoint = new Point();
                if (!findNearestSpawnLocation(who.getTileLocationPoint(), location, ref spawnPoint)) continue;
                SkeletalMinion minion = new SkeletalMinion(new Vector2(spawnPoint.X, spawnPoint.Y)*Game1.tileSize, location, who, SkeletalMinionsMod.taskPool, new List<string> { nameof(PlantingMinionTask) });

                int seedsToAdd = Math.Min(totalSeedCount / numSkeletons, numSeeds);
                int fertilizerToAdd = Math.Min(totalFertilizerCount / numSkeletons, numFertilizer);

                numSeeds -= seedsToAdd;
                numFertilizer -= fertilizerToAdd;

                if (seedsToAdd > 0)
                    minion.addItemToInventory(new StardewValley.Object(seedIndex, seedsToAdd));
                if (fertilizerToAdd > 0)
                    minion.addItemToInventory(new StardewValley.Object(fertilizerIndex, fertilizerToAdd));

                location.characters.Add(minion);
            }
            sw.Stop();
            SkeletalMinionsMod.mod.Monitor.Log($"Spawning (planting) minions took {sw.ElapsedMilliseconds} ms.");
        }

        List<SprinklerInfo> findAllSprinklers(GameLocation location)
        {
            List<SprinklerInfo> sprinklers = new List<SprinklerInfo>();

            // search through the map looking for sprinklers
            for (int tileX = 0; tileX < location.map.Layers[0].LayerWidth; ++tileX)
            {
                for (int tileY = 0; tileY < location.map.Layers[0].LayerHeight; ++tileY)
                {
                    Vector2 key = new Vector2(tileX, tileY);
                    if (location.Objects.ContainsKey(key))
                    {
                        StardewValley.Object obj = location.Objects[key];

                        // if the object is a sprinkler, classify it and, if eligible, add it to the list
                        if (obj.Name.Contains("Sprinkler"))
                        {
                            SprinklerInfo candidate = new SprinklerInfo();
                            candidate.position = new Point(tileX, tileY);

                            // classify candidate
                            if (obj.Name.Contains("Quality"))
                                candidate.type = PlantingMinionTask.SprinklerTypes.Quality;
                            else if (obj.Name.Contains("Iridium"))
                                candidate.type = PlantingMinionTask.SprinklerTypes.Iridium;
                            else
                                candidate.type = PlantingMinionTask.SprinklerTypes.Standard;

                            // determine if eligible (that is, the sprinkler isn't completely surrounded by crops already)
                            bool[,] map = PlantingMinionTask.getSprinklerMapFromType(candidate.type);
                            for (int x = candidate.position.X - 2; x <= candidate.position.X + 2; ++x)
                            {
                                for (int y = candidate.position.Y - 2; y <= candidate.position.Y + 2; ++y)
                                {
                                    if (map[x - candidate.position.X + 2, y - candidate.position.Y + 2])
                                    {
                                        if (location.doesTileHaveProperty(x, y, "Diggable", "Back") != null)
                                        {
                                            Vector2 pos = new Vector2(x, y);

                                            //if (location.isTileLocationTotallyClearAndPlaceable(x,y))
                                            if ((!location.terrainFeatures.ContainsKey(pos) ||
                                                (location.isTileHoeDirt(pos) && !location.isCropAtTile(x, y))) &&
                                                !location.Objects.ContainsKey(pos) && 
                                                location.map.GetLayer("Paths")?.PickTile(new xTile.Dimensions.Location(x, y), Game1.viewport.Size) == null &&
                                                location.isTilePassable(new xTile.Dimensions.Location(x,y), Game1.viewport))
                                            {
                                                candidate.seedsRequired++;
                                            }
                                        }
                                    }
                                }
                            }

                            if (candidate.seedsRequired > 0)
                            {
                                sprinklers.Add(candidate);
                            }
                        }
                    }
                }
            }

            return sprinklers;
        }

        bool findNearestSpawnLocation(Point fromHere, GameLocation location, ref Point spawnPoint, int limit=25)
        {
            StardewValley.Monsters.Skeleton exemplar = new StardewValley.Monsters.Skeleton(new Vector2(0, 0));
            Queue<Point> points = new Queue<Point>();
            Dictionary<Point, bool> visited = new Dictionary<Point, bool>();

            visited.Add(fromHere, true);
            points.Enqueue(fromHere);
            int count = 0;

            while (points.Count > 0 && count++ < limit)
            {
                Point point = points.Dequeue();

                // check & enqueue 8 surrounding neighbors
                for (int tileX = point.X-1; tileX<=point.X+1; tileX++)
                {
                    for (int tileY = point.Y-1; tileY<=point.Y+1; tileY++)
                    {
                        if (tileX != point.X || tileY != point.Y)
                        {
                            Point key = new Point(tileX, tileY);
                            if (!visited.ContainsKey(key))
                            {
                                // check if this spawn point is eligible; if so, return it
                                if (!location.isCollidingPosition(new Rectangle(key.X * Game1.tileSize + 1, key.Y * Game1.tileSize + 1, Game1.tileSize - 2, Game1.tileSize - 2), Game1.viewport, exemplar))
                                {
                                    spawnPoint = key;
                                    return true;
                                }

                                visited.Add(key, true);
                                points.Enqueue(key);
                            }
                        }
                    }
                }
            }


            return false;
        }

        private void spawnMinionsForHarvesting(GameLocation location, StardewValley.Farmer who)
        {
            const int tasksPerMinion = 50;
            
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // get harvesting tasks and clear any previous harvesting tasks from the task pool
            List<HarvestingMinionTask> tasks = generateHarvestingTasks(location);
            SkeletalMinionsMod.taskPool.clearTasks(location, new List<string> { nameof(HarvestingMinionTask) });

            // add tasks to task pool
            foreach (HarvestingMinionTask task in tasks) SkeletalMinionsMod.taskPool.addTask(task);

            // spawn skeletons for harvest
            int numMinions = tasks.Count / tasksPerMinion;
            if (numMinions == 0) numMinions = 1;

            for (int i=0; i<numMinions; ++i)
            {
                Point spawnPoint = new Point();
                if (!findNearestSpawnLocation(who.getTileLocationPoint(), location, ref spawnPoint)) continue;
                location.characters.Add(
                    new SkeletalMinion(
                        new Vector2(spawnPoint.X, spawnPoint.Y) * Game1.tileSize,
                        location, who,
                        SkeletalMinionsMod.taskPool,
                        new List<string> { nameof(HarvestingMinionTask) }
                    )
                );
            }

            sw.Stop();
            SkeletalMinionsMod.mod.Monitor.Log($"Spawning (harvesting) minions took {sw.ElapsedMilliseconds} ms.");
        }

        private List<HarvestingMinionTask> generateHarvestingTasks(GameLocation location)
        {
            List<HarvestingMinionTask> tasks = new List<HarvestingMinionTask>();
            List<Point> visited = new List<Point>();
            const int maxCropsPerTask = 9;

            /*
             Iterate over the map, finding harvestable crops.
             For each harvestable crop, add it to a list of visited crops.
             Attempt to add up to 8 adjacent, harvestable, unvisited crops to the task.
             */

            for (int tileX = 0; tileX < location.map.Layers[0].LayerWidth; ++tileX)
            {
                for (int tileY = 0; tileY < location.map.Layers[0].LayerHeight; ++tileY)
                {
                    Vector2 key = new Vector2(tileX, tileY);
                    if (location.terrainFeatures.ContainsKey(key))
                    {
                        if (location.terrainFeatures[key] is HoeDirt)
                        {
                            HoeDirt dirt = location.terrainFeatures[key] as HoeDirt;
                            Point p = new Point(tileX, tileY);

                            // if harvestable crop is here, try to make a task out of it
                            if (!visited.Contains(p) && dirt.readyForHarvest())
                            {
                                visited.Add(p);
                                List<Point> taskCrops = new List<Point>();
                                List<Point> adjacentCrops = getHarvestableCropsAdjacentTo(location, p);
                                taskCrops.Add(p);

                                // collect up to maxCropsPerTask-1 adjacent crops and make them part of the task
                                for (int i=0; i<adjacentCrops.Count && i<maxCropsPerTask-1; ++i)
                                {
                                    if (!visited.Contains(adjacentCrops[i]))
                                    {
                                        taskCrops.Add(adjacentCrops[i]);
                                        visited.Add(adjacentCrops[i]);
                                    }
                                }

                                // make the task
                                tasks.Add(new HarvestingMinionTask(location, p, taskCrops));
                            }
                        }
                    }
                }
            }

            return tasks;
        }

        // get harvestable crops adjacent to the given point.
        List<Point> getHarvestableCropsAdjacentTo(GameLocation location, Point here)
        {
            List<Point> crops = new List<Point>();

            for (int tileX = here.X-1; tileX <= here.X+1; ++tileX)
            {
                for (int tileY = here.Y-1; tileY <= here.Y+1; ++tileY)
                {
                    if (tileX != here.X || tileY != here.Y)
                    {
                        Vector2 key = new Vector2(tileX, tileY);
                        if (location.terrainFeatures.ContainsKey(key))
                        {
                            if (location.terrainFeatures[key] is HoeDirt)
                            {
                                HoeDirt dirt = location.terrainFeatures[key] as HoeDirt;
                                if (dirt.readyForHarvest())
                                    crops.Add(new Point(tileX, tileY));
                            }
                        }
                    }
                }
            }

            return crops;
        }

        // This method is used by the game to attach the given object to the tool.
        // To actually attach an object, you use attachments[<index>] = <object to attach>;
        // The returned object is what is placed in the player's cursor.
        public override StardewValley.Object attach(StardewValley.Object o)
        {
            /* Seeds: attachment slot 0 */
            if (o != null && o.category == StardewValley.Object.SeedsCategory)
            {
                StardewValley.Object obj = attachments[0];
                if (obj != null && obj.canStackWith(o))
                {
                    obj.Stack = o.addToStack(obj.Stack);
                    if (obj.Stack <= 0) obj = null;
                }
                attachments[0] = o;
                Game1.playSound("button1");
                return obj;
            }

            /* Fertilizer: attachment slot 1 */
            if (o != null && o.category == StardewValley.Object.fertilizerCategory)
            {
                StardewValley.Object obj = attachments[1];
                if (obj != null && obj.canStackWith(o))
                {
                    obj.Stack = o.addToStack(obj.Stack);
                    if (obj.Stack <= 0) obj = null;
                }
                attachments[1] = o;
                Game1.playSound("button1");
                return obj;
            }

            // Remove attachments.
            if (o == null)
            { 
                // Remove seeds first.
                if (attachments[0] != null)
                {
                    StardewValley.Object attachment = attachments[0];
                    attachments[0] = null;
                    Game1.playSound("dwop");
                    return attachment;
                }

                // Remove fertilizer second.
                if (attachments[1] != null)
                {
                    StardewValley.Object attachment = attachments[1];
                    attachments[1] = null;
                    Game1.playSound("dwop");
                    return attachment;
                }
            }

            return null; // unreachable?
        }

        // Used by the game to draw the attachments on the object when the mouse is hovering over the item in the inventory.
        public override void drawAttachments(SpriteBatch b, int x, int y)
        {
            if (this.attachments[0] == null)
            {
                b.Draw(seedAttachmentTexture, new Vector2(x, y), Color.White);
            }
            else
            {
                b.Draw(Game1.menuTexture, new Vector2((float)x, (float)y), new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10, -1, -1)), Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.86f);
                this.attachments[0].drawInMenu(b, new Vector2((float)x, (float)y), 1f);
            }

            if (this.attachments[1] == null)
            {
                b.Draw(fertilizerAttachmentTexture, new Vector2(x, y + Game1.tileSize + 4), Color.White);
            }
            else
            {
                b.Draw(Game1.menuTexture, new Vector2((float)x, (float)(y + Game1.tileSize + 4)), new Rectangle?(Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10, -1, -1)), Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.86f);
                this.attachments[1].drawInMenu(b, new Vector2((float)x, (float)(y + Game1.tileSize + 4)), 1f);
            }
        }

        public override void drawInMenu(SpriteBatch spriteBatch, Vector2 location, float scaleSize, float transparency, float layerDepth, bool drawStackNumber)
        {
            // Note to self: This drawing code was modified from the drawing code for the Wand.
            spriteBatch.Draw(wandTexture, location + new Vector2((float)(Game1.tileSize / 2), (float)(Game1.tileSize / 2)), new Rectangle?(Game1.getSquareSourceRectForNonStandardTileSheet(Game1.toolSpriteSheet, Game1.tileSize / 4, Game1.tileSize / 4, this.indexOfMenuItemView)), Color.White * transparency, 0.0f, new Vector2((float)(Game1.tileSize / 4 / 2), (float)(Game1.tileSize / 4 / 2)), (float)Game1.pixelZoom * scaleSize, SpriteEffects.None, layerDepth);
        }

        public override int attachmentSlots()
        {
            return attachments.Length;
        }

        public override bool canThisBeAttached(StardewValley.Object o)
        {
            return o == null || o.Category == StardewValley.Object.SeedsCategory ||
                o.Category == StardewValley.Object.fertilizerCategory;
        }

        public void goToNextWandMode()
        {
            switch (wandMode)
            {
                case WandMode.Harvesting:
                    wandMode = WandMode.Planting;
                    Game1.showGlobalMessage("Wand mode set to Plant.");
                    break;

                case WandMode.Planting:
                    wandMode = WandMode.Harvesting;
                    Game1.showGlobalMessage("Wand mode set to Harvest.");
                    break;
            }
        }

        // replace the skeleton wand with a chest containing its attachments
        public object getReplacement()
        {
            Chest replacement = new Chest(true);

            if (attachments[0] != null)
            {
                replacement.addItem(attachments[0]);
            }

            if (attachments[1] != null)
            {
                replacement.addItem(attachments[1]);
            }

            return replacement;
        }

        // need no additional save data--return an empty dictionary
        public Dictionary<string, string> getAdditionalSaveData()
        {
            return new Dictionary<string, string>();
        }

        // rebuild the object from its replacement.
        public void rebuild(Dictionary<string, string> additionalSaveData, object replacement)
        {
            build();

            Chest chest = replacement as Chest;
            if (!chest.isEmpty())
            {
                // items in chest may not be in the same order as attachments. Thus, assign attachments
                // from chest based on their category rather than their position in the chest.
                if (chest.items[0].category == StardewValley.Object.SeedsCategory)
                {
                    attachments[0] = chest.items[0] as StardewValley.Object;
                }
                else
                {
                    attachments[1] = chest.items[0] as StardewValley.Object;
                }

                if (chest.items.Count > 1)
                {
                    if (chest.items[1].category == StardewValley.Object.fertilizerCategory)
                    {
                        attachments[1] = chest.items[1] as StardewValley.Object;
                    }
                    else
                    {
                        attachments[0] = chest.items[1] as StardewValley.Object;
                    }
                }
            }
        }

        // Build the skeleton wand object.
        private void build()
        {
            numAttachmentSlots = 2;
            attachments = new StardewValley.Object[numAttachmentSlots];
            wandMode = WandMode.Planting;
            upgradeLevel = 0;
            initialParentTileIndex = 21;
            currentParentTileIndex = initialParentTileIndex;
            indexOfMenuItemView = 0;
            name = "Skeleton Wand";
            description = "An ancient and powerful wand.";
            stackable = false;
            category = -99;
        }
    }
}
