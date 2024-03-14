using Microsoft.Extensions.Options;
using query.service;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddWindowsService();
builder.Services.AddCors();
builder.Services.AddHealthChecks();
builder.Services.AddOptions<MongoDBConnectionDetail>().Bind(builder.Configuration).BindConfiguration("MongoDB");

var app = builder.Build();

app.Urls.Add("http://0.0.0.0:32015");

app.UseCors(cors => cors
.AllowAnyMethod()
.AllowAnyHeader()
.SetIsOriginAllowed(origin => true)
.AllowCredentials()
);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();

var opt = app.Services.GetService<IOptions<MongoDBConnectionDetail>>();
var mongoService = new QueryMongoService(opt!);
var prtg = new HttpClient();

app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (ArgumentException ex)
    {
        Console.WriteLine($"[!!!] {ex.Message}");
        
        ctx.Response.StatusCode = StatusCodes.Status417ExpectationFailed;

        await ctx.Response.WriteAsync($"query exception {ex.Message} {ctx.Response.StatusCode}");
    }
});

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// GET section
app.MapGet("/", () => { return mongoService.Test(); });

// POST section
app.MapPost("/query/", (DBSettings s) => mongoService.Query(s.database, s.collection, s.query));

app.MapPost("/queryandproject/", (DBProject s) => mongoService.QueryAndProject(s.database, s.collection, s.query, s.project));

app.MapPost("/register/", (DBSettings s) => mongoService.Register(s.database, s.collection, s.query));

app.MapPost("/delete/", (DBSettings s) => mongoService.Delete(s.database, s.collection, s.query));

app.Run();

record DBSettings(string database, string collection, string query);

record DBProject(string database, string collection, string query, string project);
