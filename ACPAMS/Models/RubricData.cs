using System.Collections.Generic;

namespace ACPAMS.Models
{
    public class RubricData
    {
        public string Description { get; set; } = string.Empty;
        public string AiInstructions { get; set; } = string.Empty;
        public int TotalWeight { get; set; }
        public List<RubricCriteriaData> Criteria { get; set; } = new();

        /// <summary>Full path to the rubric reference PDF attached in AIRubricModal,
        /// if one was provided. Set by CreateExamPage from AIRubricModal.FilePath —
        /// previously this was being discarded entirely after the modal closed.</summary>
        public string? AttachmentFilePath { get; set; }
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