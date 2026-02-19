using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;
using UltraPinball.Sample.Modes;

namespace UltraPinball.Sample;

public class SampleGame : GameController
{
    private AutoLaunchMode? _autoLaunch;

    public SampleGame(SampleMachine machine, IHardwarePlatform platform, ILoggerFactory loggerFactory)
        : base(machine, platform, loggerFactory) { }

    protected override void OnStartup()
    {
        Modes.Add(new AttractMode());
        Modes.Add(new DrainMode());
    }

    public override void StartBall()
    {
        base.StartBall();
        _autoLaunch = new AutoLaunchMode();
        Modes.Add(_autoLaunch);
        Coils["TroughEject"].Pulse();
    }

    public override void EndBall()
    {
        if (_autoLaunch != null)
        {
            Modes.Remove(_autoLaunch);
            _autoLaunch = null;
        }
        base.EndBall();
    }
}
