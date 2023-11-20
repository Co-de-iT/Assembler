using System.Collections.Generic;

namespace AssemblerLib.Graph
{
    /// <summary>
    /// Connection class as part of a Graph
    /// </summary>
    class Connection
    {
        public Node from;
        public Node to;
        public int index;
        public bool bidirectional;
        public double weight;
        public int iWeight;

        public Connection(Node from, Node to, int index, bool bidirectional, double weight, int iWeight)
        {
            this.from = from;
            this.to = to;
            this.index = index;
            this.bidirectional = bidirectional;
            this.weight = weight;
            this.iWeight = iWeight;
        }

        public Connection(Node from, Node to, int index) 
        {
            this.from = from;
            this.to = to;
            this.index = index;
            bidirectional = true;
            ComputeWeights();
        }

        public void ComputeWeights()
        {
            weight = 0.5 * (from.weight + to.weight);
            iWeight = (int)0.5 * (from.iWeight + to.iWeight);
        
        }

        public override bool Equals(object obj)
        {
            return obj is Connection connection &&
                   ((EqualityComparer<Node>.Default.Equals(from, connection.from) &&
                   EqualityComparer<Node>.Default.Equals(to, connection.to)) ||
                   (EqualityComparer<Node>.Default.Equals(from, connection.to) &&
                   EqualityComparer<Node>.Default.Equals(to, connection.from)));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
