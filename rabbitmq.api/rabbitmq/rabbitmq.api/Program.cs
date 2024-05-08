using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using rabbitmq.api;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("RABBITMQ_");

builder.Services.AddOptions<RabbitMQConnectionDetail>().Bind(builder.Configuration).BindConfiguration("RabbitMQ");

builder.Services.AddCors();

builder.Services.AddSingleton(svc =>
    ActivatorUtilities.CreateInstance<RabbitServer>(svc, svc.GetRequiredService<IOptions<RabbitMQConnectionDetail>>().Value));

var app = builder.Build();

app.UseCors(cors => cors
    .AllowAnyMethod()
    .AllowAnyHeader()
    .SetIsOriginAllowed(origin => true)
    .AllowCredentials()
    );

Console.WriteLine($"{Environment.Version}");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.MapGet("/", () => "RABBITMQ API " + app.Services.GetRequiredService<IOptions<RabbitMQConnectionDetail>>().Value.Version);

app.MapPost("/sendmessage", (RabbitMessage rm, [FromServices] RabbitServer svc) =>
    svc.SendMessage(rm.exchange, rm.routingkey, rm.message, rm.vhost));

app.MapPost("/sendmessagewithresponse", async (RabbitMessage rm, [FromServices] RabbitServer svc) => 
{
    var responseTask = svc.SendMessageWithResponse(rm.exchange, rm.routingkey, rm.message, rm.vhost, rm.timeout);

    var timeouttask = Task.Run(async () => { await Task.Delay(rm.timeout); return ""; });

    var winner = await Task.WhenAny(responseTask, timeouttask);

    return winner == responseTask ? Results.Ok(responseTask.Result) : Results.NotFound($"{rm.routingkey} service down");
});

app.MapPost("/sendbinarymessage", (RabbitMessage rm, [FromServices] RabbitServer svc) =>
    svc.SendMessageBinary(rm.exchange, rm.routingkey, rm.message, rm.vhost));

app.Run();

record RabbitMessage(string exchange, string routingkey, string message, string vhost="/", int timeout = 30000);
