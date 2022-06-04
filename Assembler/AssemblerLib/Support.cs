using Rhino.Geometry;

namespace AssemblerLib
{
    // NOT IMPLEMENTED YET

    /// <summary>
    /// Support class for embedded structural consistency - NOT IMPLEMENTED YET
    /// </summary>
    public struct Support
    {
        /// <summary>
        /// Support line
        /// </summary>
        public Line line;

        /// <summary>
        /// initial length of the support
        /// </summary>
        public readonly double initLength;

        /// <summary>
        /// connected flag - true if support intersects a nearby geometry
        /// </summary>
        public bool Connected
            { get { return neighbourObject != -1; } }

        /// <summary>
        /// Index of neighbour AssemblyObject connected by the support
        /// -1 if free, -2 if connected to an <see cref="MeshEnvironment"/> obstacle
        /// </summary>
        public int neighbourObject;

        /// <summary>
        /// Construct a support from another Support (Clone with connectivity)
        /// </summary>
        /// <param name="other"></param>
        public Support(Support other)
        {
            line = other.line;
            initLength = other.initLength;
            //connected = other.connected;
            neighbourObject = other.neighbourObject;
        }

        /// <summary>
        /// Construct a support from origin point, direction vector and length
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="length"></param>
        public Support(Point3d origin, Vector3d direction, double length)
        {
            line = new Line(origin, direction, length);
            initLength = length;
            //connected = false;
            neighbourObject = -1;
        }

        /// <summary>
        /// Construct a support from a line
        /// </summary>
        /// <param name="line"></param>
        public Support(Line line)
        {
            this.line = line;
            initLength = line.Length;
            //connected = false;
            neighbourObject = -1;
        }

        /// <summary>
        /// Resets support to initial length value and resets connectivity data
        /// </summary>
        public void Reset()
        {
            if (line.Length != initLength)
                line.To = line.From + (line.UnitTangent * initLength);
            //connected = false;
            neighbourObject = -1;
        }

        /// <summary>
        /// Transform Support using a generic Transformation
        /// </summary>
        /// <param name="xForm"></param>
        public void Transform(Transform xForm)
        {
            line.Transform(xForm);
        }
    }
}
