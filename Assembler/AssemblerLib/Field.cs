using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using Rhino.Geometry;
using Grasshopper;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

namespace AssemblerLib
{
    /// <summary>
    /// Stores spatially-distributed scalar, vector and integer weights values
    /// </summary>
    /// <remarks>test if field improves transforming scalars to integer values - i.e. input desired precision (3, 4, etc.) 
    /// and multiply 0-1 * 10^precision.
    /// Similarly, a LUT for Vector angles might be done (a Math.Cos lookup table) to speed up vector field computations?</remarks>
    public class Field
    {
        /// <summary>
        /// Array of Tensor points in the field
        /// </summary>
        public Tensor[] tensors;
        /// <summary>
        /// Points in Rhino.Collections.Point3dList format for fast neighbour search
        /// </summary>
        private Rhino.Collections.Point3dList points;
        /// <summary>
        /// RTree for radius-based search and interpolated value calculation
        /// </summary>
        private RTree pointsTree;
        /// <summary>
        /// Search Radius for interpolated value calculation
        /// </summary>
        private double searchRadius;
        /// <summary>
        /// Neighbour index array - for future implementation
        /// </summary>
        public int[][] topology;
        /// <summary>
        /// Field points color - for display purposes only
        /// </summary>
        public Color[] colors;

        /*
         FUTURE IMPLEMENTATION
        . Neighbour map - called topology (for effects like diffusion or CA strategies) - see WFC for implementation
        . Generate field from any object shape (brute force voxelization via bounding box - cull external points to be implemented)
         */

        /// <summary>
        /// Construct a Field from another Field (copy)
        /// </summary>
        /// <param name="otherField"></param>
        public Field(Field otherField)
        {
            points = otherField.points.Duplicate();
            pointsTree = RTree.CreateFromPointArray(otherField.points);
            searchRadius = otherField.searchRadius;
            tensors = (Tensor[])otherField.tensors.Clone();
            topology = otherField.topology;
            colors = otherField.colors;
        }

        #region Raw Data Constructors

        /// <summary>
        /// Construct a Field from a List of Point3d, DataTrees for scalar and vector (multiple values per point), and a DataTree for topology (neighbours map)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scalars"></param>
        /// <param name="vectors"></param>
        /// <param name="topology"></param>
        public Field(List<Point3d> points, DataTree<double> scalars, DataTree<Vector3d> vectors, DataTree<int> topology)
        {
            this.points = new Rhino.Collections.Point3dList();
            this.points.AddRange(points);
            pointsTree = RTree.CreateFromPointArray(points);
            searchRadius = points[0].DistanceTo(points[1]) * 1.2;

            // initialize tensors array and populate it
            tensors = new Tensor[points.Count];
            PopulateField(scalars, vectors);
            //for (int i = 0; i < points.Count; i++)
            //    tensors[i] = new Tensor(scalars.Branches[i], vectors.Branches[i]);

            // convert topology DataTree to bidimensional array of neighbours
            if (topology != null) this.topology = Utilities.ToJaggedArray(topology);
        }

        /// <summary>
        /// Construct a Field from a List of Point3d, and DataTrees for scalar and vector values (multiple values per point)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        public Field(List<Point3d> points, DataTree<double> scalar, DataTree<Vector3d> vector) : this(points, scalar, vector, null)
        { }

        /// <summary>
        /// Construct a Field from a List of Point3d, Lists for scalar and vector (single values per point), and a DataTree for topology (neighbours map)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        /// <param name="topology"></param>
        public Field(List<Point3d> points, List<double> scalar, List<Vector3d> vector, DataTree<int> topology)
        {
            this.points = new Rhino.Collections.Point3dList();
            this.points.AddRange(points);
            pointsTree = RTree.CreateFromPointArray(points);
            searchRadius = points[0].DistanceTo(points[1]) * 1.2;

            // initialize tensors array and populate it
            tensors = new Tensor[points.Count];
            PopulateField(scalar, vector);
            //for (int i = 0; i < points.Count; i++)
            //    tensors[i] = new Tensor(new[] { scalar[i] }, new[] { vector[i] });

            // convert topology DataTree to bidimensional array of neighbours
            if (topology != null) this.topology = Utilities.ToJaggedArray(topology);
        }

        /// <summary>
        /// Construct a Field from a List of Point3d, and Lists for scalar and vector (single values per point)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="scalar"></param>
        /// <param name="vector"></param>
        public Field(List<Point3d> points, List<double> scalar, List<Vector3d> vector) : this(points, scalar, vector, null)
        { }

        #endregion

        #region Geometry Constructors

        /// <summary>
        /// constructs an empty field from a Box, with individual resolutions in X, Y, and Z
        /// </summary>
        /// <param name="b"></param>
        /// <param name="resX"></param>
        /// <param name="resY"></param>
        /// <param name="resZ"></param>
        public Field(Box b, double resX, double resY, double resZ)
        {
            Vector3d diag = b.BoundingBox.Diagonal;

            int nX = Math.Max(1, (int)Math.Round(diag.X / resX));
            int nY = Math.Max(1, (int)Math.Round(diag.Y / resY));
            int nZ = Math.Max(1, (int)Math.Round(diag.Z / resZ));

            InitField(b, nX, nY, nZ);

        }

        /// <summary>
        /// constructs an empty field from a Box, with single resolution
        /// </summary>
        /// <param name="b"></param>
        /// <param name="res"></param>
        public Field(Box b, double res) : this(b, res, res, res)
        { }

        /// <summary>
        /// constructs an empty field from a Box, with individual number of points in X, Y, and Z
        /// </summary>
        /// <param name="b"></param>
        /// <param name="nX"></param>
        /// <param name="nY"></param>
        /// <param name="nZ"></param>
        public Field(Box b, int nX, int nY, int nZ)
        {
            InitField(b, nX, nY, nZ);
        }

        /// <summary>
        /// constructs an empty field from a Box, with n points on the largest dimension and according numbers on other
        /// </summary>
        /// <param name="b"></param>
        /// <param name="n"></param>
        public Field(Box b, int n)
        {
            //Vector3d diag = b.BoundingBox.Diagonal;

            Point3d[] corners = b.GetCorners();
            double x, y, z;
            x = corners[0].DistanceTo(corners[1]);
            y = corners[0].DistanceTo(corners[3]);
            z = corners[0].DistanceTo(corners[4]);

            double maxDim = Math.Max(Math.Max(x, y), z);
            double res = maxDim / n;
            int nX = Math.Max(1, (int)Math.Round(x / res));
            int nY = Math.Max(1, (int)Math.Round(y / res));
            int nZ = Math.Max(1, (int)Math.Round(z / res));
            //double maxDim = Math.Max(Math.Max(diag.X, diag.Y), diag.Z);
            //double res = maxDim / n;
            //int nX = Math.Max(1, (int)Math.Round(diag.X / res));
            //int nY = Math.Max(1, (int)Math.Round(diag.Y / res));
            //int nZ = Math.Max(1, (int)Math.Round(diag.Z / res));

            InitField(b, nX, nY, nZ);

        }

        ///// <summary>
        ///// constructs an empty field from a Mesh and an orientation plane, with n points on the largest dimension and according numbers on other
        ///// </summary>
        ///// <param name="M"></param>
        ///// <param name="P"></param>
        ///// <param name="n"></param>
        //public Field(Mesh M, Plane P, int n) : this(new Box(P, M), n)
        //{ }

        ///// <summary>
        ///// constructs an empty field from a Mesh and an orientation plane, with individual number of points in X, Y, and Z
        ///// </summary>
        ///// <param name="M"></param>
        ///// <param name="P"></param>
        ///// <param name="nX"></param>
        ///// <param name="nY"></param>
        ///// <param name="nZ"></param>
        //public Field(Mesh M, Plane P, int nX, int nY, int nZ) : this(new Box(P, M), nX, nY, nZ)
        //{ }

        private void InitField(Box b, int nX, int nY, int nZ)
        {

            double resX = 1.0 / nX;
            double resY = 1.0 / nY;
            double resZ = 1.0 / nZ;

            points = new Rhino.Collections.Point3dList();

            for (int i = 0; i < nX; i++)
                for (int j = 0; j < nY; j++)
                    for (int k = 0; k < nZ; k++)
                        points.Add(b.PointAt((i + 0.5) * resX, (j + 0.5) * resY, (k + 0.5) * resZ));

            pointsTree = RTree.CreateFromPointArray(points);

            if (points.Count == 1) searchRadius = b.BoundingBox.Diagonal.Length * 0.6;
            else searchRadius = points[0].DistanceTo(points[1]) * 1.2;

            // initialize tensors array
            tensors = new Tensor[points.Count];

            // costruct topology

        }

        #endregion

        /// <summary>
        /// Populate Field with scalar values - 1 value per Field point
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <returns></returns>
        public bool PopulateScalars(List<double> scalarValues)
        {
            if (scalarValues.Count != points.Count) return false;

            scalarValues = Utilities.NormalizeRange(scalarValues);

            if (tensors[0] == null)
                tensors = scalarValues.Select(s => new Tensor(new[] { s })).ToArray();
            else for (int i = 0; i < scalarValues.Count; i++)
                    tensors[i].scalar = new[] { scalarValues[i] };

            return true;
        }

        /// <summary>
        /// Populate Field with scalar values - multiple values per Field point
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <returns></returns>
        public bool PopulateScalars(DataTree<double> scalarValues)
        {
            if (scalarValues.BranchCount != points.Count) return false;

            scalarValues = Utilities.NormalizeRanges(scalarValues);

            if (tensors[0] == null)
                for (int i = 0; i < scalarValues.BranchCount; i++)
                    tensors[i] = new Tensor(scalarValues.Branches[i]);
            else
                for (int i = 0; i < scalarValues.BranchCount; i++)
                    tensors[i].scalar = scalarValues.Branches[i].ToArray();

            return true;
        }

        /// <summary>
        /// Populate Field with vector values - 1 value per Field point
        /// </summary>
        /// <param name="vectorValues"></param>
        /// <returns></returns>
        public bool PopulateVectors(List<Vector3d> vectorValues)
        {
            if (vectorValues.Count != points.Count) return false;

            if (tensors[0] == null)
                tensors = vectorValues.Select(v => new Tensor(new[] { v })).ToArray();
            else for (int i = 0; i < vectorValues.Count; i++)
                    tensors[i].vector = new[] { vectorValues[i] };

            return true;
        }

        /// <summary>
        /// Populate Field with vector values - multiple values per Field point
        /// </summary>
        /// <param name="vectorValues"></param>
        /// <returns></returns>
        public bool PopulateVectors(DataTree<Vector3d> vectorValues)
        {
            if (vectorValues.BranchCount != points.Count) return false;

            if (tensors[0] == null)
                for (int i = 0; i < vectorValues.BranchCount; i++)
                    tensors[i] = new Tensor(vectorValues.Branches[i]);
            else
                for (int i = 0; i < vectorValues.BranchCount; i++)
                    tensors[i].vector = vectorValues.Branches[i].ToArray();

            return true;
        }

        /// <summary>
        /// Populates Field - a single scalar and vector for each Field point, no iWeights
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <param name="vectorValues"></param>
        /// <returns></returns>
        public bool PopulateField(List<double> scalarValues, List<Vector3d> vectorValues)
        {

            if (vectorValues.Count != points.Count || scalarValues.Count != points.Count) return false;

            PopulateScalars(scalarValues);
            PopulateVectors(vectorValues);

            return true;
        }

        /// <summary>
        /// Populates Field - lists of scalars and vectors for each Field point, no iWeights
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <param name="vectorValues"></param>
        /// <returns></returns>
        public bool PopulateField(DataTree<double> scalarValues, DataTree<Vector3d> vectorValues)
        {
            if (vectorValues.BranchCount != points.Count || scalarValues.BranchCount != points.Count) return false;

            PopulateScalars(scalarValues);
            PopulateVectors(vectorValues);

            return true;
        }

        /// <summary>
        /// Populates Field - lists of scalars, vectors, and data tree of iWeights for each Field point
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <param name="vectorValues"></param>
        /// <param name="iWeights"></param>
        /// <returns></returns>
        public bool PopulateField(List<double> scalarValues, List<Vector3d> vectorValues, DataTree<int> iWeights)
        {
            if (vectorValues.Count != points.Count || scalarValues.Count != points.Count || iWeights.BranchCount != points.Count) return false;

            scalarValues = Utilities.NormalizeRange(scalarValues);

            for (int i = 0; i < vectorValues.Count; i++)
                tensors[i] = new Tensor(new[] { scalarValues[i] }, new[] { vectorValues[i] });

            PopulateiWeights(iWeights);

            return true;
        }

        /// <summary>
        /// Populates Field - data trees of scalars, vectors and iWeights for each Field point
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <param name="vectorValues"></param>
        /// <param name="iWeights"></param>
        /// <returns></returns>
        public bool PopulateField(DataTree<double> scalarValues, DataTree<Vector3d> vectorValues, DataTree<int> iWeights)
        {
            if (vectorValues.BranchCount != points.Count || scalarValues.BranchCount != points.Count || iWeights.BranchCount != points.Count) return false;

            scalarValues = Utilities.NormalizeRanges(scalarValues);

            for (int i = 0; i < vectorValues.BranchCount; i++)
                tensors[i] = new Tensor(scalarValues.Branches[i], vectorValues.Branches[i], iWeights.Branches[i]);

            return true;
        }

        /// <summary>
        /// Generates a color for each Field point based on a list of attractor points and respective colors 
        /// </summary>
        /// <param name="attColors"></param>
        /// <param name="attractors"></param>
        /// <param name="blend"></param>
        public bool GenerateColorsByAttractors(List<Color> attColors, List<Point3d> attractors, bool blend)
        {
            if (attColors.Count != attractors.Count) return false;

            colors = new Color[points.Count];

            DataTree<int> cWeights = new DataTree<int>();

            for (int i = 0; i < attColors.Count; i++)
            {
                GH_Path p = new GH_Path(i);
                cWeights.Add(attColors[i].R, p);
                cWeights.Add(attColors[i].G, p);
                cWeights.Add(attColors[i].B, p);
            }

            int[][] rgbValues = new int[points.Count][];

            if (blend)
                rgbValues = InterpolateIntegers(cWeights, attractors);
            else
                rgbValues = AllocateIntegers(cWeights, attractors);

            colors = rgbValues.Select(c => Color.FromArgb(c[0], c[1], c[2])).ToArray();

            return true;
        }

        /// <summary>
        /// Generates colors according to scalar values at given index in the field\nthreshold is used when blend is false
        /// </summary>
        /// <param name="lowVal"></param>
        /// <param name="hiVal"></param>
        /// <param name="index"></param>
        /// <param name="threshold"></param>
        /// <param name="blend"></param>
        public bool GenerateScalarColors(Color lowVal, Color hiVal, int index, double threshold, bool blend)
        {
            // return if scalar values are not present or sInd is invalid
            if (tensors[0] == null) return false;
            if (tensors[0].scalar == null) return false;
            if (tensors[0].scalar.Length == 0 || tensors[0].scalar.Length <= index) return false;

            colors = new Color[points.Count];
            List<Color> attColors = new List<Color> { lowVal, hiVal };
            DataTree<int> cWeights = new DataTree<int>();

            for (int i = 0; i < attColors.Count; i++)
            {
                GH_Path p = new GH_Path(i);
                cWeights.Add(attColors[i].R, p);
                cWeights.Add(attColors[i].G, p);
                cWeights.Add(attColors[i].B, p);
            }

            int[][] rgbValues = new int[points.Count][];

            if (blend)
                rgbValues = InterpolateIntegersScalar(cWeights, index);
            else
                rgbValues = AllocateIntegersScalar(cWeights, threshold, index);

            colors = rgbValues.Select(c => Color.FromArgb(c[0], c[1], c[2])).ToArray();

            return true;
        }

        /// <summary>
        /// Distribute iWeights according to scalar values in the field at index ind
        /// </summary>
        /// <param name="weights"></param>
        /// <param name="threshold"></param>
        /// <param name="index"></param>
        /// <param name="blend"></param>
        public bool DistributeiWeightsScalar(DataTree<int> weights, double threshold, int index, bool blend)
        {
            // if tensors aren't populated yet or there are no scalar values at the index or index exceeds scalar vector length return
            if (tensors[0] == null) return false;
            if (tensors[0].scalar == null) return false;
            if (index >= tensors[0].scalar.Length) return false;

            if (blend)
                return PopulateiWeights(InterpolateIntegersScalar(weights, index));

            else
                return PopulateiWeights(AllocateIntegersScalar(weights, threshold, index));
        }

        /// <summary>
        /// Distribute attractor-based iWeights, with an option for blending
        /// </summary>
        /// <param name="weights">Data Tree of input weights\nOne branch for each attractor point</param>
        /// <param name="attractors">List of attractor points</param>
        /// <param name="blend">True for smooth blending, false for closest-point criteria</param>
        public bool DistributeiWeights(DataTree<int> weights, List<Point3d> attractors, bool blend)
        {
            // if tensors aren't populated yet initialize the tensors array with placeholder weights
            if (tensors[0] == null)
            {
                int[] w = new[] { 0 };
                for (int i = 0; i < tensors.Length; i++)
                    tensors[i] = new Tensor(w);
            }

            if (blend)
                return PopulateiWeights(InterpolateIntegers(weights, attractors));

            else
                return PopulateiWeights(AllocateIntegers(weights, attractors));

        }

        /// <summary>
        /// Populate field with weights distribution - 1 value per Field point
        /// </summary>
        /// <param name="iWeights"></param>
        /// <returns></returns>
        public bool PopulateiWeights(List<int> iWeights)
        {
            if (iWeights.Count != points.Count) return false;

            //iWeights = Utilities.NormalizeRange(iWeights);

            if (tensors[0] == null)
                tensors = iWeights.Select(s => new Tensor(new[] { s })).ToArray();
            else for (int i = 0; i < iWeights.Count; i++)
                    tensors[i].iWeights = new[] { iWeights[i] };

            return true;
        }

        /// <summary>
        /// Populate field with weights distribution
        /// </summary>
        /// <param name="iWeights"></param>
        /// <returns>true if operation was successful</returns>
        public bool PopulateiWeights(DataTree<int> iWeights)
        {
            if (iWeights.BranchCount != points.Count) return false;

            // if tensors aren't populated yet
            if (tensors[0] == null)
                for (int i = 0; i < iWeights.BranchCount; i++)
                    tensors[i] = new Tensor(iWeights.Branches[i]);
            else
            {
                for (int i = 0; i < tensors.Length; i++)
                    tensors[i].iWeights = iWeights.Branches[i].ToArray();
            }
            return true;
        }

        /// <summary>
        /// Populate field with weights distribution
        /// </summary>
        /// <param name="iWeights"></param>
        /// <returns>true if operation was successful</returns>
        private bool PopulateiWeights(int[][] iWeights)
        {
            if (iWeights.Length != points.Count) return false;
            // if tensors aren't populated yet
            if (tensors[0] == null)
                tensors = iWeights.Select(iW => new Tensor(iW)).ToArray();
            else
            {
                for (int i = 0; i < tensors.Length; i++)
                    tensors[i].iWeights = iWeights[i];
            }
            return true;
        }

        /// <summary>
        /// Interpolates integers according to a list of attractor points
        /// </summary>
        /// <param name="weights">Data Tree of input weights\nOne branch for each attractor point</param>
        /// <param name="attractors">List of attractor points</param>
        /// <returns>Jagged array of interpolated integers</returns>
        private int[][] InterpolateIntegers(DataTree<int> weights, List<Point3d> attractors)
        {
            int[][] integerArray = new int[points.Count][];

            Parallel.For(0, points.Count, i =>
            //for(int i = 0; i < points.Count; i++)
            {
                integerArray[i] = new int[weights.Branches[0].Count];
                for (int j = 0; j < weights.Branches[0].Count; j++)
                {
                    double dist, weight = 0, distW = 0;
                    for (int k = 0; k < attractors.Count; k++)
                    {
                        dist = 1 / (0.01 + points[i].DistanceTo(attractors[k]));
                        weight += weights.Branches[k][j] * dist;
                        distW += dist;
                    }
                    weight /= distW;

                    integerArray[i][j] = (int)Math.Round(weight);
                }

            });

            return integerArray;
        }

        /// <summary>
        /// Interpolates integers according to the Field scalar values
        /// </summary>
        /// <param name="weights">Data Tree of input weights\nOtwo branches for integers corresponding to scalar values extremes</param>
        /// <param name="index"></param>
        /// <returns>Jagged array of interpolated integers</returns>
        private int[][] InterpolateIntegersScalar(DataTree<int> weights, int index)
        {
            int[][] integerArray = new int[points.Count][];

            Parallel.For(0, points.Count, i =>
            //for(int i = 0; i < points.Count; i++)
            {
                integerArray[i] = new int[weights.Branches[0].Count];
                for (int j = 0; j < weights.Branches[0].Count; j++)
                {
                    double weight = weights.Branches[0][j] * (1 - tensors[i].scalar[index]) + weights.Branches[1][j] * tensors[i].scalar[index];
                    integerArray[i][j] = (int)Math.Round(weight);
                }

            });

            return integerArray;
        }

        /// <summary>
        /// Allocates integers (no interpolation) according to the closest corresponding attractor point
        /// </summary>
        /// <param name="weights">Data Tree of input weights\nOne branch for each attractor point</param>
        /// <param name="attractors">List of attractor points</param>
        /// <returns>Jagged array of allocated integers</returns>
        private int[][] AllocateIntegers(DataTree<int> weights, List<Point3d> attractors)
        {

            int[][] integerArray = new int[points.Count][];

            Parallel.For(0, points.Count, i =>
            //for(int i = 0; i < points.Length; i++)
            {

                double dist, minDist = double.MaxValue;
                int minInd = 0;

                for (int k = 0; k < attractors.Count; k++)
                {
                    dist = points[i].DistanceToSquared(attractors[k]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        minInd = k;
                    }
                }

                integerArray[i] = new int[weights.Branches[minInd].Count];
                for (int j = 0; j < weights.Branches[minInd].Count; j++)
                {
                    integerArray[i][j] = weights.Branches[minInd][j];
                }

            });

            return integerArray;
        }

        /// <summary>
        /// Allocates integers (no interpolation) according to the Field scalar values and a threshold
        /// </summary>
        /// <param name="weights">Data Tree of input weights\ntwo branches for integers corresponding to scalar values extremes</param>
        /// <param name="threshold"></param>
        /// <param name="index"></param>
        /// <returns>Jagged array of distributed weights</returns>
        private int[][] AllocateIntegersScalar(DataTree<int> weights, double threshold, int index)
        {

            int[][] integerArray = new int[points.Count][];

            Parallel.For(0, points.Count, i =>
            //for(int i = 0; i < points.Length; i++)
            {
                integerArray[i] = new int[weights.Branches[0].Count];
                for (int j = 0; j < weights.Branches[0].Count; j++)
                    integerArray[i][j] = tensors[i].scalar[index] < threshold ? weights.Branches[0][j] : weights.Branches[1][j];

            });

            return integerArray;
        }

        /// <summary>
        /// Gets index of closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public int GetClosestIndex(Point3d P)
        {
            return points.ClosestIndex(P);
        }

        /// <summary>
        /// Gets indexes of closest Field points to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public List<int> GetNeighbourIndexes(Point3d P)
        {
            // find neighbours in Assemblage (remove receving object?)
            List<int> neighInd = new List<int>();
            // collision radius is a field of AssemblyObjects
            pointsTree.Search(new Sphere(P, searchRadius), (object sender, RTreeEventArgs e) =>
            {
                // recover the AssemblyObject index related to the found centroid
                neighInd.Add(e.Id);
            });

            return neighInd;
        }

        /// <summary>
        /// Gets the first scalar value for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public double GetClosestScalar(Point3d P)
        {
            return tensors[points.ClosestIndex(P)].GetScalar();
        }

        /// <summary>
        /// Gets array of scalar values for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public double[] GetClosestScalars(Point3d P)
        {
            return tensors[points.ClosestIndex(P)].GetScalars();
        }

        /// <summary>
        /// Get interpolation of first scalar value for a sample point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public double GetInterpolatedScalar(Point3d P)
        {
            // find neighbours in Assemblage (remove receving object?)
            List<double> neighScal = GetNeighbourScalars(P);

            double intScal = 0;
            if (neighScal.Count == 0) return intScal;

            for (int i = 0; i < neighScal.Count; i++)
                intScal += neighScal[i];
            intScal /= neighScal.Count;

            return intScal;
        }

        /// <summary>
        /// Get first scalar values near a sample point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public List<double> GetNeighbourScalars(Point3d P)
        {
            // find neighbours in Assemblage (remove receving object?)
            List<double> neighScal = new List<double>();
            pointsTree.Search(new Sphere(P, searchRadius), (object sender, RTreeEventArgs e) =>
            {
                // get the scalar neighbours values
                neighScal.Add(tensors[e.Id].GetScalar());
            });

            return neighScal;
        }

        /// <summary>
        /// Gets the first vector value for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public Vector3d GetClosestVector(Point3d P)
        {
            return tensors[points.ClosestIndex(P)].GetVector();
        }

        /// <summary>
        /// Get interpolation of first vector value for a sample point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public Vector3d GetInterpolatedVector(Point3d P)
        {
            List<Vector3d> neighVec = GetNeighbourVectors(P);

            Vector3d intVec = new Vector3d();
            if (neighVec.Count == 0) return intVec;

            foreach (Vector3d v in neighVec) intVec += v;

            intVec /= neighVec.Count;

            intVec.Unitize();

            return intVec;
        }

        public List<Vector3d> GetNeighbourVectors(Point3d P)
        {
            // find neighbours in Assemblage (remove receving object?)
            List<Vector3d> neighVec = new List<Vector3d>();
            // collision radius is a field of AssemblyObjects
            pointsTree.Search(new Sphere(P, searchRadius), (object sender, RTreeEventArgs e) =>
            {
                // recover the AssemblyObject index related to the found centroid
                neighVec.Add(tensors[e.Id].GetVector());
            });

            return neighVec;

        }

        /// <summary>
        /// Gets array of vector values for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public Vector3d[] GetClosestVectors(Point3d P)
        {
            return tensors[points.ClosestIndex(P)].GetVectors();
        }

        /// <summary>
        /// Gets array of integer weights for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public int[] GetClosestiWeights(Point3d P)
        {
            return tensors[points.ClosestIndex(P)].iWeights;
        }

        /// <summary>
        /// Gets the first scalar value for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public double GetScalar(int i)
        {
            if (tensors.Length > 0 && tensors[i] != null && tensors[i].scalar != null)
                return tensors[i].GetScalar();
            else return double.NaN;
        }

        /// <summary>
        /// Gets the array of scalar values for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public double[] GetScalars(int i)
        {
            if (tensors.Length > 0 && tensors[i] != null && tensors[i].scalar != null)
                return tensors[i].GetScalars();
            else return null;
        }

        /// <summary>
        /// Gets the first vector value for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Vector3d GetVector(int i)
        {
            if (tensors.Length > 0 && tensors[i] != null && tensors[i].vector != null)
                return tensors[i].GetVector();
            else return Vector3d.Zero;
        }

        /// <summary>
        /// Gets the array of vector values for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Vector3d[] GetVectors(int i)
        {
            if (tensors.Length > 0 && tensors[i] != null && tensors[i].vector != null)
                return tensors[i].GetVectors();
            else return null;
        }

        /// <summary>
        /// Gets the array of integer weights for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public int[] GetiWeights(int i)
        {
            if (tensors.Length > 0 && tensors[i] != null && tensors[i].iWeights != null)
                return tensors[i].iWeights;
            else return null;
        }

        /// <summary>
        /// Gets all Field Points
        /// </summary>
        /// <returns></returns>
        public Point3d[] GetPoints()
        {
            return points.Select(p => p).ToArray();
        }

        /// <summary>
        /// Gets all Field Points in GH_Point format (for fast output)
        /// </summary>
        /// <returns></returns>
        public GH_Point[] GetGH_Points()
        {
            return points.Select(p => new GH_Point(p)).ToArray();
        }

        /// <summary>
        /// Gets all Field scalar values as DataTree
        /// </summary>
        /// <returns></returns>
        public DataTree<double> GetScalars()
        {
            DataTree<double> scalars = new DataTree<double>();

            if (tensors.Length > 0 && tensors[0] != null && tensors[0].scalar != null)
                for (int j = 0; j < tensors.Length; j++)
                    scalars.AddRange(tensors[j].scalar, new GH_Path(j));

            return scalars;
        }

        /// <summary>
        /// Gets all Field scalar values as DataTree in GH_Number format (for fast output)
        /// </summary>
        /// <returns></returns>
        public DataTree<GH_Number> GetGH_Scalars()
        {
            DataTree<GH_Number> scalars = new DataTree<GH_Number>();

            if (tensors.Length > 0 && tensors[0] != null && tensors[0].scalar != null)
                for (int j = 0; j < tensors.Length; j++)
                    scalars.AddRange(tensors[j].scalar.Select(s => new GH_Number(s)).ToList(), new GH_Path(j));

            return scalars;
        }

        /// <summary>
        /// Gets all Field vector values as DataTree
        /// </summary>
        /// <returns></returns>
        public DataTree<Vector3d> GetVectors()
        {
            DataTree<Vector3d> vectors = new DataTree<Vector3d>();

            if (tensors.Length > 0 && tensors[0] != null && tensors[0].vector != null)
                for (int j = 0; j < tensors.Length; j++)
                    vectors.AddRange(tensors[j].vector, new GH_Path(j));

            return vectors;
        }

        /// <summary>
        /// Gets all Field vector values as DataTree in GH_Vector format (for fast output)
        /// </summary>
        /// <returns></returns>
        public DataTree<GH_Vector> GetGH_Vectors()
        {
            DataTree<GH_Vector> vectors = new DataTree<GH_Vector>();

            if (tensors.Length > 0 && tensors[0] != null && tensors[0].vector != null)
                for (int j = 0; j < tensors.Length; j++)
                    vectors.AddRange(tensors[j].vector.Select(v => new GH_Vector(v)).ToList(), new GH_Path(j));

            return vectors;
        }

        /// <summary>
        /// Gets all Field integer Weights as DataTree
        /// </summary>
        /// <returns></returns>
        public DataTree<int> GetiWeights()
        {
            DataTree<int> data = new DataTree<int>();

            if (tensors.Length > 0 && tensors[0] != null && tensors[0].iWeights != null)
                for (int j = 0; j < tensors.Length; j++)
                    data.AddRange(tensors[j].iWeights, new GH_Path(j));

            return data;
        }

        /// <summary>
        /// Gets all Field integer Weights as DataTree in GH_Integer format (for fast output)
        /// </summary>
        /// <returns></returns>
        public DataTree<GH_Integer> GetGH_iWeights()
        {
            DataTree<GH_Integer> data = new DataTree<GH_Integer>();

            if (tensors.Length > 0 && tensors[0] != null && tensors[0].iWeights != null)
                for (int j = 0; j < tensors.Length; j++)
                    data.AddRange(tensors[j].iWeights.Select(i => new GH_Integer(i)).ToList(), new GH_Path(j));

            return data;
        }

        public override string ToString()
        {
            return string.Format("Assembler Field containing {0} points", points.Count);
        }

    }
}
