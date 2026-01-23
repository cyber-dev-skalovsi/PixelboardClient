using PixelboardClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddControllers();

builder.Services.AddSingleton<IGraphQLPixelService, GraphQLPixelService>();

builder.Services.AddSingleton<BoardStateService>();
builder.Services.AddSingleton<IBoardStateService>(services =>
    services.GetRequiredService<BoardStateService>());
builder.Services.AddHostedService(services =>
    services.GetRequiredService<BoardStateService>());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

app.Run();
