using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblerLib
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

        public Graph(int[][] topology)
        {

        }

        void GenerateGraph(List<Point3d> locations, int[][] topology, double[] weights, int[] iWeights)
        {
            // create nodes
            for (int i = 0; i < topology.Length; i++)
                Nodes.Add(new Node(locations[i],i,weights[i], iWeights[i]));

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
                        newConn.CalculateWeights();
                        Connections.Add(newConn);
                        connCount++;
                }

        }
    }
}
