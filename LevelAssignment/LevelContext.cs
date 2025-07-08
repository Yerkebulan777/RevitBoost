using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LevelAssignment
{
    internal readonly record struct LevelContext
    {
        public required int Index { get; init; }
        public required string Name { get; init; }
        public required int Total { get; init; }
        public required double Elevation { get; init; }
        public required double PreviousElevation { get; init; }
        public required double ElevationDifference { get; init; }
    }
}
