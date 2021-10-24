using System.Collections.Generic;
using Rhino.Geometry;

namespace AssemblerLib
{
    /// <summary>
    /// Stores a lists of scalar, vector and integer weights values
    /// </summary>
    public class Tensor
    {
        //internal Point3d point;
        /// <summary>
        /// Array of scalar values
        /// </summary>
        public double[] scalar;
        /// <summary>
        /// Array of Vector values
        /// </summary>
        public Vector3d[] vector;
        /// <summary>
        /// Array of integer weights
        /// </summary>
        public int[] iWeights;

        /*
         FUTURE IMPLEMENTATION
        Flexible parameters (user decided) - fields that support custom values to be added 
        as single values or arrays (single and multi-dimensional) of any type
         */

        /// <summary>
        /// Constructs a Tensor from constituting parameters
        /// </summary>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        /// <param name="iWeights"></param>
        public Tensor(double[] scalar, Vector3d[] vector, int[] iWeights)
        {
            this.scalar = scalar;
            this.vector = vector;
            this.iWeights = iWeights;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        /// <param name="iWeights"></param>
        public Tensor(List<double> scalar, List<Vector3d> vector, int[] iWeights) : this(scalar.ToArray(), vector.ToArray(), iWeights)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        /// <param name="iWeights"></param>
        public Tensor(List<double> scalar, List<Vector3d> vector, List<int> iWeights) : this(scalar.ToArray(), vector.ToArray(), iWeights.ToArray())
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        public Tensor(double[] scalar, Vector3d[] vector)
        {
            this.scalar = scalar;
            this.vector = vector;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        public Tensor(List<double> scalar, List<Vector3d> vector) : this(scalar.ToArray(), vector.ToArray())
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scalar"></param>
        public Tensor(double[] scalar)
        {
            this.scalar = scalar;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scalar"></param>
        public Tensor(List<double> scalar) : this(scalar.ToArray())
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vector"></param>
        public Tensor(Vector3d[] vector)
        {
            this.vector = vector;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vector"></param>
        public Tensor(List<Vector3d> vector) : this(vector.ToArray())
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iWeights"></param>
        public Tensor(List<int> iWeights)
        {
            this.iWeights = iWeights.ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iWeights"></param>
        public Tensor(int[] iWeights)
        {
            this.iWeights = iWeights;
        }

        /// <summary>
        /// Gets first scalar value
        /// </summary>
        /// <returns></returns>
        public double GetScalar()
        {
            return scalar[0];
        }

        /// <summary>
        /// Gets first Vector value
        /// </summary>
        /// <returns></returns>
        public Vector3d GetVector()
        {
            return vector[0];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public double[] GetScalars()
        {
            return scalar;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Vector3d[] GetVectors()
        {
            return vector;
        }

        /// <summary>
        /// Operator + for Tensor
        /// </summary>
        /// <param name="a">First Tensor operand</param>
        /// <param name="b">Second Tensor operand</param>
        /// <returns>Tensor which is the sum of the two Tensor operands</returns>
        static public Tensor operator +(Tensor a, Tensor b)
        {
            if (a.scalar.Length != b.scalar.Length) return null;
            if (a.vector.Length != b.vector.Length) return null;
            if (a.iWeights.Length != b.iWeights.Length) return null;

            Tensor sum;
            double[] sSum = new double[a.scalar.Length];
            Vector3d[] vSum = new Vector3d[a.vector.Length];
            int[] iWsum = new int[a.iWeights.Length];

            for (int i = 0; i < a.scalar.Length; i++)
                sSum[i] = a.scalar[i] + b.scalar[i];

            for (int i = 0; i < a.vector.Length; i++)
                vSum[i] = a.vector[i] + b.vector[i];

            for (int i = 0; i < a.iWeights.Length; i++)
                iWsum[i] = a.iWeights[i] + b.iWeights[i];

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
            if (a.scalar.Length != b.scalar.Length) return null;
            if (a.vector.Length != b.vector.Length) return null;
            if (a.iWeights.Length != b.iWeights.Length) return null;

            Tensor diff;
            double[] sDiff = new double[a.scalar.Length];
            Vector3d[] vDiff = new Vector3d[a.vector.Length];
            int[] iWDiff = new int[a.iWeights.Length];

            for (int i = 0; i < a.scalar.Length; i++)
                sDiff[i] = a.scalar[i] - b.scalar[i];

            for (int i = 0; i < a.vector.Length; i++)
                vDiff[i] = a.vector[i] - b.vector[i];

            for (int i = 0; i < a.iWeights.Length; i++)
                iWDiff[i] = a.iWeights[i] - b.iWeights[i];

            diff = new Tensor(sDiff, vDiff, iWDiff);

            return diff;
        }

    }
}
