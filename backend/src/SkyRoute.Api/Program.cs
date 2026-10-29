var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

// Contract first: the OpenAPI document is always published so other groups can integrate.
app.MapOpenApi();

app.UseHttpsRedirection();

app.Run();
