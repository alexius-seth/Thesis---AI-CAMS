using System.Collections.Generic;

namespace ACPAMS.Models
{
    public class RubricData
    {
        public string Description { get; set; } = string.Empty;
        public string AiInstructions { get; set; } = string.Empty;
        public int TotalWeight { get; set; }
        public List<RubricCriteriaData> Criteria { get; set; } = new();
    }

    public class RubricCriteriaData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double MaxScore { get; set; }
        public decimal WeightPercentage { get; set; }
        public List<RubricLevelData> Levels { get; set; } = new();
    }

    public class RubricLevelData
    {
        public string Name { get; set; } = string.Empty;
        public double Points { get; set; }
        public string Description { get; set; } = string.Empty;
        public double ScorePercentage { get; set; }
        public string Descriptor { get; set; } = string.Empty;
    }
}