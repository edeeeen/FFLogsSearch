using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using FFLogsSearch.Windows;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace FFLogsSearch;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/fflogs";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("FFLogsSearch");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    
    private IDataManager DM { get; init; }
    public IContextMenu ContextMenu { get; init; }
    public Plugin(IDataManager dataManager, IContextMenu contextMenu)
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // you might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Uses args to search fflogs :^)"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [FFLogsSearch] ===A cool log message from Sample Plugin===
        Log.Information($"==={PluginInterface.Manifest.Name}===");
        
        this.ContextMenu = contextMenu;
        this.ContextMenu.OnMenuOpened += this.OnContextMenuOpened;
        this.DM = dataManager;
        
        //aether
        this.NA.Add(73, "adamantoise");
        this.NA.Add(79, "cactuar");
        this.NA.Add(54, "faerie");
        this.NA.Add(63, "gilgamesh");
        this.NA.Add(40, "jenova");
        this.NA.Add(65, "midgardsormr");
        this.NA.Add(99, "sargatanas");
        this.NA.Add(57, "siren");
        //crystal
        this.NA.Add(91, "balmung");
        this.NA.Add(34, "brynhildr");
        this.NA.Add(74, "coeurl");
        this.NA.Add(62, "diabolos");
        this.NA.Add(81, "goblin");
        this.NA.Add(75, "malboro");
        this.NA.Add(37, "mateus");
        this.NA.Add(41, "zalera");
        //primal
        this.NA.Add(78, "behemoth");
        this.NA.Add(93, "excalibur");
        this.NA.Add(53, "exodus");
        this.NA.Add(35, "famfrit");
        this.NA.Add(95, "hyperion");
        this.NA.Add(55, "lamia");
        this.NA.Add(64, "leviathan");
        this.NA.Add(77, "ultros");
        //dynamis
        this.NA.Add(406, "halicarnassus");
        this.NA.Add(407, "maduin");
        this.NA.Add(404, "marilith");
        this.NA.Add(405, "seraph");
        this.NA.Add(408, "cuchulainn");
        this.NA.Add(411, "golem");
        this.NA.Add(409, "kraken");
        this.NA.Add(410, "rafflesia");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        
        this.ContextMenu.OnMenuOpened -= this.OnContextMenuOpened;
        
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        OpenUrl("https://www.fflogs.com/search/?term=" + args);
        // in response to the slash command, just toggle the display status of our main ui
    }
    private static readonly string[] ValidAddons =
    [
        null,
        "PartyMemberList",
        "FriendList",
        "FreeCompany",
        "LinkShell",
        "CrossWorldLinkshell",
        "_PartyList",
        "ChatLog",
        "LookingForGroup",
        "BlackList",
        "ContentMemberList",
        "SocialList",
        "ContactList",
    ];

    private Dictionary<uint, string> NA = new Dictionary<uint, string>();
    
    
    
    private void OnContextMenuOpened(IMenuOpenedArgs args)
    {


        if (Array.IndexOf(ValidAddons, args.AddonName) != -1 && args.Target is MenuTargetDefault defMen && defMen.TargetName != null && defMen.TargetName != null)
        {
            args.AddMenuItem(new()
            {
                OnClicked = (_) =>
                {
                    //defMen.TargetHomeWorld
                    uint worldId = defMen.TargetHomeWorld.Value.RowId;
                    if (NA.ContainsKey(worldId))
                    {
                        OpenUrl("https://www.fflogs.com/character/na/" + NA[worldId] + "/" + defMen.TargetName);
                    }
                    else
                    {
                        OpenUrl("https://www.fflogs.com/search/?term=" + defMen.TargetName);
                    }
                    
                },
                Prefix = SeIconChar.BoxedLetterF,
                Name = "Search FFLogs",
                PrefixColor = 15
            });
        }
    }
    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }
    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
