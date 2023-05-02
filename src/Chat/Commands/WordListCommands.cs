using System.Linq;
using HarmonyLib;
using TOHTOR.Managers;
using TOHTOR.Utilities;
using VentLib.Commands;
using VentLib.Commands.Attributes;

namespace TOHTOR.Chat.Commands;

[Command(CommandFlag.HostOnly, "wordlist", "wl")]
public class WordListCommands
{
    [Command("list")]
    private static void ListWords(PlayerControl source)
    {
        Utils.SendMessage(
            ChatManager.BannedWords.Select((w, i) => $"{i+1}) {w}").Join(delimiter: "\n"),
            source.PlayerId
            );
    }

    [Command("add")]
    private static void AddWord(PlayerControl source, CommandContext context, string word)
    {
        ChatManager.AddWord(word);
    }

    [Command("reload")]
    private static void Reload(PlayerControl source)
    {
        ChatManager.Reload();
        Utils.SendMessage("Successfully Reloaded Wordlist", source.PlayerId);
    }

    private static ChatManager ChatManager => PluginDataManager.ChatManager;

}