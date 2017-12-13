using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewValley;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewSkeletalMinionsMod
{
    public abstract class MinionTask
    {
        public string name;
        protected SkeletalMinion owner;
        public Point position;
        private bool taskComplete;
        public GameLocation location;

        /* Items requirements that must be met by minion for task to be assigned. Key: item index; Value: item count */
        protected List<KeyValuePair<int, int>> requiredItems;

        public MinionTask()
            : this(nameof(MinionTask), new Point(), null)
        {
            
        }

        public MinionTask(string name, Point position, GameLocation location, List<KeyValuePair<int,int>> requiredItems = null)
        {
            this.name = name;
            this.position = position;
            this.requiredItems = requiredItems;
            this.location = location;
        }

        public bool IsComplete
        {
            get { return taskComplete; }
        }

        protected abstract void updateTask(GameTime time, GameLocation location);
        public abstract bool isAtEnd(PathNode currentNode, Point endPoint, GameLocation location, Character c);
        public abstract void endBehavior(Character c, GameLocation location);

        public bool meetsItemRequirements(SkeletalMinion minion)
        {
            if (requiredItems == null) return true;
            foreach (KeyValuePair<int,int> requirement in requiredItems)
            {
                if (minion.getItemInInventoryCount(requirement.Key) < requirement.Value) return false;
            }
            return true;
        }

        public void update(GameTime time, GameLocation location)
        {
            if (!taskComplete)
                updateTask(time, location);
        }

        /* Set the owner of this task to be the given minion.
         * If the task cannot be assigned--by default, because there was no path
         * to the task--then it returns false. Otherwise, it returns true.
         */
        public virtual bool setOwner(SkeletalMinion owner)
        {
            owner.controller = new PathFindController(owner, owner.currentLocation, isAtEnd, 2, true, endBehavior, 9999, position);

            if (owner.controller.pathToEndPoint == null || owner.controller.pathToEndPoint.Count == 0)
            {
                SkeletalMinionsMod.mod.Monitor.Log($"No path.");
                owner.controller = null;
                return false;
            }

            this.owner = owner;
            return true;
        }

        /* Gets rid of this task's owner, making it ownerless. */
        public void removeOwner()
        {
            if (owner != null)
            {
                owner.controller = null;
                owner = null;
            }
        }

        protected virtual void onTaskComplete(GameTime time, GameLocation location)
        {
            if (owner != null) owner.currentTask = null;
        }

        protected void complete(GameTime time, GameLocation location)
        {
            taskComplete = true;
            onTaskComplete(time, location);
        }

        public bool Equals(MinionTask other)
        {
            return other.position.Equals(position) &&
                other.name.Equals(name);
        }

        public override string ToString()
        {
            return "[name='" + name + "', position=" + position + "]";
        }
    }
}
