using Autodesk.Revit.DB;

namespace RevitUtils
{
    public static class UnitManager
    {
        private const double epsilon = 0.003;
        private const double inchToMm = 25.4;
        private const double footToMm = 12 * inchToMm;
        private const double footToMeter = footToMm * 0.001;
        private const double sqfToSqm = footToMeter * footToMeter;
        private const double cubicFootToCubicMeter = footToMeter * sqfToSqm;

        public static bool IsAlmostEqual(double left, double right, double eps = double.Epsilon)
        {
            return Math.Abs(left - right) <= eps;
        }


        public static XYZ MmToFoot(XYZ vector)
        {
            return vector.Divide(footToMm);
        }

        public static double MmToFoot(double length)
        {
            return length / footToMm;
        }

        public static double FootToMm(double length)
        {
            return length * footToMm;
        }

        public static double FootToRoundedMm(double length, int baseVal = 10)
        {
            double millimeters = FootToMm(length);
            return baseVal * Math.Round(millimeters / baseVal);
        }

        public static double CubicFootToCubicMeter(double volume)
        {
            return volume * cubicFootToCubicMeter;
        }

        public static string GetDysplayUnitType(Parameter param)
        {
            return LabelUtils.GetLabelForSpec(param.Definition.GetDataType());
        }



    }
}
