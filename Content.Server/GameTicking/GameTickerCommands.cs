﻿using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Health.BodySystem;
using Content.Server.Health.BodySystem.BodyPart;
using Content.Server.Interfaces.GameTicking;
using Content.Server.Players;
using Content.Shared.Health.BodySystem;
using Content.Shared.Health.BodySystem.BodyPart;
using Content.Shared.Maps;
using Content.Shared.Roles;
using Robust.Server.Interfaces.Console;
using Robust.Server.Interfaces.Player;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking
{
    class DelayStartCommand : IClientCommand
    {
        public string Command => "delaystart";
        public string Description => "Delays the round start.";
        public string Help => $"Usage: {Command} <seconds>\nPauses/Resumes the countdown if no argument is provided.";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var ticker = IoCManager.Resolve<IGameTicker>();
            if (ticker.RunLevel != GameRunLevel.PreRoundLobby)
            {
                shell.SendText(player, "This can only be executed while the game is in the pre-round lobby.");
                return;
            }

            if (args.Length == 0)
            {
                var paused = ticker.TogglePause();
                shell.SendText(player, paused ? "Paused the countdown." : "Resumed the countdown.");
                return;
            }

            if (args.Length != 1)
            {
                shell.SendText(player, "Need zero or one arguments.");
                return;
            }

            if (!uint.TryParse(args[0], out var seconds) || seconds == 0)
            {
                shell.SendText(player, $"{args[0]} isn't a valid amount of seconds.");
                return;
            }

            var time = TimeSpan.FromSeconds(seconds);
            if (!ticker.DelayStart(time))
            {
                shell.SendText(player, "An unknown error has occurred.");
            }
        }
    }

    class StartRoundCommand : IClientCommand
    {
        public string Command => "startround";
        public string Description => "Ends PreRoundLobby state and starts the round.";
        public string Help => String.Empty;

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var ticker = IoCManager.Resolve<IGameTicker>();

            if (ticker.RunLevel != GameRunLevel.PreRoundLobby)
            {
                shell.SendText(player, "This can only be executed while the game is in the pre-round lobby.");
                return;
            }

            ticker.StartRound();
        }
    }

    class EndRoundCommand : IClientCommand
    {
        public string Command => "endround";
        public string Description => "Ends the round and moves the server to PostRound.";
        public string Help => String.Empty;

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var ticker = IoCManager.Resolve<IGameTicker>();

            if (ticker.RunLevel != GameRunLevel.InRound)
            {
                shell.SendText(player, "This can only be executed while the game is in a round.");
                return;
            }

            ticker.EndRound();
        }
    }

    class NewRoundCommand : IClientCommand
    {
        public string Command => "restartround";
        public string Description => "Moves the server from PostRound to a new PreRoundLobby.";
        public string Help => String.Empty;

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var ticker = IoCManager.Resolve<IGameTicker>();
            ticker.RestartRound();
        }
    }

    class RespawnCommand : IClientCommand
    {
        public string Command => "respawn";
        public string Description => "Respawns a player, kicking them back to the lobby.";
        public string Help => "respawn [player]";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length > 1)
            {
                shell.SendText(player, "Must provide <= 1 argument.");
                return;
            }

            var playerMgr = IoCManager.Resolve<IPlayerManager>();
            var ticker = IoCManager.Resolve<IGameTicker>();

            NetSessionId sessionId;
            if (args.Length == 0)
            {
                if (player == null)
                {
                    shell.SendText((IPlayerSession)null, "If not a player, an argument must be given.");
                    return;
                }

                sessionId = player.SessionId;
            }
            else
            {
                sessionId = new NetSessionId(args[0]);
            }

            if (!playerMgr.TryGetSessionById(sessionId, out var targetPlayer))
            {
                if (!playerMgr.TryGetPlayerData(sessionId, out var data))
                {
                    shell.SendText(player, "Unknown player");
                    return;
                }

                data.ContentData().WipeMind();
                shell.SendText(player,
                    "Player is not currently online, but they will respawn if they come back online");
                return;
            }

            ticker.Respawn(targetPlayer);
        }
    }

    class ObserveCommand : IClientCommand
    {
        public string Command => "observe";
        public string Description => "";
        public string Help => "";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (player == null)
            {
                return;
            }

            var ticker = IoCManager.Resolve<IGameTicker>();
            ticker.MakeObserve(player);
        }
    }

    class JoinGameCommand : IClientCommand
    {
#pragma warning disable 649
        [Dependency] private IPrototypeManager _prototypeManager;
#pragma warning restore 649
        public string Command => "joingame";
        public string Description => "";
        public string Help => "";

        public JoinGameCommand()
        {
            IoCManager.InjectDependencies(this);
        }
        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var output = string.Join(".", args);
            if (player == null)
            {
                return;
            }

            var ticker = IoCManager.Resolve<IGameTicker>();
            if (ticker.RunLevel == GameRunLevel.PreRoundLobby)
            {
                shell.SendText(player, "Round has not started.");
                return;
            }
            else if(ticker.RunLevel == GameRunLevel.InRound)
            {
                string ID = args[0];
                var positions = ticker.GetAvailablePositions();

                if(positions.GetValueOrDefault(ID, 0) == 0) //n < 0 is treated as infinite
                {
                    var jobPrototype = _prototypeManager.Index<JobPrototype>(ID);
                    shell.SendText(player, $"{jobPrototype.Name} has no available slots.");
                    return;
                }
                ticker.MakeJoinGame(player, args[0].ToString());
                return;
            }

            ticker.MakeJoinGame(player, null);
        }
    }

    class ToggleReadyCommand : IClientCommand
    {
        public string Command => "toggleready";
        public string Description => "";
        public string Help => "";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (player == null)
            {
                return;
            }

            var ticker = IoCManager.Resolve<IGameTicker>();
            ticker.ToggleReady(player, bool.Parse(args[0]));
        }
    }

    class SetGamePresetCommand : IClientCommand
    {
        public string Command => "setgamepreset";
        public string Description => "";
        public string Help => "";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length != 1)
            {
                shell.SendText(player, "Need exactly one argument.");
                return;
            }

            var ticker = IoCManager.Resolve<IGameTicker>();

            ticker.SetStartPreset(args[0]);
        }
    }

    class ForcePresetCommand : IClientCommand
    {
        public string Command => "forcepreset";
        public string Description => "Forces a specific game preset to start for the current lobby.";
        public string Help => $"Usage: {Command} <preset>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var ticker = IoCManager.Resolve<IGameTicker>();
            if (ticker.RunLevel != GameRunLevel.PreRoundLobby)
            {
                shell.SendText(player, "This can only be executed while the game is in the pre-round lobby.");
                return;
            }

            if (args.Length != 1)
            {
                shell.SendText(player, "Need exactly one argument.");
                return;
            }

            var name = args[0];
            if (!ticker.TryGetPreset(name, out var type))
            {
                shell.SendText(player, $"No preset exists with name {name}.");
                return;
            }

            ticker.SetStartPreset(type, true);
            shell.SendText(player, $"Forced the game to start with preset {name}.");
        }
    }

    class MappingCommand : IClientCommand
    {
        public string Command => "mapping";
        public string Description => "Creates and teleports you to a new uninitialized map for mapping.";
        public string Help => $"Usage: {Command} <id> <mapname>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (player == null)
            {
                shell.SendText(player, "Only players can use this command");
                return;
            }

            if (args.Length != 2)
            {
                shell.SendText(player, Help);
                return;
            }

            shell.ExecuteCommand(player, $"addmap {args[0]} false");
            shell.ExecuteCommand(player, $"loadbp {args[0]} \"{CommandParsing.Escape(args[1])}\"");
            shell.ExecuteCommand(player, $"aghost");
            shell.ExecuteCommand(player, $"tp 0 0 {args[0]}");

            shell.SendText(player, $"Created unloaded map from file {args[1]} with id {args[0]}. Use \"savebp 4 foo.yml\" to save it.");
        }
    }

    class AddHandCommand : IClientCommand
    {
        public string Command => "addhand";
        public string Description => "Adds a hand to your entity.";
        public string Help => $"Usage: {Command}";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (player == null)
            {
                shell.SendText((IPlayerSession) null, "Only a player can run this command.");
                return;
            }

            if (player.AttachedEntity == null)
            {
                shell.SendText(player, "You have no entity.");
                return;
            }

            if (!player.AttachedEntity.TryGetComponent(out BodyManagerComponent body))
            {
                var random = IoCManager.Resolve<IRobustRandom>();
                var text = $"You have no body{(random.Prob(0.2f) ? " and you must scream." : ".")}";

                shell.SendText(player, text);
                return;
            }

            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            prototypeManager.TryIndex("bodyPart.Hand.BasicHuman", out BodyPartPrototype prototype);

            var part = new BodyPart(prototype);
            var slot = part.GetHashCode().ToString();

            body.Template.Slots.Add(slot, BodyPartType.Hand);
            body.InstallBodyPart(part, slot);
        }
    }

    class RemoveHandCommand : IClientCommand
    {
        public string Command => "removehand";
        public string Description => "Removes a hand from your entity.";
        public string Help => $"Usage: {Command}";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (player == null)
            {
                shell.SendText((IPlayerSession) null, "Only a player can run this command.");
                return;
            }

            if (player.AttachedEntity == null)
            {
                shell.SendText(player, "You have no entity.");
                return;
            }

            var manager = player.AttachedEntity.GetComponent<BodyManagerComponent>();
            var hand = manager.PartDictionary.FirstOrDefault(x => x.Value.PartType == BodyPartType.Hand);
            if (hand.Value == null)
            {
                shell.SendText(player, "You have no hands.");
                return;
            }
            else
            {
                manager.DisconnectBodyPart(hand.Value, true);
            }
        }
    }

    class TileWallsCommand : IClientCommand
    {
        // ReSharper disable once StringLiteralTypo
        public string Command => "tilewalls";
        public string Description => "Puts an underplating tile below every wall on a grid.";
        public string Help => $"Usage: {Command} <gridId> | {Command}";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            GridId gridId;

            switch (args.Length)
            {
                case 0:
                    if (player?.AttachedEntity == null)
                    {
                        shell.SendText((IPlayerSession) null, "Only a player can run this command.");
                        return;
                    }

                    gridId = player.AttachedEntity.Transform.GridID;
                    break;
                case 1:
                    if (!int.TryParse(args[0], out var id))
                    {
                        shell.SendText(player, $"{args[0]} is not a valid integer.");
                        return;
                    }

                    gridId = new GridId(id);
                    break;
                default:
                    shell.SendText(player, Help);
                    return;
            }

            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.TryGetGrid(gridId, out var grid))
            {
                shell.SendText(player, $"No grid exists with id {gridId}");
                return;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();
            if (!entityManager.TryGetEntity(grid.GridEntityId, out var gridEntity))
            {
                shell.SendText(player, $"Grid {gridId} doesn't have an associated grid entity.");
                return;
            }

            var tileDefinitionManager = IoCManager.Resolve<ITileDefinitionManager>();
            var underplating = tileDefinitionManager["underplating"];
            var underplatingTile = new Tile(underplating.TileId);
            var changed = 0;
            foreach (var childUid in gridEntity.Transform.ChildEntityUids)
            {
                if (!entityManager.TryGetEntity(childUid, out var childEntity))
                {
                    continue;
                }

                var prototype = childEntity.Prototype;
                while (true)
                {
                    if (prototype?.Parent == null)
                    {
                        break;
                    }

                    prototype = prototype.Parent;
                }

                if (prototype?.ID != "base_wall")
                {
                    continue;
                }

                if (!childEntity.TryGetComponent(out SnapGridComponent snapGrid))
                {
                    continue;
                }

                var tile = grid.GetTileRef(childEntity.Transform.GridPosition);
                var tileDef = (ContentTileDefinition) tileDefinitionManager[tile.Tile.TypeId];

                if (tileDef.Name == "underplating")
                {
                    continue;
                }

                grid.SetTile(childEntity.Transform.GridPosition, underplatingTile);
                changed++;
            }

            shell.SendText(player, $"Changed {changed} tiles.");
        }
    }
}
