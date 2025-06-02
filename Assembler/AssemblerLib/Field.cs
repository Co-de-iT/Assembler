using AssemblerLib.Utils;
using Grasshopper;
using Grasshopper.GUI.Gradient;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Collections;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace AssemblerLib
{
    /*
     FUTURE IMPLEMENTATION
    . This class needs a refactoring to simplify stuff and reduce the number of methods
    . use Graph class for Topology (refine said class implementation)
    */

    /// <summary>
    /// Stores spatially-distributed scalar, vector and integer weights values
    /// </summary>
    /// <remarks>
    /// Test if Field improves transforming scalars to integer values - i.e. input desired precision (3, 4, etc.) 
    /// and multiply 0-1 * 10^precision.
    /// Similarly, a LUT for Vector angles might be done (a Math.Cos lookup table) to speed up vector Field computations?</remarks>
    public class Field
    {
        /// <summary>
        /// Array of Tensor points in the Field
        /// </summary>
        public Tensor[] Tensors
        { get; set; }
        /// <summary>
        /// Neighbour index array - for future implementation
        /// </summary>
        public int[][] Topology
        { get; set; }
        /// <summary>
        /// stores neighbour transmission coefficients for signal propagation
        /// </summary>
        public double[][] TransCoeff
        { get; set; }
        /// <summary>
        /// stores values for stigmergy calculation
        /// </summary>
        public double[] StigValues
        { get; set; }
        /// <summary>
        /// Field points color - for display purposes only
        /// </summary>
        public Color[] Colors
        { get; set; }
        /// <summary>
        /// Maximum distance (square, for faster calculations) after which a point is considered outside the Field
        /// </summary>
        public double MaxDistSquare
        { get; internal set; }

        /// <summary>
        /// number of cells in X direction - 0 for sparse Fields
        /// </summary>
        public int Nx
        { get; private set; }
        /// <summary>
        /// number of cells in Y direction - 0 for sparse Fields
        /// </summary>
        public int Ny
        { get; private set; }
        /// <summary>
        /// number of cells in Z direction - 0 for sparse Fields
        /// </summary>
        public int Nz
        { get; private set; }

        /// <summary>
        /// Points in Rhino.Collections.Point3dList format for fast neighbour search
        /// </summary>
        private Point3dList points;
        /// <summary>
        /// RTree for radius-based search and interpolated value calculation
        /// </summary>
        private RTree pointsTree;
        /// <summary>
        /// Search Radius for interpolated value calculation
        /// </summary>
        private double searchRadius;

        /// <summary>
        /// Construct a Field from another Field (copy)
        /// </summary>
        /// <param name="otherField"></param>
        public Field(Field otherField)
        {
            points = otherField.points.Duplicate();
            pointsTree = RTree.CreateFromPointArray(otherField.points);
            searchRadius = otherField.searchRadius;
            MaxDistSquare = otherField.MaxDistSquare;
            Tensors = (Tensor[])otherField.Tensors.Clone();
            // see if this works, otherwise revert to the line commented below
            Topology = otherField.Topology == null ? null : (int[][])otherField.Topology.Clone();
            // Topology = otherField.Topology;
            TransCoeff = otherField.TransCoeff == null ? null : (double[][])otherField.TransCoeff.Clone();
            StigValues = otherField.StigValues == null ? null : (double[])otherField.StigValues.Clone();
            Colors = otherField.Colors == null ? null : (Color[])otherField.Colors.Clone();
            Nx = otherField.Nx;
            Ny = otherField.Ny;
            Nz = otherField.Nz;
        }

        #region Sparse Field Constructor

        /// <summary>
        /// Construct an empty Field from a List of Point3d, and DataTrees for Topology and transmission coefficients
        /// </summary>
        /// <param name="points"></param>
        /// <param name="Topology"></param>
        /// <param name="TransCoeff"></param>
        public Field(List<Point3d> points, DataTree<int> Topology, DataTree<double> TransCoeff)
        {
            this.points = new Point3dList();
            this.points.AddRange(points);
            pointsTree = RTree.CreateFromPointArray(points);

            // initialize Tensors array
            Tensors = new Tensor[points.Count];

            // convert Topology DataTree to bidimensional array of neighbours
            if (Topology != null) this.Topology = DataUtils.ToJaggedArray(Topology);
            if (TransCoeff != null) this.TransCoeff = DataUtils.ToJaggedArray(TransCoeff);

            Nx = Ny = Nz = 0;

            // compute search radius and max distance
            ComputeSearchRadiusAndMaxDist();
        }
        #endregion

        #region Dense Field Constructors

        /// <summary>
        /// constructs an empty Field from a Box, with individual resolutions in X, Y, and Z
        /// </summary>
        /// <param name="box"></param>
        /// <param name="resX"></param>
        /// <param name="resY"></param>
        /// <param name="resZ"></param>
        public Field(Box box, double resX, double resY, double resZ)
        {
            Vector3d diag = box.BoundingBox.Diagonal;

            Nx = Math.Max(1, (int)Math.Round(diag.X / resX));
            Ny = Math.Max(1, (int)Math.Round(diag.Y / resY));
            Nz = Math.Max(1, (int)Math.Round(diag.Z / resZ));

            InitializeField(box, Nx, Ny, Nz);
        }

        /// <summary>
        /// constructs an empty Field from a Box, with individual number of points in X, Y, and Z
        /// </summary>
        /// <param name="box"></param>
        /// <param name="nX"></param>
        /// <param name="nY"></param>
        /// <param name="nZ"></param>
        public Field(Box box, int nX, int nY, int nZ)
        {
            this.Nx = nX;
            this.Ny = nY;
            this.Nz = nZ;
            InitializeField(box, nX, nY, nZ);
        }

        /// <summary>
        /// constructs an empty Field from a Box, with n points on the largest dimension and according numbers on other
        /// </summary>
        /// <param name="box"></param>
        /// <param name="n"></param>
        public Field(Box box, int n)
        {
            Point3d[] corners = box.GetCorners();
            double x, y, z;
            x = corners[0].DistanceTo(corners[1]);
            y = corners[0].DistanceTo(corners[3]);
            z = corners[0].DistanceTo(corners[4]);

            double maxDim = Math.Max(Math.Max(x, y), z);
            double res = maxDim / n;
            Nx = Math.Max(1, (int)Math.Round(x / res));
            Ny = Math.Max(1, (int)Math.Round(y / res));
            Nz = Math.Max(1, (int)Math.Round(z / res));

            InitializeField(box, Nx, Ny, Nz);
        }

        #endregion

        #region Initialization Methods
        private void InitializeField(Box box, int nX, int nY, int nZ)
        {
            double resX = 1.0 / nX;
            double resY = 1.0 / nY;
            double resZ = 1.0 / nZ;

            points = new Point3dList();

            for (int i = 0; i < nX; i++)
                for (int j = 0; j < nY; j++)
                    for (int k = 0; k < nZ; k++)
                        points.Add(box.PointAt((i + 0.5) * resX, (j + 0.5) * resY, (k + 0.5) * resZ));

            pointsTree = RTree.CreateFromPointArray(points);

            if (points.Count == 1) searchRadius = box.BoundingBox.Diagonal.Length * 0.5 * Constants.SafeScaleMultiplier;
            else searchRadius = points[0].DistanceTo(points[1]) * Constants.SafeScaleMultiplier;
            MaxDistSquare = searchRadius * searchRadius;

            // initialize Tensors array
            Tensors = new Tensor[points.Count];

            // construct Topology
            Topology = ConstructTopology(points, nX, nY, nZ, resX, resY, resZ);
        }

        private int[][] ConstructTopology(Point3dList points, int nX, int nY, int nZ, double resX, double resY, double resZ)
        {
            // compute transmission weights as inverse of their length
            double dXw = 1 / resX;
            double dYw = 1 / resY;
            double dZw = 1 / resZ;
            double dXYw = 1 / Math.Sqrt(resX * resX + resY * resY);
            double dXZw = 1 / Math.Sqrt(resX * resX + resZ * resZ);
            double dYZw = 1 / Math.Sqrt(resY * resY + resZ * resZ);
            double dXYZw = 1 / Math.Sqrt(resX * resX + resY * resY + resZ * resZ);

            /*
             TransCoeff calculation:
            . define inverse lengths for each trait
            . add it to the coeff and to a total sum
            . divide coeffs for total sum
             */

            double totalWeight;// = 2 * (resXcoeff + resYcoeff + resZcoeff) + 4 * (dXYcoeff + dXZcoeff + dYZcoeff + dXYZcoeff); // if all neighbours are present

            // construct Topology
            int[][] topology = new int[points.Count][];
            TransCoeff = new double[points.Count][];
            int[] dimIndexes;
            List<int> neighInd;
            List<double> neighCoeff;

            for (int i = 0; i < points.Count; i++)
            {
                dimIndexes = GetDimensionalIndices(i, nY, nZ);
                neighInd = new List<int>();
                neighCoeff = new List<double>();
                totalWeight = 0;
                // max 26 neighbours
                // add perpendicular neighbours (max 6)
                if (dimIndexes[0] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1], dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dXw);
                }
                if (dimIndexes[0] < nX - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1], dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dXw);
                }
                if (dimIndexes[1] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1] - 1, dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dYw);
                }
                if (dimIndexes[1] < nY - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1] + 1, dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dYw);
                }
                if (dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1], dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dZw);
                }
                if (dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1], dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dZw);
                }

                // add mid point diagonal neighbours (max 12 - 4 on each plane)
                // . XY plane
                if (dimIndexes[0] > 0 && dimIndexes[1] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1] - 1, dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                if (dimIndexes[0] > 0 && dimIndexes[1] < nY - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1] + 1, dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[1] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1] - 1, dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[1] < nY - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1] + 1, dimIndexes[2], nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                // . XZ plane
                if (dimIndexes[0] > 0 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1], dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dXZw);
                }
                if (dimIndexes[0] > 0 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1], dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1], dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1], dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dXYw);
                }
                // . YZ plane
                if (dimIndexes[1] > 0 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1] - 1, dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dYZw);
                }
                if (dimIndexes[1] > 0 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1] - 1, dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dYZw);
                }
                if (dimIndexes[1] < nY - 1 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1] + 1, dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dYZw);
                }
                if (dimIndexes[1] < nY - 1 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0], dimIndexes[1] + 1, dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dYZw);
                }

                // add diagonal vertex neighbours (max 8)
                if (dimIndexes[0] > 0 && dimIndexes[1] > 0 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1] - 1, dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] > 0 && dimIndexes[1] > 0 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1] - 1, dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] > 0 && dimIndexes[1] < nY - 1 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1] + 1, dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] > 0 && dimIndexes[1] < nY - 1 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] - 1, dimIndexes[1] + 1, dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[1] > 0 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1] - 1, dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[1] > 0 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1] - 1, dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[1] < nY - 1 && dimIndexes[2] > 0)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1] + 1, dimIndexes[2] - 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }
                if (dimIndexes[0] < nX - 1 && dimIndexes[1] < nY - 1 && dimIndexes[2] < nZ - 1)
                {
                    neighInd.Add(GetSequentialIndex(dimIndexes[0] + 1, dimIndexes[1] + 1, dimIndexes[2] + 1, nY, nZ));
                    neighCoeff.Add(dXYZw);
                }

                // compute totalWeight
                for (int j = 0; j < neighCoeff.Count; j++)
                    totalWeight += neighCoeff[j];
                // normalize transmission coefficients
                for (int j = 0; j < neighCoeff.Count; j++)
                    neighCoeff[j] /= totalWeight;

                // add to Topology
                topology[i] = neighInd.ToArray();
                TransCoeff[i] = neighCoeff.ToArray();
            }

            return topology;
        }

        #endregion

        #region Populate methods

        /// <summary>
        /// Populate Field with scalar values - multiple values per Field point
        /// </summary>
        /// <param name="scalarValues"></param>
        /// <returns></returns>
        public bool PopulateScalars(DataTree<double> scalarValues)
        {
            if (scalarValues.BranchCount == 1) scalarValues.Graft();

            if (scalarValues.BranchCount != points.Count) return false;

            scalarValues = MathUtils.NormalizeRanges(scalarValues);

            if (Tensors[0] == null)
                for (int i = 0; i < scalarValues.BranchCount; i++)
                    Tensors[i] = new Tensor(scalarValues.Branches[i].ToArray());
            else
                for (int i = 0; i < scalarValues.BranchCount; i++)
                    Tensors[i].Scalars = scalarValues.Branches[i].ToArray();

            return true;
        }

        /// <summary>
        /// Populate Field with vector values - multiple values per Field point
        /// </summary>
        /// <param name="vectorValues"></param>
        /// <returns></returns>
        public bool PopulateVectors(DataTree<Vector3d> vectorValues)
        {
            if (vectorValues.BranchCount == 1) vectorValues.Graft();

            if (vectorValues.BranchCount != points.Count) return false;

            if (Tensors[0] == null)
                for (int i = 0; i < vectorValues.BranchCount; i++)
                    Tensors[i] = new Tensor(vectorValues.Branches[i].ToArray());
            else
                for (int i = 0; i < vectorValues.BranchCount; i++)
                    Tensors[i].Vectors = vectorValues.Branches[i].ToArray();

            return true;
        }

        /// <summary>
        /// Populate Field with weights distribution - multiple values per Field point
        /// </summary>
        /// <param name="iWeights"></param>
        /// <returns>true if operation was successful</returns>
        public bool PopulateiWeights(DataTree<int> iWeights)
        {
            if (iWeights.BranchCount == 1) iWeights.Graft();

            if (iWeights.BranchCount != points.Count) return false;

            // if Tensors aren't populated yet
            if (Tensors[0] == null)
                for (int i = 0; i < iWeights.BranchCount; i++)
                    Tensors[i] = new Tensor(iWeights.Branches[i].ToArray());
            else
            {
                for (int i = 0; i < Tensors.Length; i++)
                    Tensors[i].IWeights = iWeights.Branches[i].ToArray();
            }
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
            return (PopulateScalars(scalarValues) && PopulateVectors(vectorValues) && PopulateiWeights(iWeights));
        }

        #endregion Populate methods

        /// <summary>
        /// Computes search radius for sparse Fields
        /// </summary>
        /// <returns></returns>
        private void ComputeSearchRadiusAndMaxDist()
        {
            double radius;
            double dist;

            if (Topology == null)
            {
                radius = double.MaxValue;
                for (int i = 0; i < points.Count; i++)
                    for (int j = i + 1; j < points.Count; j++)
                    {
                        dist = points[i].DistanceTo(points[j]);
                        if (dist < radius) radius = dist;
                    }
                searchRadius = radius * 1.5;
                MaxDistSquare = radius * radius;
            }
            else
            {
                radius = 0;
                for (int i = 0; i < points.Count; i++)
                    for (int j = 0; j < Topology[i].Length; j++)
                    {
                        dist = points[i].DistanceTo(points[Topology[i][j]]);
                        if (dist > radius) radius = dist;
                    }
                searchRadius = radius * Constants.SafeScaleMultiplier;
                MaxDistSquare = radius * radius;
            }
        }

        /// <summary>
        /// Generates Colors according to scalar values at given index in the Field;
        /// threshold is used when blend is false
        /// </summary>
        /// <param name="i_colors"></param>
        /// <param name="index"></param>
        /// <param name="threshold"></param>
        /// <param name="blend"></param>
        /// <returns>True if operation is successful</returns>
        public bool GenerateScalarColors(List<Color> i_colors, int index, double threshold, bool blend)
        {
            // return if scalar values are not present or index is invalid
            if (Tensors[0] == null) return false;
            if (Tensors[0].Scalars == null) return false;
            if (Tensors[0].Scalars.Length == 0 || Tensors[0].Scalars.Length <= index) return false;

            List<double> parameters = new List<double>();
            double factor = 1.0 / (i_colors.Count - 1.0);
            for (int i = 0; i < i_colors.Count; i++)
                parameters.Add(i * factor);

            Colors = new Color[points.Count];

            if (blend)
            {
                GH_Gradient gradient = new GH_Gradient(parameters, i_colors);
                Parallel.For(0, points.Count, i =>
                {
                    Colors[i] = gradient.ColourAt(Tensors[i].Scalars[index]);
                });
            }

            else
            {
                List<Color> attColors = new List<Color> { i_colors[0], i_colors[i_colors.Count - 1] };
                DataTree<int> cWeights = new DataTree<int>();

                for (int i = 0; i < attColors.Count; i++)
                {
                    GH_Path p = new GH_Path(i);
                    cWeights.Add(attColors[i].R, p);
                    cWeights.Add(attColors[i].G, p);
                    cWeights.Add(attColors[i].B, p);
                }

                int[][] rgbValues = new int[points.Count][];
                rgbValues = AllocateIntegersScalar(cWeights, threshold, index);
                Colors = rgbValues.Select(c => Color.FromArgb(c[0], c[1], c[2])).ToArray();
            }
            return true;
        }

        ///// <summary>
        ///// Distribute iWeights according to scalar values in the Field at index ind
        ///// </summary>
        ///// <param name="weights"></param>
        ///// <param name="threshold"></param>
        ///// <param name="index"></param>
        ///// <param name="blend"></param>
        //private bool DistributeiWeightsScalar(DataTree<int> weights, double threshold, int index, bool blend)
        //{
        //    // if Tensors aren't populated yet or there are no scalar values at the index or index exceeds scalar vector length return
        //    if (Tensors[0] == null) return false;
        //    if (Tensors[0].Scalars == null) return false;
        //    if (index >= Tensors[0].Scalars.Length) return false;

        //    if (blend)
        //        return PopulateiWeights(InterpolateIntegersScalar(weights, index));

        //    else
        //        return PopulateiWeights(AllocateIntegersScalar(weights, threshold, index));
        //}

        ///// <summary>
        ///// Distribute attractor-based iWeights, with an option for blending
        ///// </summary>
        ///// <param name="weights">Data Tree of input weights\nOne branch for each attractor point</param>
        ///// <param name="attractors">List of attractor points</param>
        ///// <param name="blend">True for smooth blending, false for closest-point criteria</param>
        //private bool DistributeiWeights(DataTree<int> weights, List<Point3d> attractors, bool blend)
        //{
        //    // if Tensors aren't populated yet initialize the Tensors array with placeholder weights
        //    if (Tensors[0] == null)
        //    {
        //        int[] w = new[] { 0 };
        //        for (int i = 0; i < Tensors.Length; i++)
        //            Tensors[i] = new Tensor(w);
        //    }

        //    if (blend)
        //        return PopulateiWeights(InterpolateIntegers(weights, attractors));

        //    else
        //        return PopulateiWeights(AllocateIntegers(weights, attractors));

        //}

        ///// <summary>
        ///// Interpolates integers according to a list of attractor points
        ///// </summary>
        ///// <param name="weights">Data Tree of input weights\nOne branch for each attractor point</param>
        ///// <param name="attractors">List of attractor points</param>
        ///// <returns>Jagged array of interpolated integers</returns>
        //private int[][] InterpolateIntegers(DataTree<int> weights, List<Point3d> attractors)
        //{
        //    int[][] integerArray = new int[points.Count][];

        //    Parallel.For(0, points.Count, i =>
        //    //for(int i = 0; i < points.Count; i++)
        //    {
        //        integerArray[i] = new int[weights.Branches[0].Count];
        //        for (int j = 0; j < weights.Branches[0].Count; j++)
        //        {
        //            double dist, weight = 0, distW = 0;
        //            for (int k = 0; k < attractors.Count; k++)
        //            {
        //                dist = 1 / (0.01 + points[i].DistanceTo(attractors[k]));
        //                weight += weights.Branches[k][j] * dist;
        //                distW += dist;
        //            }
        //            weight /= distW;

        //            integerArray[i][j] = (int)Math.Round(weight);
        //        }

        //    });

        //    return integerArray;
        //}

        ///// <summary>
        ///// Interpolates integers according to the Field scalar values
        ///// </summary>
        ///// <param name="weights">Data Tree of input weights\nOtwo branches for integers corresponding to scalar values extremes</param>
        ///// <param name="index"></param>
        ///// <returns>Jagged array of interpolated integers</returns>
        //private int[][] InterpolateIntegersScalar(DataTree<int> weights, int index)
        //{
        //    int[][] integerArray = new int[points.Count][];

        //    Parallel.For(0, points.Count, i =>
        //    //for(int i = 0; i < points.Count; i++)
        //    {
        //        integerArray[i] = new int[weights.Branches[0].Count];
        //        for (int j = 0; j < weights.Branches[0].Count; j++)
        //        {
        //            double weight = weights.Branches[0][j] * (1 - Tensors[i].Scalars[index]) + weights.Branches[1][j] * Tensors[i].Scalars[index];
        //            integerArray[i][j] = (int)Math.Round(weight);
        //        }

        //    });

        //    return integerArray;
        //}

        ///// <summary>
        ///// Allocates integers (no interpolation) according to the closest corresponding attractor point
        ///// </summary>
        ///// <param name="weights">Data Tree of input weights\nOne branch for each attractor point</param>
        ///// <param name="attractors">List of attractor points</param>
        ///// <returns>Jagged array of allocated integers</returns>
        //private int[][] AllocateIntegers(DataTree<int> weights, List<Point3d> attractors)
        //{

        //    int[][] integerArray = new int[points.Count][];

        //    Parallel.For(0, points.Count, i =>
        //    //for(int i = 0; i < points.Length; i++)
        //    {

        //        double dist, minDist = double.MaxValue;
        //        int minInd = 0;

        //        for (int k = 0; k < attractors.Count; k++)
        //        {
        //            dist = points[i].DistanceToSquared(attractors[k]);
        //            if (dist < minDist)
        //            {
        //                minDist = dist;
        //                minInd = k;
        //            }
        //        }

        //        integerArray[i] = new int[weights.Branches[minInd].Count];
        //        for (int j = 0; j < weights.Branches[minInd].Count; j++)
        //        {
        //            integerArray[i][j] = weights.Branches[minInd][j];
        //        }

        //    });

        //    return integerArray;
        //}

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
                    integerArray[i][j] = Tensors[i].Scalars[index] < threshold ? weights.Branches[0][j] : weights.Branches[1][j];

            });

            return integerArray;
        }

        #region getters

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
            // collision radius is a Field of AssemblyObjects
            pointsTree.Search(new Sphere(P, searchRadius), (sender, e) =>
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
            return Tensors[points.ClosestIndex(P)].GetScalar();
        }

        /// <summary>
        /// Gets array of scalar values for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public double[] GetClosestScalars(Point3d P)
        {
            return Tensors[points.ClosestIndex(P)].Scalars;
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
            if (neighScal.Count == 0) return intScal; // this is the condition of being outside the Field - correct with another value

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
            pointsTree.Search(new Sphere(P, searchRadius), (sender, e) =>
            {
                // get the scalar neighbours values
                neighScal.Add(Tensors[e.Id].GetScalar());
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
            return Tensors[points.ClosestIndex(P)].GetVector();
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
            List<Vector3d> neighVec = new List<Vector3d>();

            pointsTree.Search(new Sphere(P, searchRadius), (sender, e) =>
            {
                // recover the AssemblyObject index related to the found centroid
                neighVec.Add(Tensors[e.Id].GetVector());
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
            return Tensors[points.ClosestIndex(P)].Vectors;
        }

        /// <summary>
        /// Gets array of integer weights for the closest Field point to the given Point P
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public int[] GetClosestiWeights(Point3d P)
        {
            return Tensors[points.ClosestIndex(P)].IWeights;
        }

        /// <summary>
        /// Gets the first scalar value for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public double GetScalar(int i)
        {
            if (Tensors.Length > 0 && Tensors[i] != null && Tensors[i].Scalars != null)
                return Tensors[i].GetScalar();
            else return double.NaN;
        }

        /// <summary>
        /// Gets the array of scalar values for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public double[] GetScalars(int i)
        {
            if (Tensors.Length > 0 && Tensors[i] != null && Tensors[i].Scalars != null)
                return Tensors[i].Scalars;
            else return null;
        }

        /// <summary>
        /// Gets the first vector value for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Vector3d GetVector(int i)
        {
            if (Tensors.Length > 0 && Tensors[i] != null && Tensors[i].Vectors != null)
                return Tensors[i].GetVector();
            else return Vector3d.Zero;
        }

        /// <summary>
        /// Gets the array of vector values for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public Vector3d[] GetVectors(int i)
        {
            if (Tensors.Length > 0 && Tensors[i] != null && Tensors[i].Vectors != null)
                return Tensors[i].Vectors;
            else return null;
        }

        /// <summary>
        /// Gets the array of integer weights for the Field point at index i
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public int[] GetiWeights(int i)
        {
            if (Tensors.Length > 0 && Tensors[i] != null && Tensors[i].IWeights != null)
                return Tensors[i].IWeights;
            else return null;
        }

        /// <summary>
        /// Gets all Field Points
        /// </summary>
        /// <returns></returns>
        public Point3d[] GetPoints()
        {
            return points.ToArray();
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

            if (Tensors.Length > 0 && Tensors[0] != null && Tensors[0].Scalars != null)
                for (int j = 0; j < Tensors.Length; j++)
                    scalars.AddRange(Tensors[j].Scalars, new GH_Path(j));

            return scalars;
        }

        /// <summary>
        /// Gets all Field scalar values as DataTree in GH_Number format (for fast output)
        /// </summary>
        /// <returns></returns>
        public DataTree<GH_Number> GetGH_Scalars()
        {
            DataTree<GH_Number> scalars = new DataTree<GH_Number>();

            if (Tensors.Length > 0 && Tensors[0] != null && Tensors[0].Scalars != null)
                for (int j = 0; j < Tensors.Length; j++)
                    scalars.AddRange(Tensors[j].Scalars.Select(s => new GH_Number(s)).ToList(), new GH_Path(j));

            return scalars;
        }

        /// <summary>
        /// Gets all Field vector values as DataTree
        /// </summary>
        /// <returns></returns>
        public DataTree<Vector3d> GetVectors()
        {
            DataTree<Vector3d> vectors = new DataTree<Vector3d>();

            if (Tensors.Length > 0 && Tensors[0] != null && Tensors[0].Vectors != null)
                for (int j = 0; j < Tensors.Length; j++)
                    vectors.AddRange(Tensors[j].Vectors, new GH_Path(j));

            return vectors;
        }

        /// <summary>
        /// Gets all Field vector values as DataTree in GH_Vector format (for fast output)
        /// </summary>
        /// <returns></returns>
        public DataTree<GH_Vector> GetGH_Vectors()
        {
            DataTree<GH_Vector> vectors = new DataTree<GH_Vector>();

            if (Tensors.Length > 0 && Tensors[0] != null && Tensors[0].Vectors != null)
                for (int j = 0; j < Tensors.Length; j++)
                    vectors.AddRange(Tensors[j].Vectors.Select(v => new GH_Vector(v)).ToList(), new GH_Path(j));

            return vectors;
        }

        /// <summary>
        /// Gets all Field integer Weights as DataTree
        /// </summary>
        /// <returns></returns>
        public DataTree<int> GetiWeights()
        {
            DataTree<int> data = new DataTree<int>();

            if (Tensors.Length > 0 && Tensors[0] != null && Tensors[0].IWeights != null)
                for (int j = 0; j < Tensors.Length; j++)
                    data.AddRange(Tensors[j].IWeights, new GH_Path(j));

            return data;
        }

        /// <summary>
        /// Gets all Field integer Weights as DataTree in GH_Integer format (for fast output)
        /// </summary>
        /// <returns></returns>
        public DataTree<GH_Integer> GetGH_iWeights()
        {
            DataTree<GH_Integer> data = new DataTree<GH_Integer>();

            if (Tensors.Length > 0 && Tensors[0] != null && Tensors[0].IWeights != null)
                for (int j = 0; j < Tensors.Length; j++)
                    data.AddRange(Tensors[j].IWeights.Select(i => new GH_Integer(i)).ToList(), new GH_Path(j));

            return data;
        }
        #endregion getters


        private int GetSequentialIndex(int i, int j, int k, int nY, int nZ) => i * (nY * nZ) + j * nZ + k;

        private int[] GetDimensionalIndices(int index, int nY, int nZ)
        {
            int[] dimIndices = new int[3];

            dimIndices[0] = index / (nY * nZ);
            dimIndices[1] = (index % (nY * nZ)) / nZ;
            dimIndices[2] = index % (nZ);

            return dimIndices;
        }

        #region Stigmergy methods
        public void InitStigmergy()
        {
            StigValues = new double[points.Count];

            for (int i = 0; i < StigValues.Length; i++) StigValues[i] = 0;
        }

        public void UpdateStigmergy(double addCoeff, double lossCoeff, double evapRate)
        {
            double[] stigAdd = new double[StigValues.Length];
            double[] stigLoss = new double[StigValues.Length];

            // instead of calculating an outgoing value (writing to neighbours)
            // an incoming contribution from neighbours is calculated (reading from neighbours)
            // as well as the loss of value to the neighbours
            // hence making parallel computation possible
            Parallel.For(0, StigValues.Length, i =>
            {
                for (int j = 0; j < Topology[i].Length; j++)
                {
                    stigAdd[i] += StigValues[Topology[i][j]] * TransCoeff[i][j];
                    stigLoss[i] += StigValues[i] * TransCoeff[i][j];
                }
            });

            // still, calculation must be split in 2 parts: calculating modification and value update
            Parallel.For(0, StigValues.Length, i =>
            {
                StigValues[i] = (StigValues[i] + stigAdd[i] * addCoeff - stigLoss[i] * lossCoeff) * evapRate;
                StigValues[i] = Math.Min(StigValues[i], 1);
            });

        }

        #endregion Stigmergy methods

        public override string ToString()
        {
            return string.Format("Assembler Field containing {0} points", points.Count);
        }
    }
}
