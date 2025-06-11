using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RandomStartMap;

public class RandomStartMapConfig : BasePluginConfig
{
    [JsonPropertyName("SteamApiKey")]
    public string SteamApiKey { get; set; } = "your_steam_api_key_here";
    
    [JsonPropertyName("WorkshopCollectionId")]
    public string WorkshopCollectionId { get; set; } = "your_collection_id_here";
    
    [JsonPropertyName("ChangeDelay")]
    public float ChangeDelay { get; set; } = 2.0f;
}

public class RandomStartMap : BasePlugin, IPluginConfig<RandomStartMapConfig>
{
    public override string ModuleName => "RandomStartMap";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "RexusOhm";

    public RandomStartMapConfig Config { get; set; } = new();
    private readonly HttpClient _httpClient = new();
    private bool _isFirstLoad = true;
    
    public void OnConfigParsed(RandomStartMapConfig config)
    {
        Config = config;
    }
    public override void Load(bool hotReload)
    {
        _isFirstLoad = !hotReload;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        if (!_isFirstLoad) return;
        
        AddTimer(Config.ChangeDelay, async () => {
            await LoadAndChangeToRandomMapAsync();
        });
    }

    private async Task LoadAndChangeToRandomMapAsync()
    {
        Console.WriteLine("[RandomStartMap] Loading workshop collection...");

        try
        {
            var collectionFormData = new Dictionary<string, string>
            {
                { "collectioncount", "1" },
                { "publishedfileids[0]", Config.WorkshopCollectionId }
            };

            var collectionResponse = await _httpClient.PostAsync(
                $"https://api.steampowered.com/ISteamRemoteStorage/GetCollectionDetails/v1/?key={Config.SteamApiKey}",
                new FormUrlEncodedContent(collectionFormData));

            collectionResponse.EnsureSuccessStatusCode();
            var collectionJson = await collectionResponse.Content.ReadAsStringAsync();
            using var collectionDoc = JsonDocument.Parse(collectionJson);

            var mapIds = collectionDoc.RootElement
                .GetProperty("response")
                .GetProperty("collectiondetails")[0]
                .GetProperty("children")
                .EnumerateArray()
                .Select(item => item.GetProperty("publishedfileid").GetString()!)
                .ToList();

            if (mapIds.Count == 0)
            {
                Console.WriteLine("[RandomStartMap] ERROR: No maps found in collection!");
                return;
            }

            var random = new Random();
            string randomMapId = mapIds[random.Next(mapIds.Count)];
            
            Console.WriteLine($"[RandomStartMap] Changing to random map from collection: {randomMapId}");
            
            // Выполняем команду в главном потоке
            await Server.NextFrameAsync(() => {
                Server.ExecuteCommand($"host_workshop_map {randomMapId}");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RandomStartMap] ERROR: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[RandomStartMap] INNER ERROR: {ex.InnerException.Message}");
            }
        }
    }
}