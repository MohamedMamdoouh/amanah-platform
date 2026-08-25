using Amanah.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UsePipeline();

app.Run();
