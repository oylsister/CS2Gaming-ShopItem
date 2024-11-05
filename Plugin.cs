using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using CS2GamingAPIShared;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ShopAPI;
using static CounterStrikeSharp.API.Core.Listeners;

namespace ShopRestriction
{
    public class Plugin : BasePlugin
    {
        public override string ModuleName => "[Shop] Restriction";
        public override string ModuleVersion => "1.0";

        private ICS2GamingAPIShared? _cs2gamingAPI { get; set; }
        private IShopApi? _shopAPI { get; set; }
        public static PluginCapability<ICS2GamingAPIShared> _capability { get; } = new("cs2gamingAPI");
        public Dictionary<CCSPlayerController, PlayerData> _playerBought { get; set; } = new();
        public string? filePath { get; set; }
        public readonly ILogger<Plugin> _logger;

        public override void Load(bool hotReload)
        {
            RegisterListener<OnClientDisconnect>(OnClientDisconnect);
            InitializeData();
        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            _cs2gamingAPI = _capability.Get();
            _shopAPI = IShopApi.Capability.Get()!;

            if(_shopAPI == null)
            {
                _logger.LogError("ShopAPI is null!");
                return;
            }

            _shopAPI.ClientBuyItemPre += OnClientBuyItem;
        }

        public Plugin(ILogger<Plugin> logger)
        {
            _logger = logger;
        }

        public void InitializeData()
        {
            filePath = Path.Combine(ModuleDirectory, "playerdata.json");

            if (!File.Exists(filePath))
            {
                var empty = "{}";

                File.WriteAllText(filePath, empty);
                _logger.LogInformation("Data file is not found creating a new one.");
            }

            _logger.LogInformation("Found Data file at {0}.", filePath);
        }

        [GameEventHandler]
        public HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var client = @event.Userid;

            if (!IsValidPlayer(client))
                return HookResult.Continue;

            var steamID = client!.AuthorizedSteamID!.SteamId64;

            var data = GetPlayerData(steamID);

            if (data == null)
                _playerBought.Add(client!, new(false, DateTime.Now.ToString(), DateTime.Now.AddDays(7.0f).ToString()));

            else
            {
                var bought = data.Bought;
                var timeReset = DateTime.ParseExact(data.TimeReset, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
                if (timeReset <= DateTime.Now)
                {
                    bought = false;
                    Task.Run(async () => await SaveClientData(steamID, bought, true));
                }

                _playerBought.Add(client!, new(bought, DateTime.Now.ToString(), DateTime.Now.AddDays(7.0f).ToString()));
            }

            return HookResult.Continue;
        }

        public void OnClientDisconnect(int playerslot)
        {
            var client = Utilities.GetPlayerFromSlot(playerslot);

            if (!IsValidPlayer(client))
                return;

            var steamID = client!.AuthorizedSteamID!.SteamId64;
            var bought = _playerBought[client].Bought;

            Task.Run(async () => await SaveClientData(steamID, bought, !bought));

            _playerBought.Remove(client!);
        }

        public HookResult? OnClientBuyItem(CCSPlayerController client, int ItemID, string CategoryName, string UniqueName, int BuyPrice, int SellPrice, int Duration, int Count)
        {
            if (!_playerBought.ContainsKey(client))
                return HookResult.Continue;

            if (_playerBought[client].Bought)
            {
                var data = GetPlayerData(client.AuthorizedSteamID!.SteamId64);

                var now = DateTime.Now;
                var available = DateTime.ParseExact(_playerBought[client].TimeReset, "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);

                var timeleft = available - now;

                client.PrintToChat($" {Localizer.ForPlayer(client, "Prefix")} {Localizer.ForPlayer(client, "Cooldown", timeleft.Days, timeleft.Hours, timeleft.Minutes)}");
                return HookResult.Handled;
            }

            OnItemBought(client);
            return HookResult.Continue;
        }

        public void OnItemBought(CCSPlayerController client)
        {
            if (!IsValidPlayer(client))
                return;

            if (!_playerBought.ContainsKey(client!))
                return;

            if (_playerBought[client!].Bought)
                return;

            var steamid = client.AuthorizedSteamID?.SteamId64;
            Task.Run(async () => await BuyComplete(client!, (ulong)steamid!));
        }

        public async Task BuyComplete(CCSPlayerController client, ulong steamid)
        {
            if (_playerBought[client].Bought)
                return;

            _playerBought[client].Bought = true;

            var finishTime = DateTime.Now.ToString();
            var resetTime = DateTime.Now.AddDays(7.0).ToString();

            _playerBought[client].TimeReset = resetTime;
            _playerBought[client].TimeAcheived = finishTime;

            var response = await _cs2gamingAPI?.RequestSteamID(steamid!)!;
            if (response != null)
            {
                if (response.Status != 200)
                    return;

                Server.NextFrame(() =>
                {
                    var language = client.GetLanguage();
                    string message = "";

                    if (language.TwoLetterISOLanguageName == "ru_RU")
                        message = response.Message_RU!;

                    else
                        message = response.Message!;

                    client.PrintToChat($" {ChatColors.Green}[Shop]{ChatColors.White} {message}");
                });

                await SaveClientData(steamid!, true, true);
            }
        }

        private async Task SaveClientData(ulong steamid, bool bought, bool settime)
        {
            var finishTime = DateTime.Now.ToString();
            var resetTime = DateTime.Now.AddDays(7.0).ToString();
            var steamKey = steamid.ToString();

            var data = new PlayerData(bought, finishTime, resetTime);

            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return;

            if (jsonObject.ContainsKey(steamKey))
            {
                jsonObject[steamKey].Bought = bought;

                if (settime)
                {
                    jsonObject[steamKey].TimeAcheived = finishTime;
                    jsonObject[steamKey].TimeReset = resetTime;
                }

                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
            }

            else
            {
                jsonObject.Add(steamKey, data);
                var updated = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                await File.WriteAllTextAsync(filePath!, updated);
            }
        }

        private PlayerData? GetPlayerData(ulong steamid)
        {
            var jsonObject = ParseFileToJsonObject();

            if (jsonObject == null)
                return null;

            var steamKey = steamid.ToString();

            if (jsonObject.ContainsKey(steamKey))
                return jsonObject[steamKey];

            return null;
        }

        private Dictionary<string, PlayerData>? ParseFileToJsonObject()
        {
            if (!File.Exists(filePath))
                return null;

            return JsonConvert.DeserializeObject<Dictionary<string, PlayerData>>(File.ReadAllText(filePath));
        }

        public bool IsValidPlayer(CCSPlayerController? client)
        {
            return client != null && client.IsValid && !client.IsBot;
        }
    }
}
