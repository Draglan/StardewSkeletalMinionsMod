using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using xTile.Dimensions;

namespace StardewSkeletalMinionsMod
{
    public class SkeletalMinion : NPC
    {
        public const string skeletalMinionName = "Skeletal Minion";

        public StardewValley.Farmer farmerOwner;
        public MinionTask currentTask;

        public List<StardewValley.Object> inventory;
        private int maxInventorySize = int.MaxValue;

        private List<string> acceptsTasks;
        public MinionTaskPool taskPool;
        private bool isDead;

        private const int deathTime = 3000;
        private int deathTimer;
        private bool isDeathShaking;

        public const int minionSpeed = 6;

        public bool IsDead { get => isDead; }
        public int MaxInventorySize { get => maxInventorySize; }

        public SkeletalMinion(Vector2 position, GameLocation location, StardewValley.Farmer owner, MinionTaskPool taskPool, List<string> acceptsTasks)
            : base(new AnimatedSprite(Game1.content.Load<Texture2D>("Characters//Monsters//Skeleton Mage"), 0, 16, 32), position, 2, skeletalMinionName)
        {
            currentLocation = location;
            willDestroyObjectsUnderfoot = false;
            farmerPassesThrough = true;
            collidesWithOtherCharacters = false;
            
            inventory = new List<StardewValley.Object>();
            this.acceptsTasks = acceptsTasks;
            this.taskPool = taskPool;
            farmerOwner = owner;
        }

        public override void update(GameTime time, GameLocation location)
        {
            currentLocation = location;

            if (!isDead && currentTask != null && !currentTask.IsComplete)
                currentTask.update(time, location);
            else if (!isDead)
            {
                if (!taskPool.assignTask(this, acceptsTasks))
                {
                    isDead = true;
                    deathTimer = deathTime;
                }
            }
            else
            {
                deathTimer -= time.ElapsedGameTime.Milliseconds;

                if (!isDeathShaking && deathTimer > 0 && deathTimer <= 1000)
                {
                    shake(deathTimer);
                    isDeathShaking = true;
                }
                else if (deathTimer <= 0)
                {
                    returnItemsToOwner();
                    location.characters.Remove(this);
                    Game1.playSound("skeletonDie");

                    // death particle effects -- taken from StardewValley.Monsters.Skeleton.cs
                    currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(46, position, Color.White, 10, false, 70f, 0, -1, -1f, -1, 0));
                    currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(46, position + new Vector2((float)(-Game1.tileSize / 4), 0.0f), Color.White, 10, false, 70f, 0, -1, -1f, -1, 0)
                    {
                        delayBeforeAnimationStart = 100
                    });
                    currentLocation.temporarySprites.Add(new TemporaryAnimatedSprite(46, position + new Vector2((float)(Game1.tileSize / 4), 0.0f), Color.White, 10, false, 70f, 0, -1, -1f, -1, 0)
                    {
                        delayBeforeAnimationStart = 200
                    });
                }
            }

            if (controller != null) controller.pausedTimer = 0; // prevent NPC from permanently stopping if faced w/ obstacle
            base.update(time, location);
        }

        public bool addItemToInventory(StardewValley.Object item)
        {
            // attempt to stack item
            for (int i = 0; i < inventory.Count; ++i)
            {
                if (item.canStackWith(inventory[i]) && inventory[i].Stack != inventory[i].maximumStackSize())
                {
                    int leftover = inventory[i].addToStack(item.Stack);
                    item.Stack = leftover;

                    if (leftover != 0)
                        return addItemToInventory(item);
                    else
                        return true;
                }
            }

            // if cannot stack, add the item to a new inventory slot
            if (inventory.Count + 1 <= maxInventorySize)
            {
                inventory.Add(item);
                return true;
            }
            else
                return false;
        }

        // Subtract the given quantity of the item of the given index from the inventory.
        // Returns the amount actually removed, which will be <=quantity.
        public int subtractItemFromInventory(int index, int quantity)
        {
            int toRemove = quantity;
            for (int i = inventory.Count - 1; i >= 0 && toRemove > 0; --i)
            {
                if (inventory[i].parentSheetIndex == index)
                {
                    if (inventory[i].Stack <= toRemove)
                    {
                        toRemove -= inventory[i].Stack;
                        inventory.RemoveAt(i);
                    }
                    else
                    {
                        inventory[i].Stack -= toRemove;
                        toRemove = 0;
                    }
                }
            }

            return quantity - toRemove;
        }

        public int getItemInInventoryCount(int index)
        {
            int count = 0;
            foreach (StardewValley.Object o in inventory)
                if (o.parentSheetIndex == index) count += o.Stack;
            return count;
        }


        public void returnItemsToOwner()
        {
            for (int i=0; i<inventory.Count; ++i)
            {
                if (!farmerOwner.addItemToInventoryBool(inventory[i]))
                {
                    // put the rest of the objects into a list and let the user grab them
                    // via an ItemGrabMenu
                    List <Item> items = new List<Item>();
                    for (int j = i; j < inventory.Count; ++j)
                        items.Add(inventory[j]);

                    Game1.activeClickableMenu = new ItemGrabMenu(items);
                    break;
                }
            }
        }

        /* Kill the minion. */
        public void kill(bool killImmediately = false)
        {
            isDead = true;
            if (!killImmediately)
                deathTimer = deathTime;
            else
                deathTimer = 0;
        }
    }   
}
