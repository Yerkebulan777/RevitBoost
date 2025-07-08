using Autodesk.Revit.DB;

namespace RevitUtils
{
    public static class UnitManager
    {
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

        public static double FootToMt(double length, int baseVal = 3)
        {
            return Math.Round(length * footToMm / 1000, baseVal);
        }

        public static double FootToMm(double length, int baseVal = 3)
        {
            return baseVal * Math.Round(length * footToMm / baseVal);
        }

        public static double CubicFootToCubicMeter(double volume)
        {
            return volume * cubicFootToCubicMeter;
        }

        public static string GetDysplayUnitType(Parameter param)
        {
            return LabelUtils.GetLabelForSpec(param.GetUnitTypeId());
        }



    }
}
