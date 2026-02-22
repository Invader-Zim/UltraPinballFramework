using UltraPinball.Core.Devices;

namespace UltraPinball.Core.Game;

/// <summary>
/// System mode that provides hardware diagnostics for operators and developers.
///
/// <para>
/// Activated by any switch tagged <see cref="SwitchTags.Service"/>. While active,
/// all coils are disabled for safety, every switch activation is intercepted and
/// posted as <see cref="MediaEvents.ServiceSwitchActivated"/>, and individual coils
/// can be fired via <see cref="TestCoil"/>.
/// </para>
/// </summary>
public class ServiceMode : Mode
{
    public override ModeLifecycle DefaultLifecycle => ModeLifecycle.System;

    /// <summary>True while service mode is active.</summary>
    public bool IsActive { get; private set; }

    public ServiceMode() : base(priority: 100) { }

    public override void ModeStarted()
    {
        var serviceSw = Game.Switches.SingleOrDefault(sw => sw.Tags.HasFlag(SwitchTags.Service));
        if (serviceSw != null)
            AddSwitchHandler(serviceSw.Name, SwitchActivation.Active, OnServiceSwitch);

        foreach (var sw in Game.Switches)
        {
            if (sw.Tags.HasFlag(SwitchTags.Service)) continue;
            AddSwitchHandler(sw.Name, SwitchActivation.Active, OnAnySwitch);
        }
    }

    /// <summary>
    /// Pulses a named coil while service mode is active.
    /// The coil is temporarily re-enabled, pulsed, then immediately disabled again.
    /// Does nothing when service mode is not active.
    /// </summary>
    public void TestCoil(string coilName)
    {
        if (!IsActive) return;
        var coil = Game.Coils[coilName];
        coil.Enable();
        coil.Pulse();
        coil.Disable();
    }

    private SwitchHandlerResult OnServiceSwitch(Switch _)
    {
        if (IsActive) Exit(); else Enter();
        return SwitchHandlerResult.Stop;
    }

    private SwitchHandlerResult OnAnySwitch(Switch sw)
    {
        if (!IsActive) return SwitchHandlerResult.Continue;
        Game.Media?.Post(MediaEvents.ServiceSwitchActivated, new { name = sw.Name });
        return SwitchHandlerResult.Stop;
    }

    private void Enter()
    {
        IsActive = true;
        foreach (var coil in Game.Coils)
            coil.Disable();
        Game.Media?.Post(MediaEvents.ServiceModeEntered);
    }

    private void Exit()
    {
        IsActive = false;
        foreach (var coil in Game.Coils)
            coil.Enable();
        Game.Media?.Post(MediaEvents.ServiceModeExited);
    }
}
