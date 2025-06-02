using Grasshopper;
using Grasshopper.Kernel.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Utils
{
    public static class MathUtils
    {
        public static readonly Random rnd = new Random();

        //i = x < 0 ? -x : x;
        internal static double Absv1(double x) => x < 0 ? -x : x;
        //i = (x ^ (x >> 31)) - (x >> 31);
        internal static double Absv2(double x)
        {
            Byte xb = (Byte)x;
            return (xb ^ (xb >> 31)) - (xb >> 31);
        }

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
            return NormalizeRange(values.ToArray()).ToList();
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
            {
                normVal[i] = new double[values[i].Length];
                for (int j = 0; j < values[i].Length; j++)
                    normVal[i][j] = vMin[j] == vMax[j] ? 0.5 : (values[i][j] - vMin[j]) * den[j];
            }

            return normVal;
        }

        /// <summary>
        /// Normalizes a DataTree of real numbers
        /// </summary>
        /// <param name="values">the values data tree</param>
        /// <returns></returns>
        internal static DataTree<double> NormalizeRanges(DataTree<double> values)
        {
            List<GH_Path> paths = values.Paths.ToList();
            double[][] valuesArray = DataUtils.ToJaggedArray(values);
            double[][] normValuesArray = NormalizeRanges(valuesArray);

            DataTree<double> normVal = new DataTree<double>();

            for (int i = 0; i < normValuesArray.Length; i++)
                normVal.AddRange(normValuesArray[i], paths[i]);

            return normVal;
        }

        /// <summary>
        /// Performs a Weighted Random Choice given an array of weights
        /// </summary>
        /// <param name="weights"></param>
        /// <returns>index of the selected weight</returns>
        public static int WeightedRandomChoiceIndex(int[] weights)
        {

            int totWeights = weights.Sum(w => w);

            int chosenInd = rnd.Next(totWeights);
            int valueInd = 0;

            while (chosenInd >= 0)
            {
                chosenInd -= weights[valueInd];
                valueInd++;
            }

            valueInd -= 1;

            return valueInd;
        }

        /// <summary>
        /// Performs a Weighted Random Choice on a data array and corresponding weights
        /// </summary>
        /// <typeparam Name="T"></typeparam>
        /// <param name="values"></param>
        /// <param name="weights"></param>
        /// <returns>the selected value</returns>
        public static T WeightedRandomChoice<T>(T[] values, int[] weights)
        {
            return values[WeightedRandomChoiceIndex(weights)];
        }
    }
}
