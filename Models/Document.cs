namespace SemanticSearch.Models;

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    // Sender/Submitter info
    public string SubmitterName { get; set; } = string.Empty;
    public int SubmitterAge { get; set; }
    public string SubmitterPhone { get; set; } = string.Empty;
    public string SubmitterGender { get; set; } = string.Empty;
    public string SubmitterCity { get; set; } = string.Empty;
}
