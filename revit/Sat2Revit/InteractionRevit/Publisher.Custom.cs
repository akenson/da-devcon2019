using System.Collections.Generic;
using Autodesk.Forge.DesignAutomation.Model;

namespace Interaction
{
    /// <summary>
    /// Customizable part of Publisher class.
    /// </summary>
    internal partial class Publisher
    {
        /// <summary>
        /// Constants.
        /// </summary>
        private static class Constants
        {
            private const int EngineVersion = 2019;
            public static readonly string Engine = $"Autodesk.Revit+{EngineVersion}";

            public const string Description = "PUT DESCRIPTION HERE";

            internal static class Bundle
            {
                public static readonly string Id = "Sat2Revit";
                public const string Label = "alpha";

                public static readonly AppBundle Definition = new AppBundle
                {
                    Engine = Engine,
                    Id = Id,
                    Description = Description
                };
            }

            internal static class Activity
            {
                public static readonly string Id = Bundle.Id;
                public const string Label = Bundle.Label;
            }

            internal static class Parameters
            {

                public const string InputRvt = nameof(InputRvt);
                public const string InputGeometry = nameof(InputGeometry);
                public const string FamilyTemplate = nameof(FamilyTemplate);
                public const string ResultModel = nameof(ResultModel);
            }
        }


        /// <summary>
        /// Get command line for activity.
        /// </summary>
        private static List<string> GetActivityCommandLine()
        {
            return new List<string> { $"$(engine.path)\\revitcoreconsole.exe /al $(appbundles[{Constants.Activity.Id}].path)" };
        }

        /// <summary>
        /// Get activity parameters.
        /// </summary>
        private static Dictionary<string, Parameter> GetActivityParams()
        {

            return new Dictionary<string, Parameter>
                    {
                        {
                            Constants.Parameters.InputGeometry,
                            new Parameter
                            {
                                LocalName = "Input.sat",
                                Description = "Input SAT File",
                                Verb = Verb.Get,
                                Ondemand = false,
                                Required = true,
                                Zip = false
                            }
                        },
                        {
                            Constants.Parameters.FamilyTemplate,
                            new Parameter
                            {
                                LocalName = "FamilyTemplate.rft",
                                Description = "Input RFT File",
                                Verb = Verb.Get,
                                Ondemand = false,
                                Required = true,
                                Zip = false
                            }
                        },
                        {
                            Constants.Parameters.ResultModel,
                            new Parameter
                            {
                                LocalName = "Output.rfa",
                                Description = "Output rfa File",
                                Verb = Verb.Put,
                                Ondemand = false,
                                Required = true,
                                Zip = false
                            }
                        }
                    };
        }

        /// <summary>
        /// Get arguments for workitem.
        /// </summary>
        private static Dictionary<string, IArgument> GetWorkItemArgs()
        {
            // TODO: update the URLs below with real values
            return new Dictionary<string, IArgument>
                    {
                        {
                            Constants.Parameters.InputGeometry,
                            new XrefTreeArgument
                            {
                                Url = "!!! CHANGE ME !!!"

                            }
                        },
                        {
                            Constants.Parameters.FamilyTemplate,
                            new XrefTreeArgument
                            {
                                Url = "!!! CHANGE ME !!!"

                            }
                        },
                        {
                            Constants.Parameters.ResultModel,
                            new XrefTreeArgument
                            {
                                Verb = Verb.Put,
                                Url = "!!! CHANGE ME !!!"

                            }
                        } 
                    };
        }
    }
}
