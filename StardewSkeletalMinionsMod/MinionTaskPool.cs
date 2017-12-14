using Microsoft.Xna.Framework;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewSkeletalMinionsMod
{
    public class MinionTaskPool
    {
        /* All currently available tasks */
        private List<MinionTask> tasks;

        // The number of tasks present in this manager.
        public int Count
        {
            get { return tasks.Count; }
        }

        public MinionTaskPool()
        {
            tasks = new List<MinionTask>();
        }
        
        // Assign the closest, pathable task to the given minion, accepting only task names stored in 'taskTypes'.
        // If taskTypes is empty or null, any task type will be accepted.
        // Returns true if a task was assigned, false otherwise.
        public bool assignTask(SkeletalMinion minion, List<string> taskTypes)
        {
            List<MinionTask> candidates;
            if (taskTypes == null || taskTypes.Count == 0)
                candidates = tasks;
            else
                candidates = new List<MinionTask>();

            // narrow down candidates by task type
            if (candidates != tasks)
                foreach (MinionTask task in tasks)
                    if (taskTypes.Contains(task.name) && task.meetsItemRequirements(minion))
                        candidates.Add(task);

            // sort by distance to minion
            candidates.Sort(new TaskComparer(minion));

            // assign first task that is pathable
            foreach (MinionTask task in candidates)
            {
                if (task.setOwner(minion))
                {
                    minion.currentTask = task;
                    tasks.Remove(task);
                    return true;
                }
            }
            
            return false;
        }

        // Add the given task to the manager.
        public void addTask(MinionTask task)
        {
            tasks.Add(task);
        }

        // Clear tasks with the given names in the given location. If names is left null, all tasks in the manager are cleared.
        // If location is left null, tasks are cleared irrespective of location.
        public void clearTasks(GameLocation location = null, List<string> names = null)
        {
            if (location==null && names == null)
                tasks.Clear();
            else
            {
                for (int i = tasks.Count - 1; i >= 0; --i)
                {
                    if ((names == null || names.Contains(tasks[i].name)) && (location == null || location == tasks[i].location))
                        tasks.RemoveAt(i);
                }
            }
        }
    }
}
