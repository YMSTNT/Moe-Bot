using Discord;
using Discord.WebSocket;
using TNTBot.Models;
using TNTBot.Services;

namespace TNTBot.Commands
{
  public class SnapshotCommand : SlashCommandBase
  {
    private readonly SnapshotService service;

    public SnapshotCommand(SnapshotService service) : base("snapshot")
    {
      Options = new SlashCommandOptionBuilder()
        .AddOption(new SlashCommandOptionBuilder()
          .WithName("add")
          .WithDescription("Add a snapshot")
          .AddOption("name", ApplicationCommandOptionType.String, "The name of the snapshot to add", isRequired: true)
          .AddOption("voicenames", ApplicationCommandOptionType.Boolean, "Whether to include voice channel names, default is false", isRequired: false)
          .AddOption("rolenames", ApplicationCommandOptionType.Boolean, "Whether to include role names, default is false", isRequired: false)
          .AddOption("servericon", ApplicationCommandOptionType.Boolean, "Whether to include server icon names, default is false", isRequired: false)
          .WithType(ApplicationCommandOptionType.SubCommand)
        ).AddOption(new SlashCommandOptionBuilder()
          .WithName("remove")
          .WithDescription("Remove a snapshot")
          .AddOption("name", ApplicationCommandOptionType.String, "The name of the snapshot to remove", isRequired: true)
          .WithType(ApplicationCommandOptionType.SubCommand)
        ).AddOption(new SlashCommandOptionBuilder()
          .WithName("restore")
          .WithDescription("Restore a snapshot")
          .AddOption("name", ApplicationCommandOptionType.String, "The name of the snapshot to restore", isRequired: true)
          .WithType(ApplicationCommandOptionType.SubCommand)
        ).AddOption(new SlashCommandOptionBuilder()
          .WithName("dump")
          .WithDescription("Dump a snapshot")
          .AddOption("name", ApplicationCommandOptionType.String, "The name of the snapshot to dump", isRequired: true)
          .WithType(ApplicationCommandOptionType.SubCommand)
        ).AddOption(new SlashCommandOptionBuilder()
          .WithName("list")
          .WithDescription("List snapshots")
          .WithType(ApplicationCommandOptionType.SubCommand)
        ).WithType(ApplicationCommandOptionType.SubCommandGroup);
      this.service = service;
    }

    public override async Task Handle(SocketSlashCommand cmd)
    {
      var user = (SocketGuildUser)cmd.User;
      var guild = user.Guild;
      var subcommand = cmd.GetSubcommand();

      if (!service.IsAuthorized(user, ModrankLevel.Administrator, out var error))
      {
        await cmd.RespondAsync(error);
        return;
      }

      var handle = subcommand.Name switch
      {
        "add" => AddSnapshot(cmd, subcommand, guild),
        "remove" => RemoveSnapshot(cmd, subcommand, guild),
        "restore" => RestoreSnapshot(cmd, subcommand, guild),
        "dump" => DumpSnapshot(cmd, subcommand, guild),
        "list" => ListSnapshots(cmd, guild),
        _ => throw new InvalidOperationException($"Unknown subcommand {subcommand.Name}")
      };

      await handle;
    }

    private async Task AddSnapshot(SocketSlashCommand cmd, SocketSlashCommandDataOption subcommand, SocketGuild guild)
    {
      var name = subcommand.GetOption<string>("name")!;
      var voicenames = subcommand.GetOption<bool>("voicenames");
      var rolenames = subcommand.GetOption<bool>("rolenames");
      var servericon = subcommand.GetOption<bool>("servericon");

      if (await service.HasSnapshot(guild, name))
      {
        await cmd.RespondAsync($"Snapshot **{name}** already exists");
        return;
      }

      await service.AddSnapshot(guild, name, voicenames, rolenames, servericon);
      await cmd.RespondAsync($"Added snapshot **{name}**");
    }

    private async Task RemoveSnapshot(SocketSlashCommand cmd, SocketSlashCommandDataOption subcommand, SocketGuild guild)
    {
      var name = subcommand.GetOption<string>("name")!;

      if (!await service.HasSnapshot(guild, name))
      {
        await cmd.RespondAsync($"Snapshot **{name}** does not exist");
        return;
      }

      await service.RemoveSnapshot(guild, name);
      await cmd.RespondAsync($"Removed snapshot **{name}**");
    }

    private async Task RestoreSnapshot(SocketSlashCommand cmd, SocketSlashCommandDataOption subcommand, SocketGuild guild)
    {
      var name = subcommand.GetOption<string>("name")!;

      if (!await service.HasSnapshot(guild, name))
      {
        await cmd.RespondAsync($"Snapshot **{name}** does not exist");
        return;
      }

      await cmd.DeferAsync();

      var message = $"Restoring snapshot **{name}**";
      var result = await service.RestoreSnapshot(guild, name);
      if (!string.IsNullOrEmpty(result))
      {
        message += $" but there were some errors:\n{result}";
      }

      await cmd.FollowupAsync(message);
    }

    private async Task DumpSnapshot(SocketSlashCommand cmd, SocketSlashCommandDataOption subcommand, SocketGuild guild)
    {
      var name = subcommand.GetOption<string>("name")!;

      if (!await service.HasSnapshot(guild, name))
      {
        await cmd.RespondAsync($"Snapshot **{name}** does not exist");
        return;
      }

      var s = await service.GetSnapshot(guild, name)!;
      var dump = service.GetSnapshotDump(s);

      var dumpMessage = $"Dumped snapshot **{name}**:\n{dump}";
      if (s.GuildIcon == null)
      {
        await cmd.RespondAsync(dumpMessage);
      }
      else
      {
        var icon = new FileAttachment(s.GuildIcon.Value.Stream, "servericon.png");
        await cmd.RespondWithFileAsync(icon, dumpMessage);
      }
    }
    private async Task ListSnapshots(SocketSlashCommand cmd, SocketGuild guild)
    {
      if (!await service.HasSnapshots(guild))
      {
        await cmd.RespondAsync("There are no snapshots");
        return;
      }

      var snapshots = await service.ListSnapshots(guild);
      var result = "Snapshots:";
      foreach (var snapshot in snapshots)
      {
        result += $"\n - **{snapshot}**";
      }

      await cmd.RespondAsync(result);
    }
  }
}
