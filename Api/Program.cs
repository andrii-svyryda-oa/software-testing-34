using System.Text.Json.Serialization;
using Api.Modules;
using Api.Modules.Errors;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.SetupServices();

var app = builder.Build();

app.UseMiddleware<ValidationExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await app.InitialiseDb();
app.MapControllers();
app.Run();

public partial class Program;
