using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PixelboardClient.Controllers;
using PixelboardClient.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddOpenIdConnect(options =>
{
    options.Authority = "http://localhost:18080/realms/pixelboard-test";
    options.ClientId = "student_client";
    options.ClientSecret = builder.Configuration["OpenIDConnectSettings:ClientSecret"] ?? "";

    options.RequireHttpsMetadata = false;
    options.CallbackPath = "/signin-oidc";
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.SaveTokens = true;

    options.Scope.Clear();
    options.Scope.Add("openid");
    options.Scope.Add("profile");
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IGraphQLPixelService, GraphQLPixelService>();
builder.Services.AddSingleton<BoardStateService>();
builder.Services.AddSingleton<IBoardStateService>(provider =>
    provider.GetRequiredService<BoardStateService>());
builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<BoardStateService>());
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();


app.MapGet("/sse/pixels", async (IBoardStateService boardState, HttpContext context) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");

    while (!context.RequestAborted.IsCancellationRequested)
    {
        try
        {
            var pixels = boardState.GetAllPixels();
            var rand = new Random();
            var x = rand.Next(16);
            var y = rand.Next(16);
            var color = pixels[x, y];

            var update = new
            {
                x,
                y,
                red = color.Red,
                green = color.Green,
                blue = color.Blue
            };

            var json = JsonSerializer.Serialize(update);
            await context.Response.WriteAsync($"data: {json}\n\n");
            await context.Response.Body.FlushAsync();

            await Task.Delay(5000, context.RequestAborted);
        }
        catch (Exception ex)
        {
            break;
        }
    }
}).RequireAuthorization().ExcludeFromDescription();
app.Run();
