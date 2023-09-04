using DNS_round_robin.HttpClient;
using DNS_round_robin.HttpClient.Interfaces;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ISocketHttpClientSettings, SocketHttpClientSettings>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var socketHttpEndpoint = app.MapGroup("/api/v1/socketHttp/");

socketHttpEndpoint.MapGet("getDns/{url}", [Produces("application/json")] async (string url, ISocketHttpClientSettings socketHttpClientSettings) =>
{
    string decodedUrl = Uri.UnescapeDataString(url);

    await IsUriAccessible(decodedUrl);
    var httpClient = new HttpClient(socketHttpClientSettings.SocketHttp(), disposeHandler: true);

    var result = await httpClient.GetStringAsync(decodedUrl);
    return result;
})
.WithName("SocketHttp-Round-Robin")
.WithOpenApi();

static bool IsValidUri(string uri)
{
    return Uri.TryCreate(uri, UriKind.Absolute, out Uri? result) &&
            (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);
}

async Task IsUriAccessible(string uri)
{
    if (!IsValidUri(uri))
        throw new BadHttpRequestException("URI é invalida!");

    using (HttpClient httpClient = new())
    {
        try
        {
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri));
            if (!response.IsSuccessStatusCode)
                throw new BadHttpRequestException("URI inacessivel no momento!");
        }
        catch
        {
            throw;
        }
    }
}


app.Run();
