using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Logging;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using ChaseMod.Utils;

using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Cvars;

namespace ChaseMod;
internal class RoundStartFreezeTimeManager
{
    private static ILogger Logger = CoreLogging.Factory.CreateLogger("RoundStartFreezeTimeManager");

    private readonly ChaseMod _plugin;
    private readonly PlayerFreezeManager _playerFreezeManager;
    private float _roundStartTime;
    private int _roundStartTick;
    private float? _normalFalldamageScale;

    public float FrozenUntilTime => _roundStartTime + _plugin.Config.RoundStartFreezeTime;
    public int FrozenUntilTick => _roundStartTick + (int)(_plugin.Config.RoundStartFreezeTime / Server.TickInterval);

    private Timer? _countdownTimer;

    public RoundStartFreezeTimeManager(ChaseMod chaseMod, PlayerFreezeManager playerFreezeManager)
    {
        _plugin = chaseMod;
        _playerFreezeManager = playerFreezeManager;
    }

    public void Start()
    {
        _plugin.RegisterEventHandler<EventRoundFreezeEnd>((@event, info) =>
        {
            var gameRules = ChaseModUtils.GetGameRules();
            if (gameRules.WarmupPeriod)
            {
                return HookResult.Continue;
            }

            _roundStartTime = Server.CurrentTime;
            _roundStartTick = Server.TickCount;

            if (_plugin.Config.RoundStartFreezeTime <= 0)
            {
                return HookResult.Continue;
            }

            SwitchFallDamage(false);

            foreach (var player in ChaseModUtils.GetAllRealPlayers())
            {
                if (!player.PlayerPawn.IsValid) continue;
                var pawn = player.PlayerPawn.Value!;

                if (player.Team == CsTeam.CounterTerrorist)
                {
                    _playerFreezeManager.Freeze(player, _plugin.Config.RoundStartFreezeTime, true, false, true);
                }
            }

            if (_countdownTimer != null)
            {
                SwitchFallDamage(true);
                _countdownTimer.Kill();
                _countdownTimer = null;
            }

            _countdownTimer = _plugin.AddTimer(0.1f, CountdownTimerTick, TimerFlags.REPEAT);

            return HookResult.Continue;
        });
    }

    private void CountdownTimerTick()
    {
        var timeLeft = FrozenUntilTime - Server.CurrentTime;
        if (timeLeft <= 0)
        {
            SwitchFallDamage(true);
            _countdownTimer?.Kill();
            _countdownTimer = null;
        }

        foreach (var player in ChaseModUtils.GetAllRealPlayers())
        {
            player.PrintToCenter(timeLeft > 0 ? $"Round begins in {timeLeft:0.0} seconds!" : "Round start!");
        }
    }

    private void SwitchFallDamage(bool enabled)
    {
        if (enabled)
        {
            // convar SetValue isn't replicated on clients it seems...
            Server.ExecuteCommand($"sv_falldamage_scale {_normalFalldamageScale ?? 1}");
        }
        else
        {
            if (_normalFalldamageScale == null)
            {
                var falldamageScale = ConVar.Find("sv_falldamage_scale");
                if (falldamageScale != null)
                {
                    _normalFalldamageScale = falldamageScale.GetPrimitiveValue<float>();
                }
            }

            Server.ExecuteCommand($"sv_falldamage_scale 0");
        }
    }

    public bool IsInFreezeTime()
    {
        return Server.TickCount < FrozenUntilTick;
    }

}