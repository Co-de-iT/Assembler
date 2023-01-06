using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class DataUtils
    {

        /// <summary>
        /// Converts a jagged array into a DataTree of the same type
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="jaggedArray">A jagged array to convert to DataTree</param>
        /// <returns>A DataTree of type Handle</returns>
        public static DataTree<T> ToDataTree<T>(T[][] jaggedArray)
        {
            DataTree<T> data = new DataTree<T>();

            for (int i = 0; i < jaggedArray.Length; i++)
                data.AddRange(jaggedArray[i].Select(d => d).ToList(), new GH_Path(i));

            return data;
        }

        /// <summary>
        /// Converts a list of arrays into a DataTree of the same type
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="listOfArrays">A list of Arrays to convert to DataTree</param>
        /// <returns>A DataTree of type Handle</returns>
        public static DataTree<T> ToDataTree<T>(List<T[]> listOfArrays)
        {
            DataTree<T> data = new DataTree<T>();

            for (int i = 0; i < listOfArrays.Count; i++)
                data.AddRange(listOfArrays[i].Select(d => d).ToList(), new GH_Path(i));

            return data;
        }

        /// <summary>
        /// Converts a DataTree into a jagged array of the same type\nThe array length is equal to the number of branches, regardless of paths
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="tree">A DataTree to convert to jagged array</param>
        /// <returns>A Jagged Array of type Handle</returns>
        public static T[][] ToJaggedArray<T>(DataTree<T> tree)
        {

            T[][] jArray = new T[tree.BranchCount][];

            for (int i = 0; i < tree.BranchCount; i++)
                jArray[i] = tree.Branches[i].ToArray();

            return jArray;
        }

        /// <summary>
        /// Converts a DataTree into a list of arrays of the same type\nThe list count is equal to the number of branches, regardless of paths
        /// </summary>
        /// <typeparam Name="T">The Data type</typeparam>
        /// <param name="tree">A DataTree to convert to List of arrays</param>
        /// <returns>A List of arrays of type Handle</returns>
        public static List<T[]> ToListOfArrays<T>(DataTree<T> tree)
        {

            List<T[]> arraysList = new List<T[]>();

            for (int i = 0; i < tree.BranchCount; i++)
                arraysList.Add(tree.Branches[i].ToArray());

            return arraysList;
        }

        /// <summary>
        /// Clones a Dictionary and its values
        /// </summary>
        /// <typeparam Name="TKey"></typeparam>
        /// <typeparam Name="TValue"></typeparam>
        /// <param name="original"></param>
        /// <returns>The cloned Dictionary</returns>
        /// <remarks>as seen here: https://stackoverflow.com/questions/139592/what-is-the-best-way-to-clone-deep-copy-a-net-generic-dictionarystring-t</remarks>
        public static Dictionary<TKey, TValue> CloneDictionaryWithValues<TKey, TValue>(Dictionary<TKey, TValue> original)
        {
            Dictionary<TKey, TValue> copy = new Dictionary<TKey, TValue>(original.Count, original.Comparer);
            foreach (KeyValuePair<TKey, TValue> entry in original)
            {
                copy.Add(entry.Key, (TValue)entry.Value);
            }
            return copy;
        }

        /// <summary>
        /// Renames a key in a Dictionary
        /// </summary>
        /// <typeparam Name="TKey"></typeparam>
        /// <typeparam Name="TValue"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="fromKey"></param>
        /// <param name="toKey"></param>
        /// <returns>true if successful</returns>
        /// <remarks>as seen here: https://stackoverflow.com/questions/6499334/best-way-to-change-dictionary-key</remarks>
        public static bool RenameKey<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey fromKey, TKey toKey)
        {
            TValue value = dictionary[fromKey];
            if (!dictionary.Remove(fromKey))
                return false;
            dictionary[toKey] = value;
            return true;
        }

        /// <summary>
        /// Converts a GH_Structure of GH_Number to a DataTree of double
        /// </summary>
        /// <param name="ghNumbers"></param>
        /// <returns></returns>
        public static DataTree<double> GHS2TreeDoubles(GH_Structure<GH_Number> ghNumbers)
        {
            DataTree<double> rhNumbers = new DataTree<double>();

            if (ghNumbers != null)
                for (int i = 0; i < ghNumbers.Branches.Count; i++)
                    rhNumbers.AddRange(ghNumbers.Branches[i].Select(n => n.Value).ToList(), ghNumbers.Paths[i]);

            return rhNumbers;
        }

        /// <summary>
        /// Converts a GH_Structure of GH_Vector to a DataTree of Vector3d
        /// </summary>
        /// <param name="ghVectors"></param>
        /// <returns></returns>
        public static DataTree<Vector3d> GHS2TreeVectors(GH_Structure<GH_Vector> ghVectors)
        {
            DataTree<Vector3d> rhVectors = new DataTree<Vector3d>();

            if (ghVectors != null)
                for (int i = 0; i < ghVectors.Branches.Count; i++)
                    rhVectors.AddRange(ghVectors.Branches[i].Select(n => n.Value).ToList(), ghVectors.Paths[i]);

            return rhVectors;
        }

        /// <summary>
        /// Converts a GH_Structure of GH_Integer to a DataTree of integer
        /// </summary>
        /// <param name="ghInt"></param>
        /// <returns></returns>
        public static DataTree<int> GHS2TreeIntegers(GH_Structure<GH_Integer> ghInt)
        {
            DataTree<int> rhInt = new DataTree<int>();

            if (ghInt != null)
                for (int i = 0; i < ghInt.Branches.Count; i++)
                    rhInt.AddRange(ghInt.Branches[i].Select(n => n.Value).ToList(), ghInt.Paths[i]);

            return rhInt;
        }
    }
}
