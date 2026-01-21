var builder = WebApplication.CreateBuilder(args);

// Razor Pages hinzufügen
builder.Services.AddRazorPages();

// HttpClient für PixelboardService registrieren
builder.Services.AddHttpClient<PixelboardClient.Services.PixelboardService>();

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
