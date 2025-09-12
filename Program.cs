var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Options binding for LshVectorStore
builder.Services.AddOptions<SemanticSearch.Services.LshVectorStoreOptions>()
    .Bind(builder.Configuration.GetSection("VectorStore"))
    .Validate(o => o.NumPlanes > 0 && o.NumPlanes <= 64 && o.NumTables > 0 && o.MaxCandidates >= 200,
        "Invalid LshVectorStore options");

// Embedding options
builder.Services.AddOptions<SemanticSearch.Services.EmbeddingOptions>()
    .Bind(builder.Configuration.GetSection("Embedding"));

// Add in-memory caching
builder.Services.AddMemoryCache();

// Register synonym provider (file based)
builder.Services.AddSingleton<SemanticSearch.Services.ISynonymProvider>(sp =>
{
    var env = sp.GetRequiredService<IHostEnvironment>();
    var path = Path.Combine(env.ContentRootPath, "App_Data", "synonyms.txt");
    return new SemanticSearch.Services.FileSynonymProvider(path);
});

// Register vector store from options
builder.Services.AddSingleton<SemanticSearch.Services.IVectorStore>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SemanticSearch.Services.LshVectorStoreOptions>>().Value;
    return new SemanticSearch.Services.LshVectorStore(opts.NumTables, opts.NumPlanes, opts.MaxCandidates, opts.Seed);
});

// Register embedder provider selection
builder.Services.AddSingleton<SemanticSearch.Services.IEmbedder>(sp =>
{
    var embOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SemanticSearch.Services.EmbeddingOptions>>().Value;
    SemanticSearch.Services.IEmbedder inner = embOpts.Provider?.ToLowerInvariant() switch
    {
        "onnx" => new SemanticSearch.Services.OnnxEmbedder(embOpts),
        _ => new SemanticSearch.Services.MlNetEmbedder()
    };
    return new SemanticSearch.Services.CachingEmbedder(inner, maxEntries: 4096);
});

// Register semantic search service as singleton (data generated at startup)
builder.Services.AddSingleton<SemanticSearch.Services.ISemanticSearchService, SemanticSearch.Services.SemanticSearchService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
