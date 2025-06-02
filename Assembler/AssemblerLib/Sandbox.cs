using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblerLib
{
    /// <summary>
    /// EXPERIMENTAL - NOT IMPLEMENTED YET
    /// </summary>
    internal struct Sandbox
    {

        /// <summary>
        /// Sandbox for focused assemblage growth - EXPERIMENTAL - NOT IMPLEMENTED YET
        /// </summary>
        internal Box E_sandbox;
        internal RTree E_sandboxCentroidsTree;
        internal List<int> E_sandboxCentroidsAO; // list of sandbox centroid/AssemblyObject correspondances
        internal List<int> E_sandboxAvailableObjects;
        internal List<int> E_sandboxUnreachableObjects;

        internal void Reset()
        {
            E_sandbox = Box.Unset;
            E_sandboxCentroidsTree = new RTree();
            E_sandboxCentroidsAO = new List<int>(); // list of sandbox centroid/AssemblyObject correspondances
            E_sandboxAvailableObjects = new List<int>();
            E_sandboxUnreachableObjects = new List<int>();
        }
    }
}
