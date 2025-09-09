var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Options binding for LshVectorStore
builder.Services.AddOptions<SemanticSearch.Services.LshVectorStoreOptions>()
    .Bind(builder.Configuration.GetSection("VectorStore"))
    .Validate(o => o.NumPlanes > 0 && o.NumPlanes <= 64 && o.NumTables > 0 && o.MaxCandidates >= 200,
        "Invalid LshVectorStore options");

// Register vector store from options
builder.Services.AddSingleton<SemanticSearch.Services.IVectorStore>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SemanticSearch.Services.LshVectorStoreOptions>>().Value;
    return new SemanticSearch.Services.LshVectorStore(opts.NumTables, opts.NumPlanes, opts.MaxCandidates, opts.Seed);
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
