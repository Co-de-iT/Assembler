using AssemblerLib.Utils;
using System.Collections.Generic;


namespace AssemblerLib
{
    /// <summary>
    /// A structure that manages settings for Heuristics (rules, criteria)
    /// </summary>
    public struct HeuristicsSettings
    {
        /// <summary>
        /// List of Heuristics Sets - each item in the list is a set of rules in text format, to be interpreted by the <see cref="Assemblage"/> class
        /// </summary>
        public readonly List<string> HeuSetsString;
        /// <summary>
        /// Index of the current heuristic set used during an Assemblage
        /// </summary>
        public int CurrentHeuristics;
        /// <summary>
        /// Sets the Mode in which the Heuristic Set is selected during an Assemblage
        /// </summary>
        public HeuristicModes HeuristicsMode;
        /// <summary>
        /// Receiver selection mode. Presets:
        /// <list type="bullet">
        /// <item><description>0 - Random</description></item>
        /// <item><description>1 - Scalar Field nearest</description></item>
        /// <item><description>2 - Scalar Field interpolated</description></item>
        /// <item><description>3 - Dense packing - minimum sum of weights around candidate</description></item>
        /// <item><description>-1 - Custom - requires scripting a custom method or using the iterative engine</description></item>
        /// </list>
        /// </summary>
        public int ReceiverSelectionMode;
        /// <summary>
        /// Sender, aka <see cref="Rule"/>, selection mode. Presets:
        /// <list type="bullet">
        /// <item><description>0 - Random</description></item>
        /// <item><description>1 - Scalar Field nearest</description></item>
        /// <item><description>2 - Scalar Field interpolated</description></item>
        /// <item><description>3 - Vector Field nearest (monodirectional)</description></item>
        /// <item><description>4 - Vector Field interpolated (monodirectional)</description></item>
        /// <item><description>5 - Vector Field nearest (bidirectional)</description></item>
        /// <item><description>6 - Vector Field interpolated (bidirectional)</description></item>
        /// <item><description>7 - Minimum sender + receiver AABB (Axis-Aligned Bounding Box) volume</description></item>
        /// <item><description>8 - Minimum sender + receiver AABB (Axis-Aligned Bounding Box) diagonal</description></item>
        /// <item><description>9 - Weighted random choice</description></item>
        /// <item></item>
        /// <item><description>-1 - Custom - requires scripting a custom method or using the iterative engine</description></item>
        /// </list>
        /// </summary>
        public int SenderSelectionMode;
        /// <summary>
        /// Checks whether the Heuristics Settings require a <see cref="Field"/>
        /// </summary>
        public bool IsFieldDependent
        {
            get
            {
                return (HeuristicsMode == HeuristicModes.Field ||
                    (ReceiverSelectionMode > 0 && ReceiverSelectionMode < 3) ||
                    (SenderSelectionMode > 0 && SenderSelectionMode < 7));
            }
        }
        /// <summary>
        /// Delegate variable for custom computing sender value method
        /// </summary>
        public ComputeCandidatesValuesMethod<double> computeSendersValues;
        /// <summary>
        /// Delegate variable for custom computing receiver value method
        /// </summary>
        public ComputeReceiverMethod<double> computeReceiverValue;
        /// <summary>
        /// Delegate variable for custom sender selection method
        /// </summary>
        public SelectWinnerMethod<double> selectSender;
        /// <summary>
        /// Delegate variable for custom receiver selection method
        /// </summary>
        public SelectWinnerMethod<double> selectReceiver;

        /// <summary>
        /// HeuristicsSettings constructor for standard cases (using presets)
        /// </summary>
        /// <param name="HeuSetsString">Heuristics Set as a list of strings - each string contains all the Rules for that set, comma separated in a single string</param>
        /// <param name="CurrentHeuristics">index of current heuristics to use</param>
        /// <param name="HeuristicsMode">The <see cref="HeuristicModes"/> to use</param>
        /// <param name="ReceiverSelectionMode">The Receiver selection mode</param>
        /// <param name="SenderSelectionMode">The Sender (aka Rule) selection mode</param>
        public HeuristicsSettings(List<string> HeuSetsString, int CurrentHeuristics, int HeuristicsMode, int ReceiverSelectionMode, int SenderSelectionMode) : this(HeuSetsString, CurrentHeuristics, HeuristicsMode, ReceiverSelectionMode, SenderSelectionMode, null, null, null, null)
        {
        }
        /// <summary>
        /// HeuristicsSettings full constructor - for custom implementations
        /// </summary>
        /// <param name="HeuSetsString">Heuristics Set as a list of strings - each string contains all the Rules for that set, comma separated in a single string</param>
        /// <param name="CurrentHeuristics">index of current heuristics to use</param>
        /// <param name="HeuristicsMode">The <see cref="HeuristicModes"/> to use</param>
        /// <param name="ReceiverSelectionMode">The Receiver selection mode</param>
        /// <param name="SenderSelectionMode">The Sender (aka Rule) selection mode</param>
        /// <param name="customComputeReceiverValue">The custom delegate method for computing Receiver value</param>
        /// <param name="customComputeSendersValues">The custom delegate method for computing Sender candidates values</param>
        /// <param name="customSelectSender">The custom delegate method for selecting a Receiver from values</param>
        /// <param name="customSelectReceiver">The custom delegate method for selecting a Sender from values</param>
        public HeuristicsSettings(List<string> HeuSetsString, int CurrentHeuristics, int HeuristicsMode, int ReceiverSelectionMode, int SenderSelectionMode, ComputeReceiverMethod<double> customComputeReceiverValue, ComputeCandidatesValuesMethod<double> customComputeSendersValues, SelectWinnerMethod<double> customSelectSender, SelectWinnerMethod<double> customSelectReceiver)
        {
            this.HeuSetsString = HeuSetsString;
            this.CurrentHeuristics = CurrentHeuristics % HeuSetsString.Count; // this makes the index coherent from the get go
            this.HeuristicsMode = (HeuristicModes)HeuristicsMode;
            this.ReceiverSelectionMode = ReceiverSelectionMode;
            this.SenderSelectionMode = SenderSelectionMode;

            //computeSendersValues = customComputeSendersValues;
            //computeReceiverValue = customComputeReceiverValue;
            //selectSender = customSelectSender;
            //selectReceiver = customSelectReceiver;

            // set receiver compute and selection delegates
            switch (ReceiverSelectionMode)
            {
                // TODO: maybe custom delegates are redundant - assing them as null (they are assigned at Setup or not at all for iterative strategies)
                case -1:
                    // custom mode - methods assigned in scripted component or iterative mode
                    computeReceiverValue = customComputeReceiverValue;
                    selectReceiver = customSelectReceiver;
                    break;
                case 0:
                    // random selection among available objects
                    computeReceiverValue = ComputingRSMethods.ComputeZero;
                    selectReceiver = ComputingRSMethods.SelectRandomIndex;
                    break;
                case 1:
                    // scalar Field search - closest Field point
                    computeReceiverValue = ComputingRSMethods.ComputeScalarField;
                    selectReceiver = ComputingRSMethods.SelectMinIndex;
                    break;
                case 2:
                    // scalar Field search - interpolated values
                    computeReceiverValue = ComputingRSMethods.ComputeScalarFieldInterpolated;
                    selectReceiver = ComputingRSMethods.SelectMinIndex;
                    break;
                case 3:
                    // maximum sum weight around candidate
                    computeReceiverValue = ComputingRSMethods.ComputeWeightDensity;
                    selectReceiver = ComputingRSMethods.SelectMaxIndex;
                    break;

                // add more criteria here (must return an avInd)
                // density driven
                // component weight driven
                // ....

                //case 99:
                //    // "sequential" mode - return last available object in the list
                //    computeReceiverValue = (a, ao) => 0; // anonymous function that always returns 0
                //    anonymous function that returns AInd of last available object
                //    BUG: this won't work without a reference to the Assemblage
                //    selectReceiver = (a) => { return availableObjectsAInds.Count - 1; }; 
                //    break;

                default: goto case 0;
            }

            // set sender candidates (rules) compute and selection delegates
            switch (SenderSelectionMode)
            {
                case -1:
                    // custom mode - methods assigned in scripted component or iterative mode
                    computeSendersValues = customComputeSendersValues;
                    selectSender = customSelectSender;
                    break;
                case 0:
                    // random selection - chooses one candidate at random
                    computeSendersValues = ComputingRSMethods.ComputeZeroMany;
                    selectSender = ComputingRSMethods.SelectRandomIndex;
                    break;
                case 1:
                    // scalar Field nearest with threshold - chooses candidate whose centroid closest scalar Field value is closer to the threshold
                    computeSendersValues = ComputingRSMethods.ComputeScalarFieldMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 2:
                    // scalar Field interpolated with threshold - chooses candidate whose centroid interpolated scalar Field value is closer to the threshold
                    computeSendersValues = ComputingRSMethods.ComputeScalarFieldInterpolatedMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 3:
                    // vector Field nearest - chooses candidate whose Direction has minimum angle with closest vector Field point
                    computeSendersValues = ComputingRSMethods.ComputeVectorFieldMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 4:
                    // vector Field interpolated - chooses candidate whose Direction has minimum angle with interpolated vector Field point
                    computeSendersValues = ComputingRSMethods.ComputeVectorFieldInterpolatedMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 5:
                    // vector Field bidirectional nearest - chooses candidate whose Direction has minimum angle with closest vector Field point (bidirectional)
                    computeSendersValues = ComputingRSMethods.ComputeVectorFieldBidirectionalMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 6:
                    // vector Field bidirectional interpolated - chooses candidate whose Direction has minimum angle with interpolated vector Field point (bidirectional)
                    computeSendersValues = ComputingRSMethods.ComputeVectorFieldBidirectionalInterpolatedMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 7:
                    // density search 1 - chooses candidate with minimal bounding box volume with receiver
                    computeSendersValues = ComputingRSMethods.ComputeBBVolumeMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 8:
                    // density search 2 - chooses candidate with minimal bounding box diagonal with receiver
                    computeSendersValues = ComputingRSMethods.ComputeBBDiagonalMany;
                    selectSender = ComputingRSMethods.SelectMinIndex;
                    break;
                case 9:
                    // Weighted Random Choice among valid rules
                    computeSendersValues = ComputingRSMethods.ComputeWRC;
                    selectSender = ComputingRSMethods.SelectWRCIndex;
                    break;
                // . add more criteria here
                // ...
                //
                //case 99:
                //    // sequential Rule - tries to apply the heuristics set rules in sequence (buggy)
                //    // anonymous function - the computation is not necessary
                //    computeSendersValues = ComputeZero;
                //    //computeSendersValues = (candidates) => { return candidates.Select(ri => 0.0).ToArray(); };
                //    selectSender = SelectNextRuleIndex;
                //    break;

                default: goto case 0;
            }
        }
        /// <summary>
        /// Assign custom methods to Heuristics Settings
        /// </summary>
        /// <param name="customComputeReceiverValue">A custom method to compute Receiver value</param>
        /// <param name="customSelectReceiver">A custom method to select a Receiver</param>
        /// <param name="customComputeSendersValues">A custom method to compute Senders values</param>
        /// <param name="customSelectSender">A custom method to select a Sender</param>
        /// <remarks>These will overwrite existing methods. Pass a null value for methods you do not want to overwrite</remarks>
        public void AssignCustomMethods(ComputeReceiverMethod<double> customComputeReceiverValue, SelectWinnerMethod<double> customSelectReceiver, ComputeCandidatesValuesMethod<double> customComputeSendersValues, SelectWinnerMethod<double> customSelectSender)
        {
            if (customComputeReceiverValue != null)
                computeReceiverValue = customComputeReceiverValue;

            if (customSelectReceiver != null)
                selectReceiver = customSelectReceiver;

            if (customComputeSendersValues != null)
                computeSendersValues = customComputeSendersValues;

            if (customSelectSender != null)
                selectSender = customSelectSender;
        }
    }
}
