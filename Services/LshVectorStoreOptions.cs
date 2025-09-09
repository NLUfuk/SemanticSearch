namespace SemanticSearch.Services;

public class LshVectorStoreOptions
{
    public int NumTables { get; set; } = 8;
    public int NumPlanes { get; set; } = 24;
    public int MaxCandidates { get; set; } = 3000;
    public int Seed { get; set; } = 42;
}
