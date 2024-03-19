packages:
{ config, pkgs, lib, specialArgs, ... }:

with lib;
with types;
let
  cfg = config.moe;
in
{
  options.moe = {
    enable = mkEnableOption "Enable the moe service";
    package = mkOption {
      type = package;
      default = packages.${pkgs.system}.default;
    };
    group = mkOption {
      type = str;
      description = ''
        The group for moe user that the systemd service will run under.
      '';
    };
    openFirewall = mkOption {
      type = bool;
      default = false;
      description = ''
        Whether to open the TCP port for status in the firewall.
      '';
    };
    settings = {
      backups-interval-minutes = mkOption {
        type = int;
        default = 60;
        description = ''
          Minutes between automatic database backups.
        '';
      };
      backups-to-keep = mkOption {
        type = int;
        default = 50;
        description = ''
          Delete old backups after the number of backups exceeds this.
        '';
      };
      status-port = mkOption {
        type = port;
        default = 8000;
        description = ''
          Start a web server on this port to appear online for status services.
        '';
      };
    };
    credentialsFile = mkOption {
      type = types.path;
      description = lib.mdDoc ''
        Path to a key-value pair file to be merged with the settings.
        Useful to merge a file which is better kept out of the Nix store
        to set secret config parameters like `token` and `owners`.
      '';
      default = "/dev/null";
      example = "/var/lib/secrets/moe/production.env";
    };
  };

  config = mkIf cfg.enable {
    users.users.moe = {
      isSystemUser = true;
      home = "/var/moe";
      createHome = true;
      group = cfg.group;
    };

    networking.firewall.allowedTCPPorts = mkIf cfg.openFirewall [ cfg.settings.status-port ];

    systemd.services.moe = {
      description = "Moe, a multi-purpose Discord bot made using Discord.Net.";
      wantedBy = [ "multi-user.target" ];
      serviceConfig = {
        Type = "simple";
        ExecStart = "${cfg.package}/bin/moe";
        WorkingDirectory = "/var/moe";
        User = "moe";
        EnvironmentFile = cfg.credentialsFile;
        Environment =
          let
            backups-interval-minutes = "BACKUP_INTERVAL_MINUTES=${toString cfg.settings.backups-interval-minutes}";
            backups-to-keep = "BACKUPS_TO_KEEP=${toString cfg.settings.backups-to-keep}";
            status-port = "STATUS_PORT=${toString cfg.settings.status-port}";
          in
          "${backups-interval-minutes} ${backups-to-keep} ${status-port}";
      };
    };
  };
}

