using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewSkeletalMinionsMod
{
    public class TaskComparer : IComparer<MinionTask>
    {
        private SkeletalMinion minion;
        public TaskComparer(SkeletalMinion minion) => this.minion = minion;

        public int Compare(MinionTask x, MinionTask y)
        {
            double distanceToX = Math.Sqrt(Math.Pow(x.position.X - minion.getTileX(), 2) + Math.Pow(x.position.Y - minion.getTileY(), 2));
            double distanceToY = Math.Sqrt(Math.Pow(y.position.X - minion.getTileX(), 2) + Math.Pow(y.position.Y - minion.getTileY(), 2));

            if (distanceToX < distanceToY) return -1;
            else if (distanceToX > distanceToY) return 1;
            return 0;
        }
    }
}
