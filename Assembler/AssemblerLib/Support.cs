using Rhino.Geometry;

namespace AssemblerLib
{
    // NOT IMPLEMENTED YET

    /// <summary>
    /// Support class for embedded structural consistency - NOT IMPLEMENTED YET
    /// </summary>
    /// <exclude>Exclude from documentation</exclude>
    public struct Support
    {
        /// <summary>
        /// Support line
        /// </summary>
        public Line Line;

        /// <summary>
        /// initial length of the support
        /// </summary>
        public double InitLength;

        /// <summary>
        /// connected flag - true if support intersects a nearby geometry
        /// </summary>
        public bool Connected
            { get { return NeighbourObject != -1; } }

        /// <summary>
        /// Index of neighbour AssemblyObject connected by the support
        /// -1 if free, -2 if connected to an <see cref="MeshEnvironment"/> obstacle
        /// </summary>
        public int NeighbourObject;

        /// <summary>
        /// Construct a support from another Support (Clone with connectivity)
        /// </summary>
        /// <param name="other"></param>
        public Support(Support other)
        {
            Line = other.Line;
            InitLength = other.InitLength;
            //connected = other.connected;
            NeighbourObject = other.NeighbourObject;
        }

        /// <summary>
        /// Construct a support from origin point, Direction vector and length
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="length"></param>
        public Support(Point3d origin, Vector3d direction, double length)
        {
            Line = new Line(origin, direction, length);
            InitLength = length;
            //connected = false;
            NeighbourObject = -1;
        }

        /// <summary>
        /// Construct a support from a line
        /// </summary>
        /// <param name="line"></param>
        public Support(Line line)
        {
            this.Line = line;
            InitLength = line.Length;
            //connected = false;
            NeighbourObject = -1;
        }

        /// <summary>
        /// Resets support to initial length value and resets connectivity data
        /// </summary>
        public void Reset()
        {
            if (Line.Length != InitLength)
                Line = new Line(Line.From, Line.From + (Line.UnitTangent * InitLength));
            //connected = false;
            NeighbourObject = -1;
        }

        /// <summary>
        /// Transform Support using a generic Transformation
        /// </summary>
        /// <param name="xForm"></param>
        public void Transform(Transform xForm)
        {
            Line.Transform(xForm);
        }
    }
}
