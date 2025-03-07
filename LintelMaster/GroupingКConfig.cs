public class GroupingConfig
{
    public string FamilyName { get; set; } = "Перемычка";
    public int MinGroupThreshold { get; set; } = 5;
    public int OptimalGroupSize { get; set; } = 10;

    #region Параметры весов для группировки
    public double ThickWeight { get; set; } = 0.6;
    public double WidthWeight { get; set; } = 0.3;
    public double HeightWeight { get; set; } = 0.1;
    public double GroupSizeWeight { get; set; } = 0.4;
    #endregion

    #region Параметры допусков для группировки
    public int ThickTolerance { get; set; } = 25;
    public int WidthTolerance { get; set; } = 50;
    public int HeightTolerance { get; set; } = 300;
    public int MaxTotalDeviation { get; set; } = 500;

    #endregion

    #region Наименования параметров
    public string ThickParameter { get; set; } = "Толщина стены";
    public string WidthParameter { get; set; } = "Ширина проема";
    public string HeightParameter { get; set; } = "Высота";

    #endregion

    #region Параметры для маркировки
    public string Prefix { get; set; } = "ПР-";
    public string MarkParam { get; set; } = "BI_марка_изделия";
    #endregion

}