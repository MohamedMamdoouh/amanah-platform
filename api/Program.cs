using Amanah.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UsePipeline();

app.Run();
