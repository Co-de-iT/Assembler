using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssemblerLib.Graph
{
    /// <summary>
    /// 
    /// </summary>
    class Node
    {
        /// <summary>
        /// 
        /// </summary>
        public Point3d location;
        /// <summary>
        /// 
        /// </summary>
        public List<Connection> connections;
        /// <summary>
        /// 
        /// </summary>
        public List<Node> neighbours;
        /// <summary>
        /// 
        /// </summary>
        public int index;
        /// <summary>
        /// 
        /// </summary>
        public int iWeight;
        /// <summary>
        /// 
        /// </summary>
        public double weight;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="location"></param>
        /// <param name="index"></param>
        /// <param name="connections"></param>
        /// <param name="neighbours"></param>
        /// <param name="weight"></param>
        /// <param name="iWeight"></param>
        public Node(Point3d location, int index, List<Connection> connections, List<Node> neighbours, double weight, int iWeight)
        {
            this.location = location;
            this.connections = connections;
            this.neighbours = neighbours;
            this.index = index;
            this.iWeight = iWeight;
            this.weight = weight;
        }

        /// <summary>
        /// Empty constructor
        /// </summary>
        public Node()
        { 
        
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        public Node(int index)
        {
            this.index = index;
            connections = new List<Connection>();
            neighbours = new List<Node>();
        }

        public Node(Point3d location, int index, double weight, int iWeight)
        {
            this.location = location;
            this.index = index;
            this.weight = weight;
            this.iWeight = iWeight;
            connections = new List<Connection>();
            neighbours = new List<Node>();
        }
    }
}
