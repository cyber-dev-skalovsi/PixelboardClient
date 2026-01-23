using PixelboardClient.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// Background Service und IBoardStateService registrieren
builder.Services.AddSingleton<BoardStateService>();
builder.Services.AddSingleton<IBoardStateService>(services =>
    services.GetRequiredService<BoardStateService>());
builder.Services.AddHostedService(services =>
    services.GetRequiredService<BoardStateService>());

var app = builder.Build();

// Configure the HTTP request pipeline
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

app.Run();
