using graphicstransform.service;
using Microsoft.AspNetCore.Mvc;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddWindowsService();
builder.Services.AddSingleton<GraphicsServer>();

var app = builder.Build();

app.Urls.Add("http://0.0.0.0:32006");

app.UseCors(cors => cors
.AllowAnyMethod()
.AllowAnyHeader()
.SetIsOriginAllowed(origin => true)
.AllowCredentials()
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMetricServer();
app.UseHttpsRedirection();

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

        await ctx.Response.WriteAsync(ctx.Response.StatusCode.ToString());
    }
});

app.UseAuthorization();

app.MapControllers();

// GET section

// POST section

app.MapPost("/resize", (Resize resize, [FromServices] GraphicsServer server) => server.Resize(resize.wfactor, resize.hfactor, resize.data));

app.MapPost("/rotateflip", (RotateFlip rotate, [FromServices] GraphicsServer server) => server.RotateFlip(rotate.rotate, rotate.flip, rotate.data));

app.MapPost("/drawimageonimage", (ImageOnImage ioi, [FromServices] GraphicsServer server) =>
    server.DrawImageOnImage(ioi.dstData, ioi.srcData, new Rectangle(ioi.x, ioi.y, ioi.w, ioi.h))
);

app.MapPost("/colorkeyrect", (ColorKeyRect ckr, [FromServices] GraphicsServer server) => server.ColorkeyRect(ckr.r, ckr.g, ckr.b, ckr.data));

app.MapPost("/colorkeyrectalpha", (DataRect ckr, [FromServices] GraphicsServer server) => server.ColorkeyRectAlpha(ckr.data));

app.Run();

record Resize(float wfactor, float hfactor, string data);
record RotateFlip(int rotate, int flip, string data);
record ImageOnImage(string dstData, string srcData, int x, int y, int w, int h);
record ColorKeyRect(byte r, byte g, byte b, string data);
record DataRect(string data);
