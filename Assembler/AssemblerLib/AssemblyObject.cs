using Newtonsoft.Json;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib
{
    /// <summary>
    /// Assemblage Object with extended connectivity and collision-check properties
    /// </summary>
    public class AssemblyObject
    {
        #region fields
        /// <summary>
        /// List of optional children objects (for composite object)
        /// </summary>
        public List<AssemblyObject> children;
        /// <summary>
        /// A Mesh for collision detections
        /// </summary>
        public Mesh collisionMesh;
        /// <summary>
        /// A Mesh for avoiding false positives in collision detection (overlaps)
        /// </summary>
        public Mesh offsetMesh;
        /// <summary>
        /// Reference Plane for the AssemblyObject
        /// </summary>
        public Plane referencePlane;
        /// <summary>
        /// Handles for connectivity and assemblage operations
        /// </summary>
        public Handle[] handles;
        /// <summary>
        /// Direction Vector - for vector field generation/interaction
        /// </summary>
        public Vector3d direction;
        /// <summary>
        /// Supports
        /// </summary>
        public List<Support> supports;
        /// <summary>
        /// n. of minimum connected Supports to consider the object supported
        /// </summary>
        public int minSupports;
        /// <summary>
        /// True if minimum amount of required supports are connected
        /// </summary>
        public bool supported;
        /// <summary>
        /// AssemblyObject id - identifies a unique object type
        /// </summary>
        public int type;
        /// <summary>
        /// AssemblyObject name
        /// </summary>
        public string name;
        /// <summary>
        /// AssemblyObject unique index in an assemblage
        /// </summary>
        public int AInd
        { get { return aInd; } set { aInd = value; } }
        private int aInd;
        /// <summary>
        /// List of tuples containing object index and handle index occluded by this object
        /// </summary>
        public List<int[]> occludedNeighbours;
        /// <summary>
        /// Collision radius for collision checks with neighbour AssemblyObjects
        /// </summary>
        public double collisionRadius;
        /// <summary>
        /// Scalar value for density evaluation or scalar field generation/interaction
        /// </summary>
        public double weight;
        /// <summary>
        /// Integer Weight for Weighted Random choice (to be implemented) or other weight-based operations
        /// </summary>
        public int iWeight;
        /// <summary>
        /// True to force the object's reference plane Z axys parallel to the World's Z axis
        /// </summary>
        public bool absoluteZLock;

        /// <summary>
        /// internal map of handles for composite object
        /// </summary>
        internal List<int[]> handleMap;

        #endregion

        #region constructors
        /// <summary>
        /// empty constructor
        /// </summary>
        public AssemblyObject()
        {
            // no parameter is defined, this constructor builds an empty AssemblyObject
            collisionMesh = new Mesh();
            offsetMesh = new Mesh();
            referencePlane = Plane.Unset;
            direction = Vector3d.Zero;
            supports = new List<Support>();
        }

        /// <summary>
        /// Constructor used for JSON deserialization
        /// This constructor DOES NOT execute a deep copy of data
        /// If you need to duplicate an AssemblyObject use the Utilities.Clone method
        /// or Utilities.CloneWithConnectivity method
        /// </summary>
        /// <param name="collisionMesh"></param>
        /// <param name="offsetMesh"></param>
        /// <param name="handles"></param>
        /// <param name="referencePlane"></param>
        /// <param name="direction"></param>
        /// <param name="aInd"></param>
        /// <param name="occludedNeighbours"></param>
        /// <param name="collisionRadius"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="weight"></param>
        /// <param name="iWeight"></param>
        /// <param name="supports"></param>
        /// <param name="minSupports"></param>
        /// <param name="supported"></param>
        /// <param name="absoluteZLock"></param>
        /// <param name="children"></param>
        /// <param name="handleMap"></param>
        [JsonConstructor]
        public AssemblyObject(Mesh collisionMesh, Mesh offsetMesh, Handle[] handles, Plane referencePlane, Vector3d direction, int aInd, List<int[]> occludedNeighbours,
            double collisionRadius, string name, int type, double weight, int iWeight, List<Support> supports, int minSupports,
            bool supported, bool absoluteZLock, List<AssemblyObject> children, List<int[]> handleMap)
        {
            this.collisionMesh = collisionMesh;
            this.offsetMesh = offsetMesh;
            this.referencePlane = referencePlane;
            this.direction = direction;
            this.handles = handles;
            this.aInd = aInd;
            this.occludedNeighbours = occludedNeighbours;
            this.collisionRadius = collisionRadius;
            this.name = name;
            this.type = type;
            this.weight = weight;
            this.iWeight = iWeight;
            this.supports = supports;
            this.minSupports = minSupports;
            this.supported = supported;
            this.absoluteZLock = absoluteZLock;
            this.children = children;
            this.handleMap = handleMap;
        }

        /// <summary>
        /// Builds an AssemblyObject from all required parameters
        /// </summary>
        /// <param name="collisionMesh">Mesh for collision calculation</param>
        /// <param name="handles">handles for assemblage</param>
        /// <param name="referencePlane">reference Plane</param>
        /// <param name="direction">direction for Vector field interaction</param>
        /// <param name="name">object type name</param>
        /// <param name="type">object type id</param>
        /// <param name="weight">scalar for density operations or field interactions</param>
        /// <param name="iWeight">integer Weight for Weighted Random choice</param>
        /// <param name="supports">list of supports</param>
        /// <param name="minSupports">minimum n. of supports required</param>
        /// <param name="absoluteZLock">lock orientation of Z axis to World Z axis</param>
        public AssemblyObject(Mesh collisionMesh, Handle[] handles, Plane referencePlane, Vector3d direction, string name, int type, double weight, int iWeight,
            List<Support> supports, int minSupports, bool absoluteZLock)
        {
            // collision Mesh operations
            this.collisionMesh = new Mesh();
            this.collisionMesh.CopyFrom(collisionMesh);
            this.collisionMesh.Vertices.Align(Utilities.RhinoAbsoluteTolerance);
            this.collisionMesh.Weld(Math.PI);
            this.collisionMesh.RebuildNormals();
            // generate offset Mesh
            double offsetTol = Utilities.RhinoAbsoluteTolerance * 2.5;
            offsetMesh = Utilities.MeshOffsetWeightedAngle(this.collisionMesh, offsetTol); // do not use standard Mesh offset

            // orientation data
            this.referencePlane = referencePlane;
            this.direction = direction;

            // connectivity data
            this.handles = handles.Select(h => Utilities.Clone(ref h)).ToArray();
            //this.handles = handles.Select(h => new Handle(h)).ToArray(); // do NOT use this formula
            occludedNeighbours = new List<int[]>();
            collisionRadius = this.collisionMesh.GetBoundingBox(false).Diagonal.Length * 2.5;

            // ID and other non-geometry data
            this.name = name;
            this.type = type;
            this.weight = weight;
            this.iWeight = iWeight;

            // supports operations
            this.supports = supports.Select(s => new Support(s)).ToList();
            this.minSupports = minSupports;
            supported = false;

            // Z Lock
            this.absoluteZLock = absoluteZLock;

            // children and handlemap initialization
            children = new List<AssemblyObject>();
            handleMap = new List<int[]>();
        }

        #endregion

        #region composite constructors

        /// <summary>
        /// Builds a composite AssemblyObject from a list of children and a custom collisionMesh
        /// </summary>
        /// <param name="collisionMesh"></param>
        /// <param name="referencePlane"></param>
        /// <param name="direction"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="weight"></param>
        /// <param name="absoluteZLock"></param>
        /// <param name="children"></param>
        public AssemblyObject(Mesh collisionMesh, Plane referencePlane, Vector3d direction, string name, int type, double weight, bool absoluteZLock, List<AssemblyObject> children) :
            this(collisionMesh, new Handle[] { }, referencePlane, direction, name, type, weight, 1, new List<Support>(), 0, absoluteZLock)
        {
            // copy children objects
            this.children = children.Select(ao => Utilities.Clone(ao)).ToList();

            // generate internal obstruction and connectivity status
            Utilities.ObstructionCheckList(this.children);

            // set handles and correspondance array
            handleMap = new List<int[]>();
            MapHandles();
        }

        /// <summary>
        /// Builds a composite AssemblyObject from a list of children, a custom collisionMesh, and a custom set of Handles
        /// </summary>
        /// <param name="collisionMesh"></param>
        /// <param name="handles"></param>
        /// <param name="referencePlane"></param>
        /// <param name="direction"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="weight"></param>
        /// <param name="absoluteZLock"></param>
        /// <param name="children"></param>
        public AssemblyObject(Mesh collisionMesh, Handle[] handles, Plane referencePlane, Vector3d direction, string name, int type, double weight, bool absoluteZLock, List<AssemblyObject> children) :
            this(collisionMesh, handles, referencePlane, direction, name, type, weight, 1, new List<Support>(), 0, absoluteZLock)
        {
            // copy children objects
            this.children = children.Select(ao => Utilities.Clone(ao)).ToList();

            handleMap = new List<int[]>();
        }

        #endregion

        #region methods

        private void MapHandles()
        {
            List<Handle> compositeHandles = new List<Handle>();

            // generate composite object handles and handlemap
            for (int i = 0; i < children.Count; i++)
                for (int j = 0; j < children[i].handles.Length; j++)
                    if (children[i].handles[j].occupancy == 0)
                    {
                        compositeHandles.Add(Utilities.Clone(ref children[i].handles[j]));
                        //compositeHandles.Add(new Handle(children[i].handles[j]));
                        handleMap.Add(new int[] { i, j });
                    }
            handles = compositeHandles.ToArray();
        }

        /// <summary>
        /// Transform AssemblyObject using a generic Transformation
        /// </summary>
        /// <param name="xForm">The Transformation to apply</param>
        public void Transform(Transform xForm)
        {

            // transform geometries
            collisionMesh.Transform(xForm);
            offsetMesh.Transform(xForm);
            referencePlane.Transform(xForm);
            direction.Transform(xForm);

            // transform Handles (do not use a foreach loop)
            for (int i = 0; i < handles.Length; i++) handles[i].Transform(xForm);

            // transform children (if any)
            if (children != null)
                for (int i = 0; i < children.Count; i++) children[i].Transform(xForm);

            // transform Supports (if they exist)
            if (supports != null)
                for (int i = 0; i < supports.Count; i++) supports[i].Transform(xForm);
        }

        // MOVE TO UTILITIES
        //newObject.UpdateHandle(rule.sH, 1, rInd, rule.rH, assemblage[rInd].handles[rule.rH].weight)
        public void UpdateHandle(int handleIndex, int occupancy, int neighbourObject, int neighbourHandle, double weight)
        {
            handles[handleIndex].occupancy = occupancy;
            handles[handleIndex].neighbourObject = neighbourObject;
            handles[handleIndex].neighbourHandle = neighbourHandle;
            handles[handleIndex].weight += weight;
        }

        #endregion
    }
}
