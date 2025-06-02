using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AssemblerLib.Utils
{
    public static class ComputingRSMethods
    {
        #region compute receiver methods

        internal static double ComputeZero(Assemblage _Ass, AssemblyObject _receiver) => 0.0;

        //internal static double ComputeRRandom(Assemblage Ass, AssemblyObject receiver) => MathUtils.rnd.NextDouble();

        /// <summary>
        /// Computes receiver scalar Field within threshold; receivers outside will return -1
        /// to stay within the threshold couple it with SelectMaxValue
        /// </summary>
        /// <param name="AO"></param>
        /// <returns></returns>
        internal static double ComputeScalarFieldWithinThreshold(Assemblage Ass, AssemblyObject AO)
        {
            double scalarValue = Ass.ExogenousSettings.Field.GetClosestScalar(AO.ReferencePlane.Origin);
            if (scalarValue > Ass.ExogenousSettings.FieldScalarThreshold) return -1;
            else return scalarValue;
        }

        /// <summary>
        /// Computes receiver scalar Field with sign
        /// </summary>
        /// <param name="AO"></param>
        /// <returns>The closest scalar with sign</returns>
        internal static double ComputeScalarFieldSigned(Assemblage Ass, AssemblyObject AO) => Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(AO.ReferencePlane.Origin);

        internal static double ComputeScalarField(Assemblage Ass, AssemblyObject AO)
        {
            // TODO: try, instead of Math.Abs(), the other versions in MathUtils
            return Math.Abs(ComputeScalarFieldSigned(Ass, AO));
        }

        internal static double ComputeScalarFieldInterpolated(Assemblage Ass, AssemblyObject AO) => Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetInterpolatedScalar(AO.ReferencePlane.Origin));

        /// <summary>
        /// Computes absolute difference between scalar <see cref="Field"/> value and threshold from each free <see cref="Handle"/> 
        /// </summary>
        /// <param name="Ass"></param>
        /// <param name="AO"></param>
        /// <returns>the minimum absolute difference from the threshold</returns>
        internal static double ComputeScalarFieldHandles(Assemblage Ass, AssemblyObject AO)
        {
            double scalarValue = double.MaxValue;
            double handleValue;
            foreach (Handle h in AO.Handles)
            {
                if (h.Occupancy != 0) continue;
                handleValue = Math.Abs(Ass.ExogenousSettings.FieldScalarThreshold - Ass.ExogenousSettings.Field.GetClosestScalar(h.SenderPlane.Origin));
                if (handleValue < scalarValue) scalarValue = handleValue;
            }
            return scalarValue;
        }
        /// <summary>
        /// Computes sum of <see cref="AssemblyObject"/> weights in a search sphere, updating neighbours accordingly
        /// </summary>
        /// <param name="AO"></param>
        /// <returns>the weights sum</returns>
        /// 
        internal static double ComputeWeightDensity(Assemblage Ass, AssemblyObject AO)
        {
            // search for neighbour objects in radius
            double density = 0;
            Ass.centroidsTree.Search(new Sphere(AO.ReferencePlane.Origin, Ass.CollisionRadius), (s, args) =>
            {
                GH_Path neighPath = new GH_Path(Ass.centroidsAInds[args.Id]);
                density += Ass.AssemblyObjects[neighPath, 0].Weight;
                // update neighbour object receiver value with current weight
                Ass.AssemblyObjects[neighPath, 0].ReceiverValue += AO.Weight;
            });

            return density;
        }

        #endregion

        #region compute candidates methods

        internal static double[] ComputeZeroMany(Assemblage _Ass, List<AssemblyObject> candidates) => candidates.Select(c => 0.0).ToArray();
        internal static double[] ComputeRandomMany(Assemblage _Ass, List<AssemblyObject> candidates) => candidates.Select(c => MathUtils.rnd.NextDouble()).ToArray();
        internal static double[] ComputeBBVolumeMany(Assemblage Ass, List<AssemblyObject> candidates)
        {
            BoundingBox bBox;

            double[] BBvolumes = new double[candidates.Count];

            // compute BBvolume for all candidates
            if (candidates.Count < Constants.parallelLimit)
                for (int i = 0; i < candidates.Count; i++)
                {
                    bBox = Ass.AssemblyObjects.Branch(Ass.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
                    bBox.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
                    BBvolumes[i] = bBox.Volume;
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    BoundingBox bBoxpar = Ass.AssemblyObjects.Branch(Ass.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
                    bBoxpar.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
                    BBvolumes[i] = bBoxpar.Volume;
                });

            return BBvolumes;
        }

        internal static double[] ComputeBBDiagonalMany(Assemblage Ass, List<AssemblyObject> candidates)
        {
            BoundingBox bBox;

            double[] BBdiagonals = new double[candidates.Count];

            // compute BBdiagonal for all candidates
            if (candidates.Count < Constants.parallelLimit)
                for (int i = 0; i < candidates.Count; i++)
                {
                    bBox = Ass.AssemblyObjects.Branch(Ass.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
                    bBox.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
                    BBdiagonals[i] = bBox.Diagonal.Length;
                }
            else
                Parallel.For(0, candidates.Count, i =>
                {
                    BoundingBox bBoxpar = Ass.AssemblyObjects.Branch(Ass.i_receiverBranchSeqInd)[0].CollisionMesh.GetBoundingBox(false);
                    bBoxpar.Union(candidates[i].CollisionMesh.GetBoundingBox(false));
                    BBdiagonals[i] = bBoxpar.Diagonal.Length;
                });

            return BBdiagonals;
        }

        internal static double[] ComputeScalarFieldMany(Assemblage Ass, List<AssemblyObject> AOs)
        {
            double[] scalarValues = new double[AOs.Count];

            // compute scalarvalue for all AOs
            if (AOs.Count < Constants.parallelLimit)
                for (int i = 0; i < AOs.Count; i++)
                    scalarValues[i] = ComputeScalarField(Ass, AOs[i]);

            else
                Parallel.For(0, AOs.Count, i =>
                {
                    scalarValues[i] = ComputeScalarField(Ass, AOs[i]);
                });

            return scalarValues;
        }

        internal static double[] ComputeScalarFieldInterpolatedMany(Assemblage Ass, List<AssemblyObject> AOs)
        {
            double[] scalarValues = new double[AOs.Count];

            // compute scalarvalue for all AOs
            if (AOs.Count < Constants.parallelLimit)
                for (int i = 0; i < AOs.Count; i++)
                    scalarValues[i] = ComputeScalarFieldInterpolated(Ass, AOs[i]);

            else
                Parallel.For(0, AOs.Count, i =>
                {
                    scalarValues[i] = ComputeScalarFieldInterpolated(Ass, AOs[i]);
                });

            return scalarValues;
        }

        internal static double ComputeVectorField(Assemblage Ass, AssemblyObject AO) => Vector3d.VectorAngle(AO.Direction, Ass.ExogenousSettings.Field.GetClosestVector(AO.ReferencePlane.Origin));

        internal static double[] ComputeVectorFieldMany(Assemblage Ass, List<AssemblyObject> AOs)
        {
            double[] vectorValues = new double[AOs.Count];

            // compute Vector angle value for all AOs
            if (AOs.Count < Constants.parallelLimit)
                for (int i = 0; i < AOs.Count; i++)
                    vectorValues[i] = ComputeVectorField(Ass, AOs[i]);
            else
                Parallel.For(0, AOs.Count, i =>
                {
                    vectorValues[i] = ComputeVectorField(Ass, AOs[i]);
                });

            return vectorValues;
        }

        internal static double ComputeVectorFieldBidirectional(Assemblage Ass, AssemblyObject AO) => 1 - Math.Abs(AO.Direction * Ass.ExogenousSettings.Field.GetClosestVector(AO.ReferencePlane.Origin));

        internal static double[] ComputeVectorFieldBidirectionalMany(Assemblage Ass, List<AssemblyObject> AOs)
        {
            double[] vectorValues = new double[AOs.Count];

            // compute bidirectional Vector angle value for all AOs
            if (AOs.Count < Constants.parallelLimit)
                for (int i = 0; i < AOs.Count; i++)
                    vectorValues[i] = ComputeVectorFieldBidirectional(Ass, AOs[i]);
            else
                Parallel.For(0, AOs.Count, i =>
                {
                    vectorValues[i] = ComputeVectorFieldBidirectional(Ass, AOs[i]);
                });

            return vectorValues;
        }

        internal static double ComputeVectorFieldInterpolated(Assemblage Ass, AssemblyObject AO) => Vector3d.VectorAngle(AO.Direction, Ass.ExogenousSettings.Field.GetInterpolatedVector(AO.ReferencePlane.Origin));

        internal static double[] ComputeVectorFieldInterpolatedMany(Assemblage Ass, List<AssemblyObject> AOs)
        {
            double[] vectorValues = new double[AOs.Count];

            // compute Vector angle value for all AOs
            if (AOs.Count < Constants.parallelLimit)
                for (int i = 0; i < AOs.Count; i++)
                    vectorValues[i] = ComputeVectorFieldInterpolated(Ass, AOs[i]);
            else
                Parallel.For(0, AOs.Count, i =>
                {
                    vectorValues[i] = ComputeVectorFieldInterpolated(Ass, AOs[i]);
                });

            return vectorValues;
        }

        internal static double ComputeVectorFieldBidirectionalInterpolated(Assemblage Ass, AssemblyObject AO) => 1 - Math.Abs(AO.Direction * Ass.ExogenousSettings.Field.GetInterpolatedVector(AO.ReferencePlane.Origin));

        internal static double[] ComputeVectorFieldBidirectionalInterpolatedMany(Assemblage Ass, List<AssemblyObject> AOs)
        {
            double[] vectorValues = new double[AOs.Count];

            // compute bidirectional Vector angle value for all AOs
            if (AOs.Count < Constants.parallelLimit)
                for (int i = 0; i < AOs.Count; i++)
                    vectorValues[i] = ComputeVectorFieldBidirectionalInterpolated(Ass, AOs[i]);
            else
                Parallel.For(0, AOs.Count, i =>
                {
                    vectorValues[i] = ComputeVectorFieldBidirectionalInterpolated(Ass, AOs[i]);
                });

            return vectorValues;
        }

        internal static double[] ComputeWRC(Assemblage Ass, List<AssemblyObject> candidates)
        {
            double[] wrcWeights = new double[candidates.Count];

            for (int i = 0; i < Ass.i_validRulesIndexes.Count; i++)
                wrcWeights[i] = Ass.i_receiverRules[Ass.i_validRulesIndexes[i]].iWeight;

            return wrcWeights;
        }

        #endregion

        #region Select Value Methods

        public static int SelectRandomIndex(double[] values) => (int)(MathUtils.rnd.NextDouble() * values.Length);

        public static int SelectMinIndex(double[] values)
        {
            double min = values[0];// double.MaxValue;
            int minindex = 0;// -1;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min)
                {
                    min = values[i];
                    minindex = i;
                }
            }

            return minindex;
        }

        public static int SelectMaxIndex(double[] values)
        {
            double max = values[0];//double.MinValue;
            int maxindex = 0;//-1;
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                    maxindex = i;
                }
            }

            return maxindex;
        }

        public static int SelectWRCIndex(double[] values)
        {
            // Weighted Random Choice among valid rules
            int[] iWeights = values.Select(v => (int)(v * 1000)).ToArray();
            int[] indexes = new int[values.Length];
            for (int i = 0; i < values.Length; i++)
                indexes[i] = i;

            return MathUtils.WeightedRandomChoice(indexes, iWeights);
        }

        #endregion Select Value Methods

        #region environment clash methods
        /// <summary>
        /// Checks environment compatibility of an AssemblyObject
        /// </summary>
        /// <param name="AO"><see cref="AssemblyObject"/> to verify</param>
        /// <param name="EnvironmentMeshes"></param>
        /// <returns>true if an object is not compatible with the <see cref="MeshEnvironment"/>s</returns>
        /// <remarks>An eventual Container is checked using collision mode</remarks>
        internal static bool EnvironmentClashCollision(AssemblyObject AO, List<MeshEnvironment> EnvironmentMeshes)
        {
            foreach (MeshEnvironment mEnv in EnvironmentMeshes)
            {

                switch (mEnv.Type)
                {
                    case EnvironmentType.Void: // controls only centroid in/out
                        if (mEnv.IsPointInvalid(AO.ReferencePlane.Origin)) return true;
                        break;
                    case EnvironmentType.Solid:
                        if (mEnv.CollisionCheck(AO.CollisionMesh)) return true;
                        goto case EnvironmentType.Void;
                    case EnvironmentType.Container:
                        goto case EnvironmentType.Solid;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks environment compatibility of an AssemblyObject
        /// </summary>
        /// <param name="AO"><see cref="AssemblyObject"/> to verify</param>
        /// <param name="EnvironmentMeshes"></param>
        /// <returns>true if an object is not compatible with the <see cref="MeshEnvironment"/>s</returns>
        /// <remarks>An eventual Container is checked using inclusion mode</remarks>
        internal static bool EnvironmentClashInclusion(AssemblyObject AO, List<MeshEnvironment> EnvironmentMeshes)
        {
            foreach (MeshEnvironment mEnv in EnvironmentMeshes)
            {

                switch (mEnv.Type)
                {
                    case EnvironmentType.Void: // controls only centroid in/out
                        if (mEnv.IsPointInvalid(AO.ReferencePlane.Origin)) return true;
                        break;
                    case EnvironmentType.Solid:
                        if (mEnv.CollisionCheck(AO.CollisionMesh)) return true;
                        goto case EnvironmentType.Void;
                    case EnvironmentType.Container:
                        goto case EnvironmentType.Void;
                }
            }

            return false;
        }


        #endregion
    }
}
