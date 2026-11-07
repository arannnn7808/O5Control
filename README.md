<div align="center">

# O5 Control

LabAPI plugin that connects your SCP: Secret Laboratory servers to [O5 Control](https://o5control.com).

[![Downloads](https://img.shields.io/github/downloads/arannnn7808/O5Control/total?style=for-the-badge&color=FF6B2C)](https://github.com/arannnn7808/O5Control/releases)
[![Release](https://img.shields.io/github/v/release/arannnn7808/O5Control?style=for-the-badge&color=FF6B2C)](https://github.com/arannnn7808/O5Control/releases/latest)
[![License](https://img.shields.io/github/license/arannnn7808/O5Control?style=for-the-badge)](LICENSE)
[![LabAPI](https://img.shields.io/badge/LabAPI-1.1.7%2B-2b2b2b?style=for-the-badge)](https://github.com/northwood-studios/LabAPI)

</div>

## Features

- Bans, mutes, warnings and kicks shared across every server of your instance.
- Staff roles from the panel or Discord become Remote Admin groups.
- Admin Chat, commands, kills, joins, role changes, reports and rounds sent to the panel logs.
- Live console, broadcasts and round restart from the panel.
- Anti-VPN and anti-alts.
- Verified automatic updates.

## Installation

1. Download `O5Control.dll` from the [latest release](https://github.com/arannnn7808/O5Control/releases/latest).
2. Put it in `LabAPI/plugins/global` and start the server.
3. In the panel open **Servers → Connect a server** and run the command it gives you in the server console:

```
o5 setup O5-XXXX-XXXX en
```

## Commands

| Command | Description |
| --- | --- |
| `o5 setup <code> [en\|es\|fr\|pl]` | Links the server with a code from the panel. Without a language it follows the instance language. |
| `o5 status` | Shows the connection state. |
| `o5 reconnect` | Reopens the connection to the panel. |
| `o5 unlink confirm` | Removes the server's credential. |

`o5control` works as an alias. From Remote Admin every command except `status` needs the `ServerConsoleCommands` permission.

## Translations

Messages shown to players (ban screen, kicks, warnings, mutes) live in `translations.yml`, next to `config.yml`. Their language follows the instance language set in the panel (or the one given to `o5 setup`); when it changes, the file is rewritten with that language's texts. You can also edit the file to write your own. Available: English, Español, Français and Polski.

## For developers

Reference `O5Control.dll` (without copying it to your output) and use the `O5Control.API` namespace.

```csharp
using O5Control.API;

if (O5.TryGetBan(player.UserId, out var ban))
    Logger.Info($"Banned: {ban!.Reason}");

var result = await O5.BanAsync(player, "Cheating", TimeSpan.FromDays(7), issuer);
if (!result.Success)
    Logger.Warn($"{result.ErrorCode}: {result.Error}");

await O5.MuteAsync(player, "Mic spam", TimeSpan.FromHours(1));
await O5.WarnAsync(player, "Teamkilling");
await O5.KickAsync(player, "AFK");

O5.Log("anticheat", "Speed hack detected", player);

O5Events.SanctionApplied += sanction => { };
O5Events.StaffAssigned += staff => { };
```

Methods return `O5Result` and never throw. Ban checks read a local cache, so they are instant. Northwood staff can't be sanctioned (`protected`).

A working plugin is in [O5Control.Example](O5Control.Example).
