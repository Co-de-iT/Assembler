using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class HandleUtils
    {

        private static Handle CloneBasic(Handle handle)
        {
            Handle clone = new Handle();
            clone.Sender = handle.Sender;
            clone.Rotations = handle.Rotations.Select(r => r).ToArray();
            clone.RDictionary = DataUtils.CloneDictionaryWithValues(handle.RDictionary);
            // this is a shallow copy - not working
            //r = other.r;
            // deep array copy (from https://stackoverflow.com/questions/3464635/deep-copy-with-array)
            clone.Receivers = handle.Receivers.Select(pl => pl.Clone()).ToArray();
            clone.Type = handle.Type;
            clone.Weight = handle.IdleWeight;
            clone.IdleWeight = handle.IdleWeight;

            return clone;
        }

        /// <summary>
        /// Clones a Handle, resetting data
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>a cloned Handle</returns>
        public static Handle Clone(ref Handle handle)
        {
            Handle clone = CloneBasic(handle);

            clone.Occupancy = 0;
            clone.NeighbourHandle = -1;
            clone.NeighbourObject = -1;

            return clone;
        }

        /// <summary>
        /// Duplicates a Handle preserving connectivity information and weight
        /// </summary>
        /// <param name="handle"></param>
        /// <returns>a duplicated Handle with the same connectivity</returns>
        public static Handle CloneWithConnectivity(ref Handle handle)
        {
            Handle clone = CloneBasic(handle);

            clone.Occupancy = handle.Occupancy;
            clone.NeighbourHandle = handle.NeighbourHandle;
            clone.NeighbourObject = handle.NeighbourObject;

            return clone;
        }

        /// <summary>
        /// Updates <see cref="Handle"/>s involved in a new connection between two <see cref="AssemblyObject"/>s
        /// </summary>
        /// <param name="AO1">first <see cref="AssemblyObject"/></param>
        /// <param name="handle1"><see cref="Handle"/> from first <see cref="AssemblyObject"/></param>
        /// <param name="AO2">second <see cref="AssemblyObject"/></param>
        /// <param name="handle2"><see cref="Handle"/> from second <see cref="AssemblyObject"/></param>
        internal static void UpdateHandlesOnConnection(AssemblyObject AO1, int handle1, AssemblyObject AO2, int handle2)
        {
            AO1.Handles[handle1].Occupancy = 1;
            AO2.Handles[handle2].Occupancy = 1;
            AO1.Handles[handle1].NeighbourObject = AO2.AInd;
            AO2.Handles[handle2].NeighbourObject = AO1.AInd;
            AO1.Handles[handle1].NeighbourHandle = handle2;
            AO2.Handles[handle2].NeighbourHandle = handle1;

            double newWeight = 0.5 * (AO1.Handles[handle1].Weight + AO2.Handles[handle2].Weight);
            AO1.Handles[handle1].Weight = newWeight;
            AO2.Handles[handle2].Weight = newWeight;
        }

        /// <summary>
        /// Purges a List of Null items
        /// </summary>
        /// <param name="inputList">A supposedly dirty <see cref="Handle"/> List containing some Null items</param>
        /// <returns>The purged List</returns>
        public static List<Handle> PurgeNullHandlesFromList(List<Handle> inputList)
        {
            List<Handle> purgedList = new List<Handle>();

            for (int i = 0; i < inputList.Count; i++)
            {
                if (inputList[i].Sender == null || inputList[i].RDictionary == null) continue;
                purgedList.Add(inputList[i]);
            }

            return purgedList;
        }

    }
}
