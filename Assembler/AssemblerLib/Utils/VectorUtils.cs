using Rhino.Geometry;
using System.Collections.Generic;

namespace AssemblerLib.Utils
{
    public static class VectorUtils
    {

        /// <summary>
        /// Average Unitized Vector from a Vector Array - unitizes vector result at each step
        /// Useful for Vertex normal calculations
        /// </summary>
        /// <param name="vectors"></param>
        /// <returns>The average normalized vector</returns>
        public static Vector3d UnitizedAverage(IEnumerable<Vector3d> vectors)
        {
            Vector3d averageNormal = Vector3d.Zero;
            foreach (Vector3d v in vectors)
            {
                averageNormal += v;
                averageNormal.Unitize();
            }

            return averageNormal;
        }

        /// <summary>
        /// Removes duplicate Vectors (within tolerance) from an array, returning only unique Vectors
        /// </summary>
        /// <param name="vectors"></param>
        /// <param name="angleTolerance"></param>
        /// <returns>Array of unique ghVectors</returns>
        public static Vector3d[] GetUniqueVectors(Vector3d[] vectors, double angleTolerance)
        {
            List<Vector3d> result = new List<Vector3d>();

            for (int i = 0; i < vectors.Length; i++)
            {
                bool isDuplicate = false;
                for (int j = 0; j < i; j++)
                {
                    if (Vector3d.VectorAngle(vectors[i], vectors[j]) < angleTolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    result.Add(vectors[i]);

                }
            }

            return result.ToArray();
        }

    }
}
