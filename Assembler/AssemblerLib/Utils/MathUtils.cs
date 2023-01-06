using Grasshopper;
using Grasshopper.Kernel.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class MathUtils
    {

        /// <summary>
        /// Converts an angle in degrees to radians
        /// </summary>
        /// <param name="angle">The angle to convert (in degrees)</param>
        /// <returns></returns>
        internal static double DegreesToRadians(double angle)
        {
            return (Math.PI / 180) * angle;
        }

        /// <summary>
        /// Converts an angle in radians to degrees
        /// </summary>
        /// <param name="angle">The angle to convert (in radians)</param>
        /// <returns></returns>
        internal static double RadiansToDegrees(double angle)
        {
            return (180 / Math.PI) * angle;
        }

        /// <summary>
        /// Normalizes an array of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal static double[] NormalizeRange(double[] values)
        {
            double vMin = values.Min();
            double vMax = values.Max();

            // if ghNumbers are identical, prevent division by 0
            if (vMin == vMax) return values.Select(x => 0.5).ToArray();

            double den = 1 / (vMax - vMin);

            double[] normVal = new double[values.Length];

            for (int i = 0; i < values.Length; i++)
                normVal[i] = (values[i] - vMin) * den;

            return normVal;
        }

        /// <summary>
        /// Normalizes a List of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        internal static List<double> NormalizeRange(List<double> values)
        {
            double vMin = values.Min();
            double vMax = values.Max();

            // if ghNumbers are identical, prevent division by 0
            if (vMin == vMax) return values.Select(x => 0.5).ToList();

            double den = 1 / (vMax - vMin);

            List<double> normVal = new List<double>();

            for (int i = 0; i < values.Count; i++)
                normVal.Add((values[i] - vMin) * den);

            return normVal;
        }

        /// <summary>
        /// Normalizes a Jagged array of real numbers
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        private static double[][] NormalizeRanges(double[][] values)
        {
            int nVals = values[0].Length;
            double[] vMin = new double[nVals], vMax = new double[nVals];
            double[] tMin = new double[nVals], tMax = new double[nVals];

            // populate arrays
            for (int j = 0; j < nVals; j++)
            {
                vMin[j] = Double.MaxValue;
                vMax[j] = Double.MinValue;
            }

            // find vMin and vMax for each set of values per point
            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < nVals; j++)
                {
                    tMin[j] = values[i][j];
                    tMax[j] = values[i][j];
                    if (tMin[j] < vMin[j]) vMin[j] = tMin[j];
                    if (tMax[j] > vMax[j]) vMax[j] = tMax[j];
                }

            double[] den = new double[nVals];

            for (int j = 0; j < nVals; j++)
                den[j] = 1 / (vMax[j] - vMin[j]);

            double[][] normVal = new double[values.Length][];

            // recompute values, preventing division by 0
            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < values[i].Length; j++)
                    normVal[i][j] = vMin[j] == vMax[j] ? 0.5 : (values[i][j] - vMin[j]) * den[j];

            return normVal;
        }

        /// <summary>
        /// Normalizes a DataTree of real numbers
        /// </summary>
        /// <param name="values">the values data tree</param>
        /// <returns></returns>
        internal static DataTree<double> NormalizeRanges(DataTree<double> values)
        {
            IList<GH_Path> paths = values.Paths;
            double[][] valuesArray = DataUtils.ToJaggedArray(values);
            double[][] normValuesArray = NormalizeRanges(valuesArray);

            DataTree<double> normVal = DataUtils.ToDataTree(normValuesArray);
            for (int i = 0; i < normVal.Paths.Count; i++)
                normVal.Paths[i] = paths[i];

            return normVal;
        }
    }
}
