using Microsoft.Extensions.Logging;
using UltraPinball.Core.Game;
using UltraPinball.Core.Platform;
using UltraPinball.Sample.Modes;

namespace UltraPinball.Sample;

public class SampleGame : GameController
{
    public SampleGame(SampleMachine machine, IHardwarePlatform platform, ILoggerFactory loggerFactory)
        : base(machine, platform, loggerFactory) { }

    protected override void OnStartup()
    {
        RegisterMode(new AttractMode(),  ModeLifecycle.System);
        RegisterMode(new TroughMode(["Trough0", "Trough1", "Trough2", "Trough3", "Trough4"]),
                     ModeLifecycle.System);
        RegisterMode(new AutoLaunchMode(), ModeLifecycle.Ball);
        RegisterMode(new SingleBall(),    ModeLifecycle.Ball);
    }
}
