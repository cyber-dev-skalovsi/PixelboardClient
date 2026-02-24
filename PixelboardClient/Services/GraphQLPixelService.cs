using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using PixelboardClient.Models;
using System.Text;
using System.Text.Json;

namespace PixelboardClient.Services
{
    public class GraphQLPixelService : IGraphQLPixelService
    {
        private readonly ILogger<GraphQLPixelService> _logger;
        private readonly IConfiguration _configuration;
        private readonly GraphQLHttpClient _graphQLClient;

        public GraphQLPixelService(
            ILogger<GraphQLPixelService> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            var apiUrl = configuration["ApiUrl"];
            var graphQLUrl = $"{apiUrl}/graphql";

            _graphQLClient = new GraphQLHttpClient(
                graphQLUrl,
                new SystemTextJsonSerializer()
            );
        }

        public class PixelResponse
        {
            public PixelData? Pixel { get; set; }
        }

        public async Task<PixelColor[,]> LoadAllPixelsAsync()
        {
            var pixels = new PixelColor[16, 16];

            try
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int y = 0; y < 16; y++)
                    {
                        var request = new GraphQLRequest
                        {
                            Query = @"
                        query GetPixel($x: Int!, $y: Int!) {
                            pixel(x: $x, y: $y) {
                                x
                                y
                                color {
                                    red
                                    green
                                    blue
                                }
                            }
                        }",
                            Variables = new { x, y }
                        };

                        var response = await _graphQLClient.SendQueryAsync<PixelResponse>(request);

                        if (response.Errors?.Any() == true)
                        {
                            _logger.LogError("GraphQL Fehler bei Pixel ({x},{y}): {err}", x, y, response.Errors.First().Message);
                            pixels[x, y] = new PixelColor(255, 0, 255); // Magenta = Error
                            continue;
                        }

                        var p = response.Data?.Pixel;
                        if (p?.Color != null)
                        {
                            pixels[x, y] = new PixelColor(p.Color.Red, p.Color.Green, p.Color.Blue);
                        }
                        else
                        {
                            pixels[x, y] = new PixelColor(0, 0, 0);
                        }
                    }
                }
                _logger.LogInformation("GraphQL: Alle 256 Pixels geladen");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GraphQL komplett fehlgeschlagen - Fallback REST");
                // REST Fallback passiert in BoardStateService
            }

            return pixels;
        }


        public class PixelRangeResponse
        {
            public List<PixelData> PixelRange { get; set; } = new();
        }

        public class PixelData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public ColorData Color { get; set; } = new();
        }

        public class ColorData
        {
            public int Red { get; set; }
            public int Green { get; set; }
            public int Blue { get; set; }
        }
    }
}
