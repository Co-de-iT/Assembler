﻿using Rhino.Geometry;
using System.Collections.Generic;

namespace AssemblerLib
{
    /// <summary>
    /// Stores arrays of scalar, vector and integer weight values
    /// </summary>
    public class Tensor
    {
        /// <summary>
        /// Array of scalar values
        /// </summary>
        public double[] Scalars;
        /// <summary>
        /// Array of Vector values
        /// </summary>
        public Vector3d[] Vectors;
        /// <summary>
        /// Array of integer weights
        /// </summary>
        public int[] IWeights;

        /*
         FUTURE IMPLEMENTATION
        Flexible parameters (user decided) - fields that support custom values to be added 
        as single values or arrays (single and multi-dimensional) of any type
         */

        #region Constructors
        /// <summary>
        /// Constructs a Tensor from constituting parameters (scalars, vectors, integers)
        /// </summary>
        /// <param name="Scalars">IEnumerable scalar values</param>
        /// <param name="Vectors">IEnumerable vector values</param>
        /// <param name="IWeights">IEnumerable integer values</param>
        public Tensor(IEnumerable<double> Scalars, IEnumerable<Vector3d> Vectors, IEnumerable<int> IWeights)
        {
            this.Scalars = (double[])Scalars;
            this.Vectors = (Vector3d[])Vectors;
            this.IWeights = (int[])IWeights;
        }

        /// <summary>
        /// Constructs a Tensor from constituting parameters (scalars, vectors)
        /// </summary>
        /// <param name="Scalars">IEnumerable scalar values</param>
        /// <param name="Vectors">IEnumerable vector values</param>
        public Tensor(IEnumerable<double> Scalars, IEnumerable<Vector3d> Vectors)
        {
            this.Scalars = (double[])Scalars;
            this.Vectors = (Vector3d[])Vectors;
        }

        /// <summary>
        /// Constructs a Tensor from scalars
        /// </summary>
        /// <param name="Scalars">IEnumerable scalar values</param>
        public Tensor(IEnumerable<double> Scalars)
        {
            this.Scalars = (double[])Scalars;
        }

        /// <summary>
        /// Constructs a Tensor from vectors
        /// </summary>
        /// <param name="Vectors">IEnumerable vector values</param>
        public Tensor(IEnumerable<Vector3d> Vectors)
        {
            this.Vectors = (Vector3d[])Vectors;
        }

        /// <summary>
        /// Constructs a Tensor from integer values
        /// </summary>
        /// <param name="IWeights">IEnumerable integer values</param>
        public Tensor(IEnumerable<int> IWeights)
        {
            this.IWeights = (int[])IWeights;
        }
        #endregion

        #region Getters
        /// <summary>
        /// Gets Scalar value at index
        /// </summary>
        /// <param name="index">index at which read the value - optional (default is 0)</param>
        /// <returns>scalar value at index 0</returns>
        public double GetScalar(int index = 0)
        {
            return Scalars[index];
        }

        /// <summary>
        /// Gets Vector value at index
        /// </summary>
        /// <param name="index">index at which read the value - optional (default is 0)</param>
        /// <returns>vector value at index 0</returns>
        public Vector3d GetVector(int index = 0)
        {
            return Vectors[index];
        }

        /// <summary>
        /// Gets iWeight value at index
        /// </summary>
        /// <param name="index">index at which read the value - optional (default is 0)</param>
        /// <returns>iWeight value at index 0</returns>
        public int GetiWeight(int index = 0)
        {
            return IWeights[index];
        }

        #endregion

        /// <summary>
        /// Operator + for Tensor
        /// </summary>
        /// <param name="a">First Tensor operand</param>
        /// <param name="b">Second Tensor operand</param>
        /// <returns>Tensor which is the sum of the two Tensor operands</returns>
        static public Tensor operator +(Tensor a, Tensor b)
        {
            if (a.Scalars.Length != b.Scalars.Length) return null;
            if (a.Vectors.Length != b.Vectors.Length) return null;
            if (a.IWeights.Length != b.IWeights.Length) return null;

            Tensor sum;
            double[] sSum = new double[a.Scalars.Length];
            Vector3d[] vSum = new Vector3d[a.Vectors.Length];
            int[] iWsum = new int[a.IWeights.Length];

            for (int i = 0; i < a.Scalars.Length; i++)
                sSum[i] = a.Scalars[i] + b.Scalars[i];

            for (int i = 0; i < a.Vectors.Length; i++)
                vSum[i] = a.Vectors[i] + b.Vectors[i];

            for (int i = 0; i < a.IWeights.Length; i++)
                iWsum[i] = a.IWeights[i] + b.IWeights[i];

            sum = new Tensor(sSum, vSum, iWsum);

            return sum;
        }

        /// <summary>
        /// Operator - for Tensor
        /// </summary>
        /// <param name="a">First Tensor operand</param>
        /// <param name="b">Second Tensor operand</param>
        /// <returns>Tensor which is the difference of the two Tensor operands</returns>
        static public Tensor operator -(Tensor a, Tensor b)
        {
            if (a.Scalars.Length != b.Scalars.Length) return null;
            if (a.Vectors.Length != b.Vectors.Length) return null;
            if (a.IWeights.Length != b.IWeights.Length) return null;

            Tensor diff;
            double[] sDiff = new double[a.Scalars.Length];
            Vector3d[] vDiff = new Vector3d[a.Vectors.Length];
            int[] iWDiff = new int[a.IWeights.Length];

            for (int i = 0; i < a.Scalars.Length; i++)
                sDiff[i] = a.Scalars[i] - b.Scalars[i];

            for (int i = 0; i < a.Vectors.Length; i++)
                vDiff[i] = a.Vectors[i] - b.Vectors[i];

            for (int i = 0; i < a.IWeights.Length; i++)
                iWDiff[i] = a.IWeights[i] - b.IWeights[i];

            diff = new Tensor(sDiff, vDiff, iWDiff);

            return diff;
        }

        /// <summary>
        /// Multiplication operator for Tensor
        /// </summary>
        /// <param name="t">Tensor operand</param>
        /// <param name="d">double operand</param>
        /// <returns>Tensor with scalars and vectors multiplied by <paramref name="d"/></returns>
        static public Tensor operator *(Tensor t, double d)
        {
            Tensor mult;
            double[] sMult = new double[t.Scalars.Length];
            Vector3d[] vMult = new Vector3d[t.Vectors.Length];
            int[] iMult = new int[t.IWeights.Length];

            for (int i = 0; i < t.Scalars.Length; i++)
                sMult[i] = t.Scalars[i] * d;

            for (int i = 0; i < t.Vectors.Length; i++)
                vMult[i] = t.Vectors[i] * d;

            for (int i = 0; i < t.IWeights.Length; i++)
                iMult[i] = t.IWeights[i];

            mult = new Tensor(sMult, vMult, iMult);

            return mult;
        }
    }
}
