using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace AssemblerLib.Graph
{
    /// <summary>
    /// a Graph class to manage topological graphs
    /// </summary>
    class Graph
    {
        /// <summary>
        /// 
        /// </summary>
        public List<Node> Nodes { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List<Connection> Connections { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Nodes"></param>
        /// <param name="Connections"></param>
        public Graph(List<Node> Nodes, List<Connection> Connections)
        {
            this.Nodes = Nodes;
            this.Connections = Connections;
        }

        /// <summary>
        /// Empty constructor
        /// </summary>
        public Graph()
        {
            Nodes = new List<Node>();
            Connections = new List<Connection>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="topology"></param>
        public Graph(int[][] topology)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="AOa"></param>
        /// <remarks>INCOMPLETE - solve the indexing-AInd problem</remarks>
        public Graph(Assemblage AOa)
        {
            Nodes = new List<Node>();
            Connections = new List<Connection>();
            // populate the data arrays
            List<Point3d> locations = new List<Point3d>();
            int[][] topology = new int[AOa.AssemblyObjects.BranchCount][];
            double[] weights = new double[AOa.AssemblyObjects.BranchCount];
            int[] iWeights = new int[AOa.AssemblyObjects.BranchCount];
            List<int> neighbours;
            // create nodes
            for (int i = 0; i > AOa.AssemblyObjects.BranchCount; i++)
            {
                // make node and add it to the list
                AssemblyObject AO = AOa.AssemblyObjects[new Grasshopper.Kernel.Data.GH_Path(i), 0];
                Nodes.Add(new Node(AO.ReferencePlane.Origin, AO.AInd, AO.Weight, AO.IWeight));

                neighbours = new List<int>();
                // populate topology array
                for (int j = 0; j < AO.Handles.Length; j++)
                {
                    if (AO.Handles[j].Occupancy == 0) continue;
                    if (AO.Handles[j].Occupancy == 1)
                    {
                        neighbours.Add(AO.Handles[j].NeighbourObject); // these are Ainds
                    }
                }
                topology[i] = neighbours.ToArray();
            }

        }

        void GenerateGraph(List<Point3d> locations, int[][] topology, double[] weights, int[] iWeights)
        {
            // create nodes
            for (int i = 0; i < topology.Length; i++)
                Nodes.Add(new Node(locations[i], i, weights[i], iWeights[i]));

            int connCount = 0;
            int otherNode;
            // determine connections and neighbours
            for (int i = 0; i < topology.Length; i++)
                for (int j = 0; j < topology[i].Length; j++)
                {
                    otherNode = topology[i][j];
                    // if a connection was already created (undirected graph) do nothing
                    if (otherNode < i && topology[otherNode].Contains(i)) continue;

                    // otherwise create connection and update data
                    Connection newConn = new Connection(Nodes[i], Nodes[j], connCount);
                    // update nodes and connection
                    Nodes[i].connections.Add(newConn);
                    Nodes[j].connections.Add(newConn);
                    Nodes[i].neighbours.Add(Nodes[j]);
                    Nodes[j].neighbours.Add(Nodes[i]);
                    newConn.ComputeWeights();
                    Connections.Add(newConn);
                    connCount++;
                }

        }
    }
}
