namespace LintelMaster;

/// <summary>
/// Manages grouping of lintels based on specified tolerances
/// </summary>
public class LintelGrouper
{
    private readonly MarkConfig _config;

    public LintelGrouper(MarkConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Groups lintels according to tolerance parameters defined in MarkConfig
    /// </summary>
    public List<List<LintelData>> GroupLintels(List<LintelData> lintels)
    {
        // Sort lintels by most significant parameters first for efficient grouping
        var sortedLintels = lintels.OrderBy(l => l.Thick)
                                  .ThenBy(l => l.Width)
                                  .ThenBy(l => l.Height)
                                  .ToList();

        var groups = new List<List<LintelData>>();

        foreach (var lintel in sortedLintels)
        {
            bool addedToExistingGroup = false;

            // Try to add to an existing group that meets tolerance requirements
            foreach (var group in groups)
            {
                if (CanAddToGroup(group, lintel))
                {
                    group.Add(lintel);
                    addedToExistingGroup = true;
                    break;
                }
            }

            // Create a new group if the lintel doesn't fit existing groups
            if (!addedToExistingGroup)
            {
                groups.Add(new List<LintelData> { lintel });
            }
        }

        // Assign group identifiers (optional)
        AssignGroupIdentifiers(groups);

        return groups;
    }

    /// <summary>
    /// Determines if a lintel can be added to an existing group based on tolerance constraints
    /// </summary>
    private bool CanAddToGroup(List<LintelData> group, LintelData candidate)
    {
        // Use the first element of the group as reference
        var reference = group[0];

        // Check individual parameter tolerances
        bool thicknessWithinTolerance = Math.Abs(candidate.Thick - reference.Thick) <= _config.ThickTolerance;
        bool widthWithinTolerance = Math.Abs(candidate.Width - reference.Width) <= _config.WidthTolerance;
        bool heightWithinTolerance = Math.Abs(candidate.Height - reference.Height) <= _config.HeightTolerance;

        // Calculate total deviation against reference values
        double totalDeviation = Math.Abs(candidate.Thick - reference.Thick) +
                               Math.Abs(candidate.Width - reference.Width) +
                               Math.Abs(candidate.Height - reference.Height);

        // Ensure total deviation is within allowed limit
        bool totalDeviationWithinLimit = totalDeviation <= _config.MaxTotalDeviation;

        return thicknessWithinTolerance && widthWithinTolerance && heightWithinTolerance && totalDeviationWithinLimit;
    }

    /// <summary>
    /// Assigns group identifiers to grouped lintels
    /// </summary>
    private void AssignGroupIdentifiers(List<List<LintelData>> groups)
    {
        // Implementation would depend on how SizeKey is structured
        // For example:
        for (int i = 0; i < groups.Count; i++)
        {
            var representativeLintel = groups[i][0];
            var sizeKey = new SizeKey(); // Assuming SizeKey has a default constructor

            // Set appropriate values for the group
            // This would need to be adjusted based on SizeKey implementation

            // Assign the same SizeKey to all lintels in the group
            foreach (var lintel in groups[i])
            {
                lintel.Size = sizeKey;
            }
        }
    }
}